using System.Security.Cryptography;
using System.Text.Json.Nodes;
using FuelIntake.Core.Model;
using FuelIntake.Core.Storage;

namespace FuelIntake.Core.Compliance;

/// <summary>A required field that isn't filled in.</summary>
public sealed record MissingField(CatalogField Field, int? Member);

public static class ComplianceRules
{
    /// <summary>
    /// The standard an application is held to: the one picked on the Review tab, otherwise the one linked to
    /// the Fuel Order's or Electric Pledge's grant source. Null when none applies.
    /// </summary>
    public static ComplianceStandard? Effective(IntakeRecord r, ComplianceSettings c)
    {
        var picked = r.Application.ComplianceStandard?.Trim();
        if (!string.IsNullOrEmpty(picked))
            return c.Standards.FirstOrDefault(s => string.Equals(s.Name.Trim(), picked, StringComparison.OrdinalIgnoreCase));
        return FromGrant(r, c);
    }

    /// <summary>The standard linked to the order's or pledge's grant source, if any.</summary>
    public static ComplianceStandard? FromGrant(IntakeRecord r, ComplianceSettings c)
    {
        string?[] grants = [r.FuelElectric.FuelOrder.FundingSource, r.FuelElectric.ElectricPledge.FundingSource, r.FuelElectric.Invoice.FundingSource];
        foreach (var g in grants.Where(g => !string.IsNullOrWhiteSpace(g)))
        {
            var s = c.Standards.FirstOrDefault(s => s.GrantSources.Any(x => string.Equals(x.Trim(), g!.Trim(), StringComparison.OrdinalIgnoreCase))
                                                    || string.Equals(s.Name.Trim(), g!.Trim(), StringComparison.OrdinalIgnoreCase));
            if (s is not null) return s;
        }
        return null;
    }

    /// <summary>Required fields that are empty (a checkbox must be ticked; "every member" fields check each member).</summary>
    public static IReadOnlyList<MissingField> Missing(IntakeRecord r, ComplianceStandard standard)
    {
        var root = JsonNode.Parse(IntakeJson.Serialize(r));
        var missing = new List<MissingField>();
        foreach (var key in standard.Required.Distinct())
        {
            if (FieldCatalog.Find(key) is not { } field) continue;
            if (key.StartsWith("household[*].", StringComparison.Ordinal))
            {
                var name = key["household[*].".Length..];
                for (var m = 0; m < r.Household.Count; m++)
                {
                    // member 1 is the client: their name and date of birth are on the client fields
                    if (m == 0 && name is "firstName" or "lastName" or "dateOfBirth") continue;
                    if (IsEmpty(root?["household"]?[m]?[name], field)) missing.Add(new(field, m + 1));
                }
            }
            else
            {
                JsonNode? node = root;
                foreach (var part in key.Split('.')) node = node?[part];
                if (IsEmpty(node, field)) missing.Add(new(field, null));
            }
        }
        return missing;
    }

    private static bool IsEmpty(JsonNode? node, CatalogField field) => node switch
    {
        null => true,
        JsonArray a => a.Count == 0,
        JsonValue v when v.TryGetValue<bool>(out var b) => field.IsCheck && !b,
        JsonValue v when v.TryGetValue<string>(out var s) => string.IsNullOrWhiteSpace(s),
        _ => false,
    };

    // ---- passcode

    public static bool HasPasscode(ComplianceSettings c) => !string.IsNullOrEmpty(c.PasscodeHash);

    public static void SetPasscode(ComplianceSettings c, string passcode)
    {
        if (passcode.Trim().Length < 4) throw new ArgumentException("Use at least 4 characters.");
        var salt = RandomNumberGenerator.GetBytes(16);
        c.PasscodeSalt = Convert.ToBase64String(salt);
        c.PasscodeHash = Convert.ToBase64String(Hash(passcode.Trim(), salt));
    }

    public static bool CheckPasscode(ComplianceSettings c, string? passcode)
    {
        if (!HasPasscode(c) || string.IsNullOrWhiteSpace(passcode) || c.PasscodeSalt is null) return false;
        try
        {
            var expected = Convert.FromBase64String(c.PasscodeHash!);
            return CryptographicOperations.FixedTimeEquals(expected, Hash(passcode.Trim(), Convert.FromBase64String(c.PasscodeSalt)));
        }
        catch (FormatException) { return false; }
    }

    private static byte[] Hash(string passcode, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(passcode, salt, 100_000, HashAlgorithmName.SHA256, 32);
}

/// <summary>The compliance standard shown beside a client in the clients list.</summary>
/// <param name="Name">The standard's name.</param>
/// <param name="FromGrant">True when it comes from the grant source rather than being picked.</param>
/// <param name="Unknown">True when the picked name is no longer in the compliance settings.</param>
public sealed record ComplianceBadge(string Name, bool FromGrant, bool Unknown)
{
    public string Tooltip => Unknown ? $"{Name} isn't in the compliance settings any more"
        : FromGrant ? $"{Name}, from the grant source" : $"Compliance standard: {Name}";

    /// <summary>The badge for a record: the picked standard, else the one from the grant source, else none.</summary>
    public static ComplianceBadge? For(IntakeRecord? r, ComplianceSettings c)
    {
        if (r is null) return null;
        var picked = r.Application.ComplianceStandard?.Trim();
        if (!string.IsNullOrEmpty(picked))
        {
            var s = ComplianceRules.Effective(r, c);
            return s is null ? new(picked, false, true) : new(s.Name, false, false);
        }
        return ComplianceRules.FromGrant(r, c) is { } g ? new(g.Name, true, false) : null;
    }

    /// <summary>
    /// The standard a new client starts under: the name as it's spelled in the settings, or null for none
    /// (blank, or not one of the standards).
    /// </summary>
    public static string? ForNewClient(string? chosen, ComplianceSettings c)
    {
        var name = chosen?.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        return c.Standards.FirstOrDefault(s => string.Equals(s.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))?.Name;
    }
}
