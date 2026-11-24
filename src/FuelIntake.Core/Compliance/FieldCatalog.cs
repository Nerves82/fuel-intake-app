using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Compliance;

/// <summary>A field a compliance rule can point at.</summary>
/// <param name="Key">Its place in intake.json: "client.dateOfBirth", "household[*].ssn" (every member),
/// "incomeExpenses.income.SSA", "fuelElectric.fuelOrder.date".</param>
/// <param name="Tab">The app tab it's on, as the Review tab names them ("Client", "Household"…).</param>
/// <param name="IsCheck">A checkbox: "required" means it must be ticked.</param>
public sealed record CatalogField(string Key, string Label, string Group, string Tab, bool IsCheck);

/// <summary>
/// Every field of an intake, found from the record itself so new fields appear without extra work.
/// Leaves out bookkeeping (activity, notes, who created it, the test-data flag, staff contact copy).
/// </summary>
public static partial class FieldCatalog
{
    private static readonly Lazy<IReadOnlyList<CatalogField>> all = new(Build);
    public static IReadOnlyList<CatalogField> All => all.Value;

    public static CatalogField? Find(string key) => All.FirstOrDefault(f => f.Key == key);

    private static readonly HashSet<string> Skip =
    [
        "formatVersion", "isTestData", "createdBy", "createdAt", "activity", "notes", "staff.staffSnapshot",
        "application.complianceStandard",
    ];

    private static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ssn"] = "SSN", ["snap"] = "SNAP", ["wic"] = "WIC", ["liheap"] = "LIHEAP", ["aca"] = "ACA", ["sfb"] = "SFB",
        ["tefap"] = "TEFAP", ["aaa"] = "AAA", ["fpl"] = "FPL", ["vita"] = "VITA", ["neto"] = "NETO", ["tanf"] = "TANF", ["va"] = "VA",
    };

    private static IReadOnlyList<CatalogField> Build()
    {
        var list = new List<CatalogField>();
        Walk(typeof(IntakeRecord), "", "", list);
        return list;
    }

    private static void Walk(Type type, string prefix, string group, List<CatalogField> list)
    {
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite))
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(p.Name);
            var key = prefix.Length == 0 ? name : $"{prefix}.{name}";
            if (Skip.Contains(key)) continue;
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            var tab = TabFor(key);

            if (key == "household")
            {
                Walk(typeof(HouseholdMember), "household[*]", "Household (each member)", list);
            }
            else if (key == "incomeExpenses.income")
            {
                foreach (var l in IncomeExpenseCatalog.IncomeLines)
                    list.Add(new($"{key}.{l.Key}", l.Label, "Income", tab, false));
            }
            else if (key == "incomeExpenses.expenses")
            {
                foreach (var l in IncomeExpenseCatalog.ExpenseLines)
                {
                    list.Add(new($"{key}.{l.Key}.paidLast30Days", $"{l.Label}: paid in last 30 days", "Expenses", tab, false));
                    list.Add(new($"{key}.{l.Key}.bill", $"{l.Label}: bill", "Expenses", tab, false));
                }
            }
            else if (IsLeaf(t))
            {
                list.Add(new(key, Humanize(p.Name), group.Length == 0 ? Humanize(type.Name) : group, tab, t == typeof(bool)));
            }
            else if (typeof(IEnumerable).IsAssignableFrom(t))
            {
                list.Add(new(key, Humanize(p.Name), group, tab, false)); // a list: required = at least one entry
            }
            else if (t.IsClass)
            {
                var g = prefix.Length == 0 ? Top(key) : (group.Length == 0 ? "" : group + " › ") + Humanize(p.Name);
                Walk(t, key, g, list);
            }
        }
    }

    private static string Top(string key) => key switch
    {
        "incomeExpenses" => "Income & Expenses",
        "fuelElectric" => "Fuel & Electric",
        "stateReview" => "VT Eligibility Review (201SF)",
        _ => Humanize(key),
    };

    private static bool IsLeaf(Type t) =>
        t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateOnly) || t == typeof(TimeOnly)
        || t == typeof(DateTime) || t == typeof(DateTimeOffset);

    private static string TabFor(string key) => key.Split('.', '[')[0] switch
    {
        "staff" or "client" => "Client",
        "household" => "Household",
        "application" => "Application",
        "incomeExpenses" => "Income & Expenses",
        "fuelElectric" => "Fuel & Electric",
        "stateReview" => InputTabs.Vermont201SFTitle,
        _ => "Client",
    };

    /// <summary>"DateOfBirth" → "Date of birth", "SnapAmount" → "SNAP amount".</summary>
    public static string Humanize(string name)
    {
        var words = CamelWords().Matches(name).Select(m => m.Value).ToList();
        var text = string.Join(' ', words.Select((w, i) =>
            Words.TryGetValue(w, out var fixedWord) ? fixedWord : i == 0 ? char.ToUpperInvariant(w[0]) + w[1..] : w.ToLowerInvariant()));
        return text.Replace("Section", "").Trim();
    }

    [GeneratedRegex(@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+")]
    private static partial Regex CamelWords();
}
