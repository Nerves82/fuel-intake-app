using System.Text.RegularExpressions;

namespace FuelIntake.Core.Import;

/// <summary>Putting what was read off the app's own forms (box by box) on the approval page.</summary>
public static partial class ScanFieldReader
{
    /// <summary>The one blank on Intake page 1 that holds three fields.</summary>
    public const string CityStateZipPath = "client.cityStateZip";

    private static int FillFromForms(PaperImport import, IReadOnlyList<OcrPage> pages)
    {
        var filled = 0;
        foreach (var page in pages)
        {
            if (page.Form is not { } form) continue;

            // household rows with anything written in them become members (never removed)
            var rows = form.Values
                .Where(v => !string.IsNullOrWhiteSpace(v.Text) && MemberIndex(v.Zone.Path) is int)
                .Select(v => MemberIndex(v.Zone.Path)!.Value)
                .DefaultIfEmpty(-1).Max();
            while (import.MemberCount < rows + 1) PaperImportRules.AddMember(import);

            foreach (var v in form.Values.Where(v => v.Zone.Kind != ZoneKind.Tick))
            {
                if (v.Zone.Path == CityStateZipPath)
                {
                    var (city, state, zip) = SplitCityStateZip(v.Text ?? "");
                    filled += Put(import, "client.city", city, v) + Put(import, "client.state", state, v) + Put(import, "client.zip", zip, v);
                    continue;
                }
                filled += Put(import, v.Zone.Path, v.Text ?? "", v);
            }

            // ticks: a yes/no box on its own, or one choice out of several printed blanks
            var baseline = ScanFormReader.TickBaseline(form.Values);
            foreach (var group in form.Values.Where(v => v.Zone.Kind == ZoneKind.Tick).GroupBy(v => v.Zone.Path))
            {
                var field = Open(import, group.Key);
                if (field is null) continue;
                var marked = group.Where(v => ScanFormReader.IsMarked(v, baseline)).OrderByDescending(v => v.Ink).ToList();
                if (marked.Count == 0) { Clear(field); continue; }
                var top = marked[0];
                field.Value = field.Kind == ImportFieldKind.Check ? PaperImportRules.Checked : top.Zone.Value;
                field.ReadValue = field.Value;
                field.Confidence = marked.Count == 1 ? 0.8 : 0.4; // more than one blank marked: worth a look
                field.Spot = top.Spot;
                filled++;
            }
        }
        return filled;
    }

    private static int? MemberIndex(string path)
    {
        var m = MemberPath().Match(path);
        return m.Success ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    /// <summary>An unticked field still holding nothing but an earlier reading; null when staff have been at it.</summary>
    private static ImportField? Open(PaperImport import, string path)
    {
        var f = import.Fields.FirstOrDefault(x => x.Path == path);
        return f is null || f.Confirmed || !(string.IsNullOrWhiteSpace(f.Value) || f.Value == f.ReadValue) ? null : f;
    }

    private static void Clear(ImportField f)
    {
        if (string.IsNullOrWhiteSpace(f.ReadValue)) return;
        f.Value = null; f.ReadValue = null; f.Spot = null; f.Confidence = 0;
    }

    /// <summary>Puts one box's reading in its field. An empty box clears an earlier guess; staff work is never touched.</summary>
    private static int Put(PaperImport import, string path, string raw, ZoneValue v)
    {
        if (Open(import, path) is not { } field) return 0;
        raw = Clean(raw);
        if (raw.Length == 0) { Clear(field); return 0; }
        var (value, ok) = path == "client.state" ? (raw, true) : Shape(field, raw);
        var unmatchedChoice = value.Length == 0 && field.Kind == ImportFieldKind.Choice && raw.Any(char.IsLetter);
        if (value.Length == 0 && !unmatchedChoice) { FuzzyHit = false; Clear(field); return 0; }
        field.Value = value.Length == 0 ? null : value;
        field.ReadValue = value.Length == 0 ? raw : value;
        // the box is certain; the handwriting reading isn't
        field.Confidence = ok ? 0.7 : unmatchedChoice ? 0.1 : FuzzyHit ? 0.5 : 0.35;
        field.Spot = v.Spot;
        FuzzyHit = false;
        return 1;
    }

    /// <summary>"St. Johnsbury, VT 05819" → town, state, zip (each blank when it isn't there).</summary>
    public static (string City, string State, string Zip) SplitCityStateZip(string raw)
    {
        raw = Clean(raw);
        var zip = ZipAtEnd().Match(raw);
        if (!zip.Success)
        {
            // "Barton VT o5822": the zip with look-alike letters
            var lastSpace = raw.LastIndexOf(' ');
            var fixedRaw = lastSpace < 0 ? raw : raw[..(lastSpace + 1)] + Digits(raw[(lastSpace + 1)..]);
            if (ZipAtEnd().Match(fixedRaw) is { Success: true } z2) { raw = fixedRaw; zip = z2; }
        }
        var rest = zip.Success ? raw[..zip.Index].TrimEnd(' ', ',') : raw;
        var state = "";
        var words = rest.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).ToList();
        if (words.Count > 0 && StateCode(words[^1]) is { } code)
        {
            state = code;
            rest = rest[..rest.LastIndexOf(words[^1], StringComparison.Ordinal)].TrimEnd(' ', ',');
        }
        return (rest.Trim(' ', ','), state, zip.Success ? zip.Groups[1].Value : "");
    }

    [GeneratedRegex(@"^household\[(\d+)\]\.")] private static partial Regex MemberPath();
    [GeneratedRegex(@"[\s,]*(\d{5})(-\d{4})?\s*$")] private static partial Regex ZipAtEnd();
}
