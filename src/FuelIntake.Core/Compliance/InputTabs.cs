namespace FuelIntake.Core.Compliance;

/// <summary>An input tab a compliance standard can add to the intake (beyond tabs 1–5).</summary>
public sealed record InputTab(string Id, string Title, string Description);

/// <summary>The extra input tabs there are. A standard lists the ones it adds in <see cref="ComplianceStandard.ExtraTabs"/>.</summary>
public static class InputTabs
{
    public const string Vermont201SF = "vt-201sf";
    public const string Vermont201SFTitle = "VT Eligibility Review";

    public static readonly IReadOnlyList<InputTab> All =
    [
        new(Vermont201SF, Vermont201SFTitle,
            "What the state's Vermont Fuel Assistance Eligibility Review (DCF form 201SF) asks that the intake doesn't: relationships, roomers and caregivers, bedrooms, jobs and pay dates…"),
    ];

    public static InputTab? Find(string? id) => All.FirstOrDefault(t => t.Id == id);

    /// <summary>The extra tabs for an application: those of its compliance standard (picked, or from the grant source).</summary>
    public static IReadOnlyList<InputTab> For(Model.IntakeRecord r, ComplianceSettings c) =>
        ComplianceRules.Effective(r, c) is { } s ? s.ExtraTabs.Select(Find).OfType<InputTab>().ToList() : [];
}
