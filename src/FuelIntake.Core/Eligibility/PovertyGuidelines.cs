namespace FuelIntake.Core.Eligibility;

/// <summary>
/// HHS poverty guidelines (100% FPL, dollars per year) for one year and region.
/// Updated each January when HHS publishes new figures; stored in agency settings so a new year
/// doesn't need a new build.
/// </summary>
/// <param name="Year">Guideline year.</param>
/// <param name="Region">e.g. "48 contiguous states and DC".</param>
/// <param name="BySize">Guideline for household sizes 1..N (index 0 = size 1).</param>
/// <param name="EachAdditionalPerson">Added per person above the last listed size.</param>
/// <param name="Source">Where the figures came from.</param>
public sealed record PovertyGuidelines(
    int Year,
    string Region,
    IReadOnlyList<decimal> BySize,
    decimal EachAdditionalPerson,
    string Source)
{
    /// <summary>
    /// 2026 HHS poverty guidelines, 48 contiguous states and DC (from the intake workbook's Settings tab).
    /// </summary>
    public static PovertyGuidelines Hhs2026 { get; } = new(
        2026,
        "48 contiguous states and DC",
        [15960m, 21640m, 27320m, 33000m, 38680m, 44360m, 50040m, 55720m],
        5680m,
        "HHS ASPE, 2026 Poverty Guidelines (detailed-guidelines-2026.pdf), https://aspe.hhs.gov/sites/default/files/documents/b1bfa16b20ae9b89d525bc35de7c1643/detailed-guidelines-2026.pdf");

    /// <summary>100% FPL in dollars per year for a household of <paramref name="householdSize"/>, or null if size &lt; 1.</summary>
    public decimal? AnnualGuideline(int householdSize)
    {
        if (householdSize < 1 || BySize.Count == 0) return null;
        if (householdSize <= BySize.Count) return BySize[householdSize - 1];
        return BySize[^1] + (householdSize - BySize.Count) * EachAdditionalPerson;
    }
}
