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
        ["client.lastName"] = ["last name", "client last name", "applicant last name", "surname"],
        ["client.firstName"] = ["first name", "client first name", "applicant first name", "given name"],
        ["client.dateOfBirth"] = ["date of birth", "dob", "birth date", "birthdate"],
        ["client.addressPhysical"] = ["physical address", "street address", "home address", "address"],
        ["client.city"] = ["town", "city", "city town", "town city"],
        ["client.state"] = ["state"],
        ["client.zip"] = ["zip", "zip code", "zipcode", "postal code"],
        ["client.addressMailing"] = ["mailing address"],
        ["client.phone"] = ["phone", "phone number", "telephone", "primary phone", "cell phone", "home phone"],
        ["client.email"] = ["email", "e mail", "email address"],
        ["client.county"] = ["county"],
        ["client.primaryLanguage"] = ["primary language", "language"],
        ["client.housingType"] = ["housing type", "housing"],
        ["client.householdType"] = ["household type"],
        ["client.benefits.snapAmount"] = ["snap amount", "3squaresvt amount", "3 squares amount"],
        ["household[0].ssn"] = ["ssn", "social security number", "social security", "soc sec", "ss"],
        ["household[0].gender"] = ["gender", "gender identity", "sex"],
        ["household[0].race"] = ["race"],
        ["household[0].incomePerMonth"] = ["monthly income", "income per month", "gross monthly income"],
    };

    /// <summary>Fills what it can; returns how many fields got a value.</summary>
    public static int Fill(PaperImport import, IReadOnlyList<OcrPage> pages)
    {
        var filled = 0;
        var allLabels = Labels.Values.SelectMany(a => a)
            .Concat(Model.IncomeExpenseCatalog.IncomeLines.Select(l => Norm(l.Label)))
            .Distinct().OrderByDescending(a => a.Length).ToList();

        foreach (var field in import.Fields.Where(f => !f.Confirmed && string.IsNullOrWhiteSpace(f.Value)))
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

    private static List<string> LabelsFor(ImportField f)
    {
        if (Labels.TryGetValue(f.Path, out var l)) return [.. l];
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
            foreach (var label in labels.OrderByDescending(l => l.Length))
            {
                var at = IndexOf(words, label.Split(' '));
                if (at < 0) continue;
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
                    var (value, ok) = Shape(field, raw);
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
