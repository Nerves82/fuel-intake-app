using System.Globalization;

namespace FuelIntake.Core.Import;

/// <summary>
/// Household tables: one row per person under printed column headings (NEKCA Intake page 1, the GoEngage data
/// printout, agency forms). Finds the heading row, works out where each column runs, and turns every row that has a
/// name into a household member: row 1 is the client, the rest become members 2, 3… on the approval page.
/// </summary>
public static partial class ScanFieldReader
{
    /// <summary>Column headings per household field (lower case, punctuation ignored). Longest match wins.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ColumnLabels = new Dictionary<string, string[]>
    {
        ["lastName"] = ["last name", "surname", "last"],
        ["firstName"] = ["first name", "given name", "first"],
        ["name"] = ["full name", "member name", "name"],
        ["dateOfBirth"] = ["date of birth", "birth date", "birthdate", "dob"],
        ["age"] = ["age"],
        ["relationship"] = ["relationship", "relation"],
        ["gender"] = ["gender identity", "gender", "sex"],
        ["disabled"] = ["disabled", "disability", "dis"],
        ["education"] = ["last grade completed", "last grade", "highest grade", "education", "grade"],
        ["race"] = ["race"],
        ["hispanic"] = ["hispanic or not hispanic", "hispanic or not", "hispanic", "hisp", "ethnicity"],
        ["healthInsurance"] = ["health insurance source", "health insurance", "health", "insurance"],
        ["militaryStatus"] = ["military status", "military", "veteran status"],
        ["occupation"] = ["occupation", "employment status"],
        ["ssn"] = ["social security number", "social security", "ssn", "ss", "soc sec"],
        ["incomePerMonth"] = ["income per month", "monthly income", "income per", "income mo", "income"],
        ["incomeSource"] = ["income source", "source of income", "source"],
        ["verified"] = ["verified"],
    };

    /// <summary>Words that wrap onto the heading's other lines ("(Y/N)", "(Employer, Medicaid, Etc.)").</summary>
    private static readonly HashSet<string> HeadingWords = new(
        ColumnLabels.Values.SelectMany(a => a).SelectMany(l => l.Split(' ')).Concat(
        [
            "y", "n", "or", "not", "per", "month", "mo", "of", "employer", "medicaid", "etc", "veteran", "active", "none",
            "completed", "identity", "status", "number", "no", "yes", "type", "member", "members", "household",
        ]), StringComparer.Ordinal);

    /// <summary>The table has ended: the certification or signature lines under it.</summary>
    private static readonly string[] TableEnd = ["certify", "signature", "continue to page", "participant", "for household members 14"];

    private const int MaxMembers = 12;

    /// <summary>A household table read off a page: the cells of each row that has a name, by field.</summary>
    public sealed record HouseholdRow(Dictionary<string, (string Raw, ScanSpot Spot)> Cells);

    /// <summary>Finds household tables on the pages (the first one with rows wins) and returns their rows in order.</summary>
    public static (List<HouseholdRow> Rows, HashSet<string> Columns)? ReadHousehold(IReadOnlyList<OcrPage> pages)
    {
        foreach (var page in pages)
        {
            var lines = Rows(page).Lines.OrderBy(l => l.Y).ToList();
            for (var i = 0; i < lines.Count; i++)
            {
                var seed = Headings(lines[i]);
                if (seed.Count < 3 || !seed.Any(h => h.Key is "lastName" or "firstName" or "name")) continue;
                var table = ReadTable(page, lines, i);
                if (table is { Rows.Count: > 0 }) return table;
            }
        }
        return null;
    }

    private static (List<HouseholdRow> Rows, HashSet<string> Columns)? ReadTable(OcrPage page, List<OcrLine> lines, int seedIndex)
    {
        // the heading band: the seed row plus the rows just above and below made of heading words
        // (headings wrap, and short ones sit in the middle of tall heading cells)
        int top = seedIndex, bottom = seedIndex;
        var rowHeight = lines[seedIndex].Height;
        while (top > 0 && IsHeadingLine(lines[top - 1]) && lines[top].Y - lines[top - 1].Bottom < rowHeight * 1.5) top--;
        while (bottom + 1 < lines.Count && IsHeadingLine(lines[bottom + 1]) && lines[bottom + 1].Y - lines[bottom].Bottom < rowHeight * 1.5) bottom++;

        // columns, reading the heading rows top to bottom: a wrapped heading's first words name its column
        // ("Health / Insurance / Source"), and words under a column already found only widen it
        var columns = new List<(string Key, double X0, double X1)>();
        for (var k = top; k <= bottom; k++)
            foreach (var h in Headings(lines[k]))
            {
                var over = columns.FindIndex(c => h.X0 < c.X1 && h.X1 > c.X0);
                if (over >= 0) { var c = columns[over]; columns[over] = (c.Key, Math.Min(c.X0, h.X0), Math.Max(c.X1, h.X1)); }
                else if (!columns.Any(c => c.Key == h.Key)) columns.Add(h);
            }
        columns = columns.OrderBy(c => c.X0).ToList();
        if (columns.Count < 3) return null;

        // a cell's words belong to the column whose span is nearest: boundaries halfway between neighbouring headings
        var bounds = new double[columns.Count + 1];
        bounds[0] = double.NegativeInfinity; bounds[^1] = double.PositiveInfinity;
        for (var c = 1; c < columns.Count; c++) bounds[c] = (columns[c - 1].X1 + columns[c].X0) / 2;

        var rows = new List<HouseholdRow>();
        var last = lines[bottom].Bottom;
        for (var k = bottom + 1; k < lines.Count && rows.Count < MaxMembers; k++)
        {
            var line = lines[k];
            var text = Norm(string.Join(' ', line.Words.Select(w => w.Text)));
            if (TableEnd.Any(e => text.Contains(e, StringComparison.Ordinal))) break;
            if (line.Y - last > 0.12) break; // well past the last row
            last = line.Bottom;

            var cells = new Dictionary<string, List<OcrWord>>();
            foreach (var w in line.Words)
            {
                var mid = w.X + w.Width / 2;
                var c = Array.FindIndex(bounds, 1, b => mid < b) - 1;
                if (c < 0) continue;
                var key = columns[c].Key;
                (cells.TryGetValue(key, out var list) ? list : cells[key] = []).Add(w);
            }
            var row = cells.ToDictionary(
                kv => kv.Key,
                kv => (Raw: Clean(string.Join(' ', kv.Value.Select(w => w.Text))), Spot: SpotOf(page, kv.Value)));
            foreach (var key in row.Where(kv => kv.Value.Raw.Length == 0).Select(kv => kv.Key).ToList()) row.Remove(key);
            if (!new[] { "lastName", "firstName", "name" }.Any(n => row.TryGetValue(n, out var v) && v.Raw.Any(char.IsLetter))) continue;
            rows.Add(new HouseholdRow(row));
        }
        return (rows, columns.Select(c => c.Key).ToHashSet());
    }

    private static bool IsHeadingLine(OcrLine line)
    {
        var words = Norm(string.Join(' ', line.Words.Select(w => w.Text))).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && !words.Any(w => w.Any(char.IsDigit)) && words.Count(HeadingWords.Contains) >= words.Length * 0.7;
    }

    /// <summary>Column headings on one row, left to right, with where each sits. Longest heading first, so
    /// "Last Grade Completed" is education, not last name.</summary>
    private static List<(string Key, double X0, double X1)> Headings(OcrLine line)
    {
        var words = line.Words.Select(w => Norm(w.Text)).ToList();
        var labels = ColumnLabels.SelectMany(kv => kv.Value.Select(l => (kv.Key, Words: l.Split(' '))))
            .OrderByDescending(x => x.Words.Length).ThenByDescending(x => x.Words.Sum(w => w.Length)).ToList();
        var found = new List<(string, double, double)>();
        for (var i = 0; i < words.Count;)
        {
            var hit = labels.FirstOrDefault(l => i + l.Words.Length <= words.Count && l.Words.Select((w, k) => words[i + k] == w).All(x => x));
            if (hit.Words is null) { i++; continue; }
            var ws = line.Words.Skip(i).Take(hit.Words.Length).ToList();
            found.Add((hit.Key, ws.Min(w => w.X), ws.Max(w => w.Right)));
            i += hit.Words.Length;
        }
        return found;
    }

    private static ScanSpot SpotOf(OcrPage page, List<OcrWord> ws) =>
        new(page.File, page.Page, ws.Min(w => w.X), ws.Min(w => w.Y), ws.Max(w => w.Right) - ws.Min(w => w.X), ws.Max(w => w.Bottom) - ws.Min(w => w.Y));

    /// <summary>
    /// Puts a household table's rows on the approval page, adding members as needed (never removing any). Row 1 is
    /// the client. A column the table has wins over what the label reader guessed for that field, and an empty cell
    /// clears such a guess (a heading's "(Y/N)" isn't an answer). Ticked and staff-edited fields are never touched.
    /// </summary>
    private static int FillHousehold(PaperImport import, IReadOnlyList<OcrPage> pages)
    {
        if (ReadHousehold(pages) is not { } table) return 0;
        var filled = 0;
        for (var n = 0; n < table.Rows.Count; n++)
        {
            while (import.MemberCount < n + 1) PaperImportRules.AddMember(import);
            var cells = table.Rows[n].Cells;
            if (cells.TryGetValue("name", out var whole) && SplitName(whole.Raw) is var (first, last))
            {
                cells.TryAdd("firstName", (first, whole.Spot));
                cells.TryAdd("lastName", (last, whole.Spot));
            }
            var columns = table.Columns.Contains("name") ? [.. table.Columns, "firstName", "lastName"] : table.Columns;
            foreach (var key in columns)
            {
                var path = PathFor(n, key);
                var field = path is null ? null : import.Fields.FirstOrDefault(f => f.Path == path);
                if (field is null || field.Confirmed || !(string.IsNullOrWhiteSpace(field.Value) || field.Value == field.ReadValue)) continue;
                if (!cells.TryGetValue(key, out var cell))
                {
                    if (!string.IsNullOrWhiteSpace(field.ReadValue)) { field.Value = null; field.ReadValue = null; field.Spot = null; field.Confidence = 0; }
                    continue;
                }
                var (value, ok) = Shape(field, cell.Raw);
                var unmatchedChoice = value.Length == 0 && field.Kind == ImportFieldKind.Choice && cell.Raw.Any(char.IsLetter);
                if (value.Length == 0 && !unmatchedChoice) { FuzzyHit = false; continue; }
                field.Value = value.Length == 0 ? null : value;
                field.ReadValue = value.Length == 0 ? cell.Raw : value;
                field.Confidence = ok ? 0.6 : unmatchedChoice ? 0.1 : FuzzyHit ? 0.45 : 0.3;
                field.Spot = cell.Spot;
                FuzzyHit = false;
                filled++;
            }
        }
        return filled;
    }

    /// <summary>The approval-page field for a table column; the client's name and birth date live on the client fields.</summary>
    private static string? PathFor(int memberIndex, string key) => key switch
    {
        "lastName" or "firstName" or "dateOfBirth" when memberIndex == 0 => $"client.{key}",
        "name" or "age" or "relationship" or "verified" => null,
        _ => $"household[{memberIndex.ToString(CultureInfo.InvariantCulture)}].{key}",
    };

    /// <summary>"Smith, Jane" or "Jane A Smith" → (first, last); null for a single word.</summary>
    private static (string First, string Last)? SplitName(string raw)
    {
        if (raw.Contains(','))
        {
            var parts = raw.Split(',', 2, StringSplitOptions.TrimEntries);
            return (parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "", parts[0]);
        }
        var words = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length < 2 ? null : (words[0], words[^1]);
    }
}
