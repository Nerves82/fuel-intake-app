using System.Collections;
using System.Reflection;
using System.Text.Json;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Validation;

/// <summary>
/// Where each object of an intake sits in intake.json ("client", "household[2]", "incomeExpenses.expenses.Rent"),
/// so a screen field bound to an object's property can tell its path ("household[2].ssn") and find the
/// validation issues and compliance rules about it. Paths use the same keys as <see cref="Compliance.FieldCatalog"/>.
/// </summary>
public sealed class RecordPaths
{
    private readonly Dictionary<object, string> paths = new(ReferenceEqualityComparer.Instance);

    public RecordPaths(IntakeRecord record) => Walk(record, "");

    /// <summary>The path of a property of an object in the record, e.g. ("household[2]" object, "Ssn") → "household[2].ssn".</summary>
    public string? PathOf(object? target, string propertyName)
    {
        if (target is null || !paths.TryGetValue(target, out var prefix)) return null;
        var name = JsonNamingPolicy.CamelCase.ConvertName(propertyName);
        return prefix.Length == 0 ? name : $"{prefix}.{name}";
    }

    /// <summary>The path of a dictionary entry, e.g. (income dictionary, "SSA") → "incomeExpenses.income.SSA".</summary>
    public string? PathOfEntry(object? dictionary, object? key) =>
        dictionary is not null && key is not null && paths.TryGetValue(dictionary, out var prefix) ? $"{prefix}.{key}" : null;

    /// <summary>Same field? "household[*].ssn" (a rule about every member) matches "household[2].ssn".</summary>
    public static bool Matches(string issuePath, string fieldPath) =>
        string.Equals(issuePath, fieldPath, StringComparison.Ordinal)
        || (issuePath.Contains("[*]") && System.Text.RegularExpressions.Regex.Replace(fieldPath, @"\[\d+\]", "[*]") == issuePath);

    private void Walk(object obj, string path)
    {
        if (!paths.TryAdd(obj, path)) return;
        if (obj is IDictionary dict)
        {
            foreach (DictionaryEntry e in dict)
                if (e.Value is { } v && IsNode(v.GetType())) Walk(v, $"{path}.{e.Key}");
            return;
        }
        if (obj is IList list)
        {
            for (var i = 0; i < list.Count; i++)
                if (list[i] is { } v && IsNode(v.GetType())) Walk(v, $"{path}[{i}]");
            return;
        }
        foreach (var p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0 || !IsNode(p.PropertyType)) continue;
            if (p.GetValue(obj) is not { } child) continue;
            var name = JsonNamingPolicy.CamelCase.ConvertName(p.Name);
            Walk(child, path.Length == 0 ? name : $"{path}.{name}");
        }
    }

    // records, sections, lists and dictionaries of the model; not strings, values, or the activity log's entries
    private static bool IsNode(Type t) =>
        t != typeof(string) && !t.IsValueType && (t.Namespace?.StartsWith("FuelIntake.Core.Model", StringComparison.Ordinal) == true
            || typeof(IDictionary).IsAssignableFrom(t) || (typeof(IList).IsAssignableFrom(t) && t.IsGenericType
                && t.GetGenericArguments()[0].Namespace?.StartsWith("FuelIntake.Core.Model", StringComparison.Ordinal) == true));
}
