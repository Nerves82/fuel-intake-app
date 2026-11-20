namespace FuelIntake.Core.Contacts;

/// <summary>
/// One row of the "Fuel Team Vendor List" (Microsoft List on the 3025 Crisis Fuel site).
/// Covers fuel vendors and electric providers ("Electric" in Fuel Offered).
/// </summary>
public sealed record Vendor(
    string Name,
    string? Location = null,
    string? ContactEmails = null,
    string? ContactPhones = null,
    string? FuelOffered = null,
    string? MinimumDelivery = null,
    string? EmergencyTripFee = null,
    string? PressureTestFee = null,
    string? Notes = null)
{
    /// <summary>What the dropdown shows and stores: "CN Brown - St. Johnsbury" (two CN Brown rows stay distinct).</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Location) ? Name.Trim() : $"{Name.Trim()} - {Location.Trim()}";

    /// <summary>Offers electric (an electric provider).</summary>
    public bool OffersElectric => FuelOffered?.Contains("Electric", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Belongs in the fuel vendor dropdown: anything except electric-only. Blank Fuel Offered counts as fuel.</summary>
    public bool OffersFuel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FuelOffered)) return true;
            var rest = FuelOffered.Replace("Electric", "", StringComparison.OrdinalIgnoreCase)
                .Replace(",", "").Replace(";", "").Replace(" ", "");
            return rest.Length > 0;
        }
    }

    /// <summary>Multi-line list values joined with ", " (as the forms print them).</summary>
    public static string JoinLines(string? value) =>
        string.Join(", ", (value ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

/// <summary>The vendor list with dropdown filters and lookup by display name.</summary>
public sealed class VendorDirectory(IEnumerable<Vendor> vendors)
{
    public IReadOnlyList<Vendor> Vendors { get; } = vendors.Where(v => !string.IsNullOrWhiteSpace(v.Name)).ToList();

    public static VendorDirectory Empty { get; } = new([]);

    public IEnumerable<Vendor> FuelVendors => Vendors.Where(v => v.OffersFuel);

    public IEnumerable<Vendor> ElectricProviders => Vendors.Where(v => v.OffersElectric);

    public Vendor? Find(string? displayName)
    {
        var d = displayName?.Trim();
        return string.IsNullOrEmpty(d) ? null
            : Vendors.FirstOrDefault(v => string.Equals(v.DisplayName, d, StringComparison.OrdinalIgnoreCase));
    }
}
