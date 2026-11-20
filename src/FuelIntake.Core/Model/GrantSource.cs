namespace FuelIntake.Core.Model;

/// <summary>
/// A grant or funding source staff pick on the Fuel Order, invoice and Electric Pledge.
/// Kept in Agency settings (settings.json); phase 2 moves it to a Microsoft List.
/// </summary>
public sealed class GrantSource
{
    /// <summary>Printed on the forms as the Grant/Funding Source.</summary>
    public string Name { get; set; } = "";

    /// <summary>Grant or account code, for the agency's own reference (shown in the app, not printed).</summary>
    public string? Code { get; set; }

    public bool ForFuel { get; set; } = true;
    public bool ForElectric { get; set; } = true;

    /// <summary>Most this source pays per Fuel Order or Electric Pledge; blank = no limit.</summary>
    public decimal? MaxPerAssist { get; set; }

    /// <summary>Unticked sources stay on old files but aren't offered for new ones.</summary>
    public bool Active { get; set; } = true;

    public string? Notes { get; set; }
}

public enum GrantUse { Fuel, Electric }

public static class GrantSources
{
    /// <summary>Active sources for a Fuel Order/invoice or an Electric Pledge, by name.</summary>
    public static IReadOnlyList<GrantSource> For(IEnumerable<GrantSource> all, GrantUse use) =>
        all.Where(g => g.Active && !string.IsNullOrWhiteSpace(g.Name) && (use == GrantUse.Fuel ? g.ForFuel : g.ForElectric))
           .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
           .ToList();

    /// <summary>The source with this name (any status), ignoring case and spacing; null if not on the list.</summary>
    public static GrantSource? Find(IEnumerable<GrantSource> all, string? name) =>
        string.IsNullOrWhiteSpace(name) ? null
            : all.FirstOrDefault(g => string.Equals(g.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Problems with the list itself, for the settings page (blank or repeated names).</summary>
    public static IReadOnlyList<string> ListProblems(IEnumerable<GrantSource> all)
    {
        var list = all.ToList();
        var problems = new List<string>();
        if (list.Any(g => string.IsNullOrWhiteSpace(g.Name) && (g.Code is { Length: > 0 } || g.MaxPerAssist is not null || g.Notes is { Length: > 0 })))
            problems.Add("Every grant source needs a name.");
        foreach (var dup in list.Where(g => !string.IsNullOrWhiteSpace(g.Name))
                     .GroupBy(g => g.Name.Trim(), StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
            problems.Add($"\"{dup.Key}\" is on the list more than once.");
        if (list.Any(g => g.MaxPerAssist < 0)) problems.Add("A maximum can't be negative.");
        if (list.Any(g => !string.IsNullOrWhiteSpace(g.Name) && !g.ForFuel && !g.ForElectric))
            problems.Add("Tick Fuel, Electric or both for each grant source.");
        return problems;
    }
}
