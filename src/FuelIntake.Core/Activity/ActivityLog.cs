using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FuelIntake.Core.Model;
using FuelIntake.Core.Storage;

namespace FuelIntake.Core.Activity;

/// <summary>One changed field: a readable name and the values before and after (SSNs masked).</summary>
public sealed record FieldChange(string Field, string? From, string? To);

/// <summary>A batch of changes by one person. Edits by the same person within a short time are merged into one entry.</summary>
public sealed class ActivityEntry
{
    public DateTimeOffset At { get; set; }
    public StaffStamp By { get; set; } = new("", null);
    /// <summary>"Created" or "Edited".</summary>
    public string Action { get; set; } = "Edited";
    public List<FieldChange> Changes { get; set; } = [];
}

/// <summary>Works out what changed between two saves and records it on the intake.</summary>
public static partial class ActivityLog
{
    /// <summary>Edits by the same person within this window are merged into one entry.</summary>
    public static readonly TimeSpan MergeWindow = TimeSpan.FromMinutes(15);

    /// <summary>Paths that are bookkeeping, not client data.</summary>
    private static readonly string[] Ignored = ["activity", "createdBy", "createdAt", "formatVersion", "staff.staffSnapshot"];

    /// <summary>The record as JSON without its history, used as the "before" snapshot.</summary>
    public static string Snapshot(IntakeRecord r)
    {
        var node = JsonNode.Parse(IntakeJson.Serialize(r))!.AsObject();
        node.Remove("activity");
        return node.ToJsonString();
    }

    /// <summary>Field-level differences between two snapshots, in a stable order.</summary>
    public static List<FieldChange> Diff(string before, string after)
    {
        var a = Flatten(JsonNode.Parse(before));
        var b = Flatten(JsonNode.Parse(after));
        var changes = new List<FieldChange>();
        foreach (var key in a.Keys.Union(b.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            if (Ignored.Any(i => key == i || key.StartsWith(i + ".", StringComparison.Ordinal) || key.StartsWith(i + "[", StringComparison.Ordinal))) continue;
            a.TryGetValue(key, out var from);
            b.TryGetValue(key, out var to);
            if (Equivalent(from, to)) continue;
            var sensitive = key.EndsWith("ssn", StringComparison.OrdinalIgnoreCase);
            changes.Add(new FieldChange(Describe(key), sensitive ? Mask(from) : from, sensitive ? Mask(to) : to));
        }
        return changes;
    }

    /// <summary>Adds a change entry for <paramref name="by"/>, merging into their previous entry if it was recent.</summary>
    public static void Record(IntakeRecord r, StaffStamp by, DateTimeOffset at, IReadOnlyList<FieldChange> changes)
    {
        if (changes.Count == 0) return;
        var last = r.Activity.LastOrDefault();
        if (last is not null && last.Action == "Edited" && SamePerson(last.By, by) && at - last.At < MergeWindow)
        {
            foreach (var c in changes)
            {
                var i = last.Changes.FindIndex(x => x.Field == c.Field);
                if (i < 0) last.Changes.Add(c);
                else if (Equivalent(last.Changes[i].From, c.To)) last.Changes.RemoveAt(i); // changed back: no net change
                else last.Changes[i] = last.Changes[i] with { To = c.To };
            }
            last.At = at;
            if (last.Changes.Count == 0) r.Activity.Remove(last);
            return;
        }
        r.Activity.Add(new ActivityEntry { At = at, By = by, Action = "Edited", Changes = changes.ToList() });
    }

    public static void RecordCreated(IntakeRecord r, StaffStamp by, DateTimeOffset at)
    {
        r.CreatedBy ??= by;
        r.CreatedAt ??= at;
        r.Activity.Add(new ActivityEntry { At = at, By = by, Action = "Created" });
    }

    public static bool SamePerson(StaffStamp a, StaffStamp b) =>
        !string.IsNullOrWhiteSpace(a.Email) && !string.IsNullOrWhiteSpace(b.Email)
            ? string.Equals(a.Email, b.Email, StringComparison.OrdinalIgnoreCase)
            : string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>"household[1].incomePerMonth" → "Household member 2 › Income per month".</summary>
    public static string Describe(string path)
    {
        var parts = path.Split('.');
        var words = new List<string>();
        for (var p = 0; p < parts.Length; p++)
        {
            var raw = parts[p];
            // income/expense lines: use the worksheet labels
            if (p > 0 && parts[p - 1] == "income")
            {
                words.Add(IncomeExpenseCatalog.IncomeLines.FirstOrDefault(l => l.Key == raw)?.Label ?? raw);
                continue;
            }
            if (p > 0 && parts[p - 1] == "expenses")
            {
                words.Add(IncomeExpenseCatalog.ExpenseLines.FirstOrDefault(l => l.Key == raw)?.Label ?? raw);
                continue;
            }
            var m = IndexPart().Match(raw);
            if (m.Success && m.Groups[1].Value == "household")
            {
                words.Add($"Household member {int.Parse(m.Groups[2].Value) + 1}");
                continue;
            }
            var name = m.Success ? m.Groups[1].Value : raw;
            if (name is "client" or "staff" or "application" or "fuelElectric" or "incomeExpenses" && words.Count == 0)
            {
                words.Add(name switch
                {
                    "client" => "Client", "staff" => "Staff", "application" => "Application",
                    "fuelElectric" => "Fuel & Electric", _ => "Income & Expenses",
                });
                continue;
            }
            words.Add(Words(name));
        }
        return string.Join(" › ", words);
    }

    private static string Words(string camel)
    {
        var s = SplitWords().Replace(camel, " $1").Trim();
        return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant()
            .Replace("ssn", "SSN").Replace("snap", "SNAP").Replace("sfb", "SFB").Replace("wic", "WIC").Replace("aca", "ACA");
    }

    private static string? Mask(string? ssn) =>
        string.IsNullOrEmpty(ssn) ? ssn : Formatting.Ssn.Masked(ssn);

    private static bool Equivalent(string? a, string? b) =>
        (string.IsNullOrEmpty(a) || a == "false") && (string.IsNullOrEmpty(b) || b == "false") || a == b;

    private static Dictionary<string, string?> Flatten(JsonNode? node)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        void Walk(JsonNode? n, string path)
        {
            switch (n)
            {
                case JsonObject o:
                    foreach (var (k, v) in o) Walk(v, path.Length == 0 ? k : $"{path}.{k}");
                    break;
                case JsonArray arr:
                    for (var i = 0; i < arr.Count; i++) Walk(arr[i], $"{path}[{i}]");
                    break;
                case null:
                    map[path] = null;
                    break;
                default:
                    map[path] = n.ToString();
                    break;
            }
        }
        Walk(node, "");
        return map;
    }

    [GeneratedRegex(@"^(\w+)\[(\d+)\]$")] private static partial Regex IndexPart();
    [GeneratedRegex("([A-Z])")] private static partial Regex SplitWords();
}
