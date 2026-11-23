namespace FuelIntake.Core.Model;

/// <summary>Giving household members (tab 2, member 2 and up) the client's last name.</summary>
public static class HouseholdNames
{
    /// <summary>The client's last name, trimmed; null when blank.</summary>
    public static string? ClientLast(IntakeRecord r) => string.IsNullOrWhiteSpace(r.Client.LastName) ? null : r.Client.LastName.Trim();

    /// <summary>Members 2+ whose last name is blank or isn't the client's (ignoring case and spacing).</summary>
    public static IReadOnlyList<HouseholdMember> Different(IntakeRecord r)
    {
        if (ClientLast(r) is not { } last) return [];
        return r.Household.Skip(1).Where(m => !Same(m.LastName, last)).ToList();
    }

    public static bool Same(string? a, string? b) =>
        string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sets the client's last name on <paramref name="members"/> and returns what each had before,
    /// so the change can be undone with <see cref="Undo"/>.
    /// </summary>
    public static IReadOnlyList<(HouseholdMember Member, string? Before)> Apply(IntakeRecord r, IEnumerable<HouseholdMember> members)
    {
        if (ClientLast(r) is not { } last) return [];
        var changed = new List<(HouseholdMember, string?)>();
        foreach (var m in members.Where(m => !ReferenceEquals(m, r.Household.FirstOrDefault())).ToList())
        {
            if (m.LastName == last) continue;
            changed.Add((m, m.LastName));
            m.LastName = last;
        }
        return changed;
    }

    public static void Undo(IEnumerable<(HouseholdMember Member, string? Before)> changes)
    {
        foreach (var (m, before) in changes) m.LastName = before;
    }

    private static string Norm(string? s) => string.Join(' ', (s ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
