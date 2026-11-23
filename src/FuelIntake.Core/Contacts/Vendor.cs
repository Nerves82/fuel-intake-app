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

    /// <summary>
    /// The one fuel this vendor sells (e.g. "Propane"), matched to the app's fuel types; null when it sells
    /// several, none, or something not on the list. Used to fill in the fuel type when the vendor is picked.
    /// </summary>
    public string? SingleFuelType
    {
        get
        {
            var fuels = (FuelOffered ?? "").Split([',', ';', '/', '\n', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(f => !f.Equals("Electric", StringComparison.OrdinalIgnoreCase))
                // "Propane", "Pellets", "Heating Oil", "Wood pellets" (last word, singular or plural)
                .Select(f => f.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "")
                .Select(last => Lists.ChoiceLists.FuelType.FirstOrDefault(t =>
                    last.Equals(t, StringComparison.OrdinalIgnoreCase) || last.Equals(t + "s", StringComparison.OrdinalIgnoreCase)))
                .Distinct().ToList();
            return fuels.Count == 1 ? fuels[0] : null;
        }
    }

    /// <summary>Phone numbers from the list, each 10-digit number formatted: "(802) 748-8934, (802) 748-1393".</summary>
    public static string FormatPhones(string? value) =>
        string.Join(", ", (value ?? "").Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => Formatting.Phone.IsValid(p) ? Formatting.Phone.Format(p) : p));

    /// <summary>Multi-line list values joined with ", " (as the forms print them).</summary>
    public static string JoinLines(string? value) =>
        string.Join(", ", (value ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

/// <summary>The vendor list with dropdown filters and lookup by display name.</summary>
public sealed class VendorDirectory(IEnumerable<Vendor> vendors)
{
    public IReadOnlyList<Vendor> Vendors { get; } = vendors.Where(v => !string.IsNullOrWhiteSpace(v.Name)).ToList();

    public static VendorDirectory Empty { get; } = new([]);

    /// <summary>Fuel vendor dropdown, A–Z.</summary>
    public IEnumerable<Vendor> FuelVendors => Vendors.Where(v => v.OffersFuel).OrderBy(v => v.DisplayName, StringComparer.CurrentCultureIgnoreCase);

    /// <summary>Electric provider dropdown, A–Z.</summary>
    public IEnumerable<Vendor> ElectricProviders => Vendors.Where(v => v.OffersElectric).OrderBy(v => v.DisplayName, StringComparer.CurrentCultureIgnoreCase);

    public Vendor? Find(string? displayName)
    {
        var d = displayName?.Trim();
        return string.IsNullOrEmpty(d) ? null
            : Vendors.FirstOrDefault(v => string.Equals(v.DisplayName, d, StringComparison.OrdinalIgnoreCase));
    }
}
