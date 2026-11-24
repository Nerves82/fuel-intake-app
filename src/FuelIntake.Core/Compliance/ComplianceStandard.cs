namespace FuelIntake.Core.Compliance;

/// <summary>
/// Agency-wide compliance standards (e.g. "Warmth", "Crisis Fuel"), kept in compliance.json in the
/// shared "_Fuel Intake Settings" folder. Editing them in the app needs the passcode.
/// Phase 2: a SharePoint list with permissions instead of a passcode.
/// </summary>
public sealed class ComplianceSettings
{
    public List<ComplianceStandard> Standards { get; set; } = [];

    /// <summary>PBKDF2-SHA256 of the passcode, and its salt (base64). Null until a passcode is set.</summary>
    public string? PasscodeHash { get; set; }
    public string? PasscodeSalt { get; set; }

    /// <summary>The two standards NEKCA starts with; nothing is required until someone with the passcode sets it.</summary>
    public static ComplianceSettings Defaults() => new()
    {
        Standards =
        [
            new() { Name = "Warmth", Description = "Applications paid from Warmth funds" },
            CrisisFuel(),
            Vermont201SF(),
        ],
    };

    /// <summary>Crisis Fuel with the Vermont Crisis Fuel Assistance Rules (3100–3113) switched on.</summary>
    public static ComplianceStandard CrisisFuel()
    {
        var s = new ComplianceStandard { Name = "Crisis Fuel", Description = "Applications paid from Crisis Fuel funds (Vermont Crisis Fuel Assistance Rules 3100–3113)" };
        s.AddCrisisFuelRules();
        return s;
    }

    public const string Vermont201SFName = "Vermont Fuel Assistance (201SF)";

    /// <summary>
    /// Built in: what the state's 201SF Eligibility Review needs from the intake, so the printed form isn't missing
    /// answers the app could have filled in. Agencies whose settings were saved before it existed add it from Settings.
    /// </summary>
    public static ComplianceStandard Vermont201SF() => new()
    {
        Name = Vermont201SFName,
        Description = "Everything the Vermont Fuel Assistance Eligibility Review (DCF form 201SF) asks that the intake can fill in",
        Required =
        [
            "client.firstName", "client.lastName", "client.dateOfBirth", "client.addressPhysical", "client.city", "client.zip",
            "client.phone", "client.housingType",
            "household[*].firstName", "household[*].lastName", "household[*].dateOfBirth", "household[*].ssn", "household[*].citizenship",
            "fuelElectric.fuelType", "fuelElectric.vendor", "fuelElectric.vendorAccount",
            "fuelElectric.electricProvider", "fuelElectric.electricAccount",
            "staff.staffName",
        ],
    };

    /// <summary>True when a standard with the built-in 201SF standard's name is already in the list.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Has201SF => Standards.Any(x => string.Equals(x.Name.Trim(), Vermont201SFName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>One standard: which fields must be filled in for an application under it.</summary>
public sealed class ComplianceStandard
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>Grant/funding sources that put an application under this standard automatically.</summary>
    public List<string> GrantSources { get; set; } = [];

    /// <summary>Field keys (see <see cref="FieldCatalog"/>) that must be filled in.</summary>
    public List<string> Required { get; set; } = [];

    /// <summary>Rule tests that apply (ids from <see cref="ComplianceChecks"/>), e.g. Crisis Fuel's income limit.</summary>
    public List<string> Checks { get; set; } = [];

    /// <summary>Adds the Crisis Fuel rules (3100–3113): their checks and the fields rule 3104 requires. Keeps what's there.</summary>
    public void AddCrisisFuelRules()
    {
        foreach (var k in ComplianceChecks.CrisisFuelRequired) if (!Required.Contains(k)) Required.Add(k);
        foreach (var k in ComplianceChecks.CrisisFuel) if (!Checks.Contains(k)) Checks.Add(k);
    }

    /// <summary>True when any Crisis Fuel rule check is switched on: the intake then shows the Crisis Fuel rule help.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool UsesCrisisFuelRules => Checks.Any(ComplianceChecks.CrisisFuel.Contains);

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasAllCrisisFuelRules =>
        ComplianceChecks.CrisisFuel.All(Checks.Contains) && ComplianceChecks.CrisisFuelRequired.All(Required.Contains);
}
