using System.Globalization;
using System.Text.RegularExpressions;
using FuelIntake.Core.Formatting;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Import;

/// <summary>
/// Fills approval-page fields from the text read off the scans: finds each field's printed label
/// ("Last Name", "Date of Birth", "SSN"…) and takes the writing next to it (right, then below, then above).
/// Works on any form with printed labels; every value still has to be checked and ticked by staff.
/// Only blank, unticked fields are filled, so re-reading never overwrites staff work.
/// </summary>
public static partial class ScanFieldReader
{
    /// <summary>Printed labels to look for, per field (lower case; punctuation ignored).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Labels = new Dictionary<string, string[]>
    {
        ["client.lastName"] = ["last name", "client last name", "applicant last name", "surname", "family name", "lname"],
        ["client.firstName"] = ["first name", "client first name", "applicant first name", "given name", "fname"],
        ["client.dateOfBirth"] = ["date of birth", "dob", "d o b", "birth date", "birthdate", "birthday"],
        ["client.addressPhysical"] = ["physical address", "street address", "home address", "residence address", "residential address",
            "address line 1", "address 1", "address1", "street", "address"],
        ["client.city"] = ["town", "city", "city town", "town city", "municipality"],
        ["client.state"] = ["state"],
        ["client.zip"] = ["zip", "zip code", "zipcode", "postal code", "zip postal code"],
        ["client.addressMailing"] = ["mailing address", "mail address", "mailing address line 1"],
        ["client.phone"] = ["phone", "phone number", "telephone", "primary phone", "primary phone number", "cell phone", "home phone",
            "mobile phone", "mobile", "cell", "phone 1", "contact phone"],
        ["client.email"] = ["email", "e mail", "email address", "e mail address", "primary email"],
        ["client.county"] = ["county"],
        ["client.primaryLanguage"] = ["primary language", "preferred language", "language"],
        ["client.housingType"] = ["housing type", "housing", "housing status"],
        ["client.householdType"] = ["household type", "family type"],
        ["client.benefits.snapAmount"] = ["snap amount", "3squaresvt amount", "3 squares amount"],
        ["household[0].ssn"] = ["ssn", "social security number", "social security no", "social security", "soc sec", "ss number", "ss"],
        ["household[0].gender"] = ["gender", "gender identity", "sex"],
        ["household[0].race"] = ["race"],
        ["household[0].education"] = ["education", "education level", "last grade completed", "highest grade completed"],
        ["household[0].militaryStatus"] = ["military status", "veteran status"],
        ["household[0].healthInsurance"] = ["health insurance", "health insurance source", "insurance"],
        ["household[0].incomePerMonth"] = ["monthly income", "income per month", "gross monthly income", "total monthly income"],
    };

    /// <summary>Labels for the whole name ("Name: Smith, Jane"), used for first and last name when nothing better is found.</summary>
    public static readonly string[] FullNameLabels = ["client name", "applicant name", "full name", "head of household", "name"];

    /// <summary>
    /// Fills what it can; returns how many fields got a value. Only fields that are unticked and either blank or
    /// still holding an earlier reading are touched, so staff edits and ticks are never overwritten.
    /// </summary>
    public static int Fill(PaperImport import, IReadOnlyList<OcrPage> pages)
    {
        var filled = 0;
        pages = pages.Select(Rows).ToList();
        var allLabels = AllLabels();

        foreach (var field in import.Fields.Where(f => !f.Confirmed && (string.IsNullOrWhiteSpace(f.Value) || f.Value == f.ReadValue)))
        {
            var labels = LabelsFor(field);
            if (labels.Count == 0) continue;
            var found = Find(pages, labels, allLabels, field);
            if (found is null) continue;
            field.Value = found.Value.Value;
            field.ReadValue = found.Value.Value;
            field.Confidence = found.Value.Confidence;
            field.Spot = found.Value.Spot;
            filled++;
        }
        return filled;
    }

    private static List<string> AllLabels() => Labels.Values.SelectMany(a => a).Concat(FullNameLabels)
        .Concat(Model.IncomeExpenseCatalog.IncomeLines.Select(l => Norm(l.Label)))
        .Distinct().OrderByDescending(a => a.Length).ToList();

    /// <summary>
    /// The reader splits a row where there's a wide gap ("Last Name" … "Smith" in a table become two lines).
    /// Joins pieces that sit on the same row back into one line, left to right.
    /// </summary>
    public static OcrPage Rows(OcrPage page)
    {
        var rows = new List<List<OcrWord>>();
        foreach (var line in page.Lines.OrderBy(l => l.Y))
        {
            var mid = (line.Y + line.Bottom) / 2;
            var row = rows.FirstOrDefault(r =>
            {
                var top = r.Min(w => w.Y); var bottom = r.Max(w => w.Bottom);
                return mid > top && mid < bottom && Math.Abs((top + bottom) / 2 - mid) < (bottom - top) * 0.5;
            });
            if (row is null) rows.Add([.. line.Words]);
            else row.AddRange(line.Words);
        }
        return page with { Lines = rows.Select(r => new OcrLine(r.OrderBy(w => w.X).ToList())).ToList() };
    }

    private static List<string> LabelsFor(ImportField f)
    {
        if (f.Path is "client.lastName" or "client.firstName" && Labels.TryGetValue(f.Path, out var n))
            return [.. n.OrderByDescending(x => x.Length), .. FullNameLabels];
        if (Labels.TryGetValue(f.Path, out var l)) return [.. l.OrderByDescending(x => x.Length)];
        if (f.Path.StartsWith("incomeExpenses.income.", StringComparison.Ordinal)) return [Norm(f.Label)];
        return [];
    }

    private static (string Value, double Confidence, ScanSpot Spot)? Find(IReadOnlyList<OcrPage> pages, List<string> labels, List<string> allLabels, ImportField field)
    {
        (string Value, double Confidence, ScanSpot Spot)? best = null;
        foreach (var page in pages)
        for (var li = 0; li < page.Lines.Count; li++)
        {
            var line = page.Lines[li];
            var words = line.Words.Select(w => Norm(w.Text)).ToList();
            foreach (var label in labels)
            {
                var at = IndexOf(words, label.Split(' '));
                if (at < 0) continue;
                var fullName = FullNameLabels.Contains(label);
                if (label == "name" && at != 0) continue; // "Staff Name", "Vendor Name"… aren't the client
                var labelEnd = at + label.Split(' ').Length;
                // "Address" inside "Mailing Address" belongs to the other field
                if (allLabels.Any(other => other.Length > label.Length && !labels.Contains(other)
                        && IndexOf(words, other.Split(' ')) is var o && o >= 0 && o <= at && o + other.Split(' ').Length >= labelEnd))
                    continue;
                var labelWords = line.Words.Skip(at).Take(labelEnd - at).ToList();

                // candidates: rest of the line (up to the next label), then the line below, then above
                var candidates = new List<IReadOnlyList<OcrWord>>();
                var right = line.Words.Skip(labelEnd).ToList();
                var stop = NextLabelIndex(right.Select(w => Norm(w.Text)).ToList(), allLabels);
                if (stop >= 0) right = right.Take(stop).ToList();
                candidates.Add(right);
                candidates.Add(Near(page, li, labelWords, +1));
                candidates.Add(Near(page, li, labelWords, -1));

                foreach (var cand in candidates.Where(c => c.Count > 0))
                {
                    var raw = Clean(string.Join(' ', cand.Select(w => w.Text)));
                    if (raw.Length == 0) continue;
                    var (value, ok) = fullName ? NamePart(field, raw) : Shape(field, raw);
                    if (value.Length == 0) continue;
                    var confidence = ok ? 0.6 : 0.3;
                    var spot = new ScanSpot(page.File, page.Page, cand.Min(w => w.X), cand.Min(w => w.Y),
                        cand.Max(w => w.Right) - cand.Min(w => w.X), cand.Max(w => w.Bottom) - cand.Min(w => w.Y));
                    if (best is null || confidence > best.Value.Confidence) best = (value, confidence, spot);
                    if (ok) return best;
                    break; // first non-empty candidate for this label occurrence
                }
            }
        }
        return best;
    }

    /// <summary>Words on the nearest line above/below that sit under/over the label.</summary>
    private static IReadOnlyList<OcrWord> Near(OcrPage page, int lineIndex, List<OcrWord> label, int direction)
    {
        var line = page.Lines[lineIndex];
        var x0 = label.Min(w => w.X) - 0.05; var x1 = Math.Max(label.Max(w => w.Right) + 0.25, x0 + 0.3);
        var others = page.Lines.Where((l, i) => i != lineIndex)
            .Where(l => direction > 0 ? l.Y >= line.Bottom - line.Height * 0.3 : l.Bottom <= line.Y + line.Height * 0.3)
            .Where(l => Math.Abs((direction > 0 ? l.Y - line.Bottom : line.Y - l.Bottom)) < line.Height * 2.5)
            .OrderBy(l => Math.Abs(direction > 0 ? l.Y - line.Bottom : line.Y - l.Bottom));
        foreach (var l in others)
        {
            var ws = l.Words.Where(w => w.Right > x0 && w.X < x1).ToList();
            if (ws.Count > 0) return ws;
        }
        return [];
    }

    /// <summary>"Smith, Jane" or "Jane A Smith" → the first or last name, for a whole-name label.</summary>
    private static (string Value, bool Ok) NamePart(ImportField f, string raw)
    {
        string first, last;
        if (raw.Contains(','))
        {
            var parts = raw.Split(',', 2, StringSplitOptions.TrimEntries);
            last = parts[0]; first = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        }
        else
        {
            var words = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2) return ("", false);
            first = words[0]; last = words[^1];
        }
        var value = f.Path == "client.lastName" ? last : first;
        return (value, false); // split from a whole name: always worth a closer look
    }

    /// <summary>Pulls out the part that fits the field (a date, an SSN, a list choice…). Ok = it looks right.</summary>
    private static (string Value, bool Ok) Shape(ImportField f, string raw)
    {
        switch (f.Kind)
        {
            case ImportFieldKind.Date:
                var d = DatePattern().Match(raw);
                return d.Success && PaperImportRules.ParseDate(d.Value.Replace('.', '/').Replace('-', '/')) is { } date
                    ? (date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture), true) : (raw, false);
            case ImportFieldKind.Ssn:
                var s = SsnPattern().Match(raw);
                return s.Success ? (Ssn.Format(s.Value), true) : (raw, false);
            case ImportFieldKind.Phone:
                var p = PhonePattern().Match(raw);
                return p.Success ? (Phone.Format(p.Value), true) : (raw, false);
            case ImportFieldKind.Zip:
                var z = ZipPattern().Match(raw);
                return z.Success ? (z.Value, true) : (raw, false);
            case ImportFieldKind.Money:
                var m = MoneyPattern().Match(raw);
                return m.Success && PaperImportRules.ParseMoney(m.Value) is { } amount
                    ? (amount.ToString("0.00", CultureInfo.InvariantCulture), true) : ("", false);
            case ImportFieldKind.Choice when f.Options is { Count: > 0 } options:
                var hit = options.Where(o => Norm(raw).Contains(Norm(o), StringComparison.Ordinal))
                    .OrderByDescending(o => o.Length).FirstOrDefault();
                return hit is not null ? (hit, true) : (raw, false);
            case ImportFieldKind.Check:
                return ("", false);
            default:
                return (raw, raw.Any(char.IsLetterOrDigit));
        }
    }

    /// <summary>
    /// What the reader saw, laid out row by row with positions, but with names, numbers and other writing
    /// hidden (letters → x, digits → 9). Printed label words stay, so the layout can be shared to improve
    /// the reading without sharing anything about the client.
    /// </summary>
    public static string MaskedLayout(IReadOnlyList<OcrPage> pages)
    {
        var vocab = new HashSet<string>(AllLabels().SelectMany(l => l.Split(' ')).Concat(LayoutWords), StringComparer.Ordinal);
        var sb = new System.Text.StringBuilder();
        foreach (var page in pages)
        {
            sb.Append("== ").Append(Mask(page.File, vocab, keepExtension: true)).Append(" page ").Append(page.Page).AppendLine(" ==");
            foreach (var line in page.Lines.OrderBy(l => l.Y))
            {
                sb.Append(CultureInfo.InvariantCulture, $"y{line.Y:0.000} x{line.X:0.000}: ");
                sb.AppendLine(string.Join(' ', line.Words.Select(w => Mask(w.Text, vocab, keepExtension: false))));
            }
        }
        return sb.ToString();
    }

    /// <summary>Common printed form words kept in the shared layout (they say nothing about the client).</summary>
    private static readonly string[] LayoutWords =
    [
        "client", "applicant", "household", "member", "members", "information", "info", "profile", "contact", "date", "income",
        "source", "amount", "monthly", "yes", "no", "y", "n", "of", "the", "and", "or", "if", "type", "number", "status", "name",
        "relationship", "head", "age", "disabled", "veteran", "employment", "benefits", "snap", "wic", "liheap", "total", "page",
        "signature", "id", "case", "program", "programs", "enrollment", "intake", "assessment", "notes", "primary", "secondary",
    ];

    private static string Mask(string text, HashSet<string> vocab, bool keepExtension)
    {
        if (vocab.Contains(Norm(text))) return text;
        var ext = keepExtension ? Path.GetExtension(text) : "";
        var core = keepExtension ? text[..^ext.Length] : text;
        return new string(core.Select(c => char.IsDigit(c) ? '9' : char.IsLetter(c) ? 'x' : c).ToArray()) + ext;
    }

    private static int IndexOf(List<string> words, string[] label)
    {
        for (var i = 0; i + label.Length <= words.Count; i++)
            if (label.Select((w, k) => words[i + k] == w).All(x => x)) return i;
        return -1;
    }

    private static int NextLabelIndex(List<string> words, List<string> allLabels)
    {
        for (var i = 0; i < words.Count; i++)
            foreach (var label in allLabels)
                if (IndexOf(words.Skip(i).Take(label.Split(' ').Length).ToList(), label.Split(' ')) == 0) return i;
        return -1;
    }

    /// <summary>Lower case, letters/digits only (so "Name:" matches "name").</summary>
    public static string Norm(string s) =>
        string.Join(' ', NonWord().Replace(s.ToLowerInvariant(), " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Drops form lines and leftover punctuation around a value ("____ Smith ___" → "Smith").</summary>
    private static string Clean(string s) => s.Trim(' ', '_', ':', '.', '-', '|', '[', ']').Trim();

    [GeneratedRegex(@"[^a-z0-9]+")] private static partial Regex NonWord();
    [GeneratedRegex(@"\b\d{1,2}[/.\-]\d{1,2}[/.\-](\d{4}|\d{2})\b")] private static partial Regex DatePattern();
    [GeneratedRegex(@"\b\d{3}[\s\-]?\d{2}[\s\-]?\d{4}\b")] private static partial Regex SsnPattern();
    [GeneratedRegex(@"\(?\b\d{3}\)?[\s.\-]?\d{3}[\s.\-]?\d{4}\b")] private static partial Regex PhonePattern();
    [GeneratedRegex(@"\b\d{5}\b")] private static partial Regex ZipPattern();
    [GeneratedRegex(@"\$?\s?\d{1,3}(,\d{3})*(\.\d{2})?|\$?\s?\d+(\.\d{2})?")] private static partial Regex MoneyPattern();
}
