using FuelIntake.Core.Formatting;

namespace FuelIntake.Core.Contacts;

/// <summary>One row of the Staff list.</summary>
public sealed record StaffMember(string Name, string? Initials, string? Phone, string? Email);

/// <summary>The Staff list, with the lookups the forms need.</summary>
public sealed class StaffDirectory(IEnumerable<StaffMember> members)
{
    public IReadOnlyList<StaffMember> Members { get; } = members.Where(m => !string.IsNullOrWhiteSpace(m.Name)).ToList();

    public static StaffDirectory Empty { get; } = new([]);

    public StaffMember? FindByName(string? name)
    {
        var n = name?.Trim();
        return string.IsNullOrEmpty(n) ? null
            : Members.FirstOrDefault(m => string.Equals(m.Name.Trim(), n, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Match the signed-in user by work email (from Windows or Microsoft 365).</summary>
    public StaffMember? FindByEmail(string? email)
    {
        var e = email?.Trim();
        return string.IsNullOrEmpty(e) ? null
            : Members.FirstOrDefault(m => string.Equals(m.Email?.Trim(), e, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Initials when none are given: "Jane Smith" → JS, "Smith, Jane" → JS, "Jane" → J.
    /// Ported from the workbook's automatic initials formula.
    /// </summary>
    public static string AutoInitials(string? name)
    {
        var n = name?.Trim() ?? "";
        if (n.Length == 0) return "";
        var comma = n.IndexOf(',');
        if (comma >= 0)
        {
            var after = n[(comma + 1)..].Trim();
            return ((after.Length > 0 ? after[..1] : "") + n[..1]).ToUpperInvariant();
        }
        var words = n.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return (words[0][..1] + (words.Length > 1 ? words[^1][..1] : "")).ToUpperInvariant();
    }

    /// <summary>"⚠ Needs 10 digits" style check for the Staff list editor.</summary>
    public static bool PhoneLooksValid(StaffMember m) => string.IsNullOrWhiteSpace(m.Phone) || Phone.IsValid(m.Phone);
}
