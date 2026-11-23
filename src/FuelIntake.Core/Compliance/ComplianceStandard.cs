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
            new() { Name = "Crisis Fuel", Description = "Applications paid from Crisis Fuel funds" },
        ],
    };
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
}
