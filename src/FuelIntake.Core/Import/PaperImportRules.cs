using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FuelIntake.Core.Formatting;
using FuelIntake.Core.Model;
using FuelIntake.Core.Storage;

namespace FuelIntake.Core.Import;

public static partial class PaperImportRules
{
    public const string HoldingPrefix = "_PENDING ";
    public const string Checked = "Yes";

    // ---- progress and names

    public static int ConfirmedCount(PaperImport i) => i.Fields.Count(f => f.Confirmed);

    /// <summary>"Smith, Jane" once names are filled in, otherwise "Paper application from Nov 23".</summary>
    public static string DisplayName(PaperImport i)
    {
        var last = Value(i, "client.lastName"); var first = Value(i, "client.firstName");
        return last.Length > 0 || first.Length > 0
            ? $"{last}{(last.Length > 0 && first.Length > 0 ? ", " : "")}{first}"
            : $"Paper application from {i.CreatedAt.ToLocalTime():MMM d}";
    }

    public static string Value(PaperImport i, string path) =>
        i.Fields.FirstOrDefault(f => f.Path == path)?.Value?.Trim() ?? "";

    // ---- editing

    /// <summary>A changed value has to be confirmed again.</summary>
    public static void SetValue(ImportField f, string? value)
    {
        value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (value == f.Value) return;
        f.Value = value;
        f.Confirmed = false; f.ConfirmedBy = null; f.ConfirmedAt = null;
    }

    public static void SetConfirmed(ImportField f, bool confirmed, StaffStamp by, DateTimeOffset at)
    {
        f.Confirmed = confirmed;
        f.ConfirmedBy = confirmed ? by : null;
        f.ConfirmedAt = confirmed ? at : null;
    }

    /// <summary>Adds the fields for one more household member (after the last member's fields).</summary>
    public static void AddMember(PaperImport i)
    {
        i.MemberCount++;
        var fields = ImportFieldCatalog.MemberFields(i.MemberCount);
        var lastMember = i.Fields.FindLastIndex(f => f.Path.StartsWith("household[", StringComparison.Ordinal));
        i.Fields.InsertRange(lastMember < 0 ? i.Fields.Count : lastMember + 1, fields);
    }

    /// <summary>Removes the last household member's fields (never member 1).</summary>
    public static void RemoveLastMember(PaperImport i)
    {
        if (i.MemberCount <= 1) return;
        var prefix = $"household[{i.MemberCount - 1}].";
        i.Fields.RemoveAll(f => f.Path.StartsWith(prefix, StringComparison.Ordinal));
        i.MemberCount--;
    }

    // ---- checking values

    /// <summary>Why the value can't be used as it is (shown next to the field), or null.</summary>
    public static string? Problem(ImportField f, DateOnly today)
    {
        var v = f.Value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return f.Kind switch
        {
            ImportFieldKind.Date => ParseDate(v) is { } d ? (d > today ? "Date is in the future" : null) : "Not a date (use MM/DD/YYYY)",
            ImportFieldKind.Money => ParseMoney(v) is null ? "Not a dollar amount" : null,
            ImportFieldKind.Ssn => Ssn.IsValid(v) ? null : "SSN needs 9 digits",
            ImportFieldKind.Phone => Phone.IsValid(v) ? null : "Phone needs 10 digits",
            ImportFieldKind.Zip => Zip.IsValid(v) ? null : "Zip needs 5 digits",
            ImportFieldKind.Check => v == Checked ? null : "Ticked boxes are \"Yes\"",
            ImportFieldKind.Choice when f.Options is { Count: > 0 } o && !o.Contains(v, StringComparer.OrdinalIgnoreCase) => "Not one of the choices",
            _ => null,
        };
    }

    /// <summary>The client's name, needed to make the folder: both confirmed and filled in.</summary>
    public static (string First, string Last)? ConfirmedName(PaperImport i)
    {
        var first = i.Fields.FirstOrDefault(f => f.Path == "client.firstName");
        var last = i.Fields.FirstOrDefault(f => f.Path == "client.lastName");
        if (first is not { Confirmed: true, Value.Length: > 0 } || last is not { Confirmed: true, Value.Length: > 0 }) return null;
        return (first.Value!.Trim(), last.Value!.Trim());
    }

    /// <summary>Everything that stops "Create client": unconfirmed fields, values that can't be used, no name.</summary>
    public static IReadOnlyList<string> Blockers(PaperImport i, DateOnly today)
    {
        var b = new List<string>();
        var open = i.Fields.Count(f => !f.Confirmed);
        if (open > 0) b.Add($"{open} field{(open == 1 ? "" : "s")} still to check");
        var bad = i.Fields.Count(f => Problem(f, today) is not null);
        if (bad > 0) b.Add($"{bad} value{(bad == 1 ? "" : "s")} to fix");
        if (string.IsNullOrWhiteSpace(Value(i, "client.firstName")) || string.IsNullOrWhiteSpace(Value(i, "client.lastName")))
            b.Add("The client's first and last name are needed");
        return b;
    }

    // ---- making the intake

    /// <summary>
    /// The intake record from the confirmed fields: each value goes to its place in intake.json.
    /// Throws if anything is unconfirmed or can't be used, so a half-checked import can't become a client.
    /// </summary>
    public static IntakeRecord Build(PaperImport i, DateOnly today)
    {
        if (Blockers(i, today) is { Count: > 0 } blockers) throw new InvalidOperationException(string.Join("; ", blockers) + ".");
        var root = JsonNode.Parse(IntakeJson.Serialize(new IntakeRecord { Household = [] }))!.AsObject();
        foreach (var f in i.Fields)
        {
            var node = ToJson(f);
            if (node is null) continue;
            Set(root, f.Path, node);
        }
        var record = IntakeJson.Deserialize(root.ToJsonString());
        // the client is member 1 (their name and date of birth live on the client fields)
        while (record.Household.Count < i.MemberCount) record.Household.Add(new HouseholdMember());
        record.Staff.AppDate = today;
        return record;
    }

    private static JsonNode? ToJson(ImportField f)
    {
        var v = f.Value?.Trim();
        if (f.Kind == ImportFieldKind.Check) return JsonValue.Create(v == Checked);
        if (string.IsNullOrEmpty(v)) return null;
        return f.Kind switch
        {
            ImportFieldKind.Date => JsonValue.Create(ParseDate(v)!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ImportFieldKind.Money => JsonValue.Create(ParseMoney(v)!.Value),
            ImportFieldKind.Ssn => JsonValue.Create(Ssn.Format(v)),
            ImportFieldKind.Phone => JsonValue.Create(Phone.Format(v)),
            ImportFieldKind.Choice when f.Options is { Count: > 0 } o =>
                JsonValue.Create(o.First(x => string.Equals(x, v, StringComparison.OrdinalIgnoreCase))),
            _ => JsonValue.Create(v),
        };
    }

    /// <summary>Sets "a.b[2].c" in the JSON, making objects and array slots on the way.</summary>
    private static void Set(JsonObject root, string path, JsonNode value)
    {
        var parts = PathPart().Matches(path).Select(m => (Name: m.Groups[1].Value, Index: m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : (int?)null)).ToList();
        JsonNode current = root;
        for (var n = 0; n < parts.Count; n++)
        {
            var (name, index) = parts[n];
            var last = n == parts.Count - 1;
            var obj = current.AsObject();
            if (index is int ix)
            {
                if (obj[name] is not JsonArray arr) obj[name] = arr = [];
                while (arr.Count <= ix) arr.Add(new JsonObject());
                if (last) { arr[ix] = value; return; }
                current = arr[ix]!;
            }
            else
            {
                if (last) { obj[name] = value; return; }
                if (obj[name] is not JsonObject child) obj[name] = child = [];
                current = child;
            }
        }
    }

    public static DateOnly? ParseDate(string s)
    {
        string[] formats = ["M/d/yyyy", "M/d/yy", "M-d-yyyy", "M-d-yy", "yyyy-MM-dd", "MMddyyyy", "M.d.yyyy"];
        return DateOnly.TryParseExact(s.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    public static decimal? ParseMoney(string s) =>
        decimal.TryParse(s.Replace("$", "").Replace(",", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d >= 0
            ? Math.Round(d, 2) : null;

    [GeneratedRegex(@"([A-Za-z0-9_]+)(?:\[(\d+)\])?")]
    private static partial Regex PathPart();
}
