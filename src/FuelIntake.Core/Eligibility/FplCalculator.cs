namespace FuelIntake.Core.Eligibility;

/// <summary>Federal Poverty Level percentage, as on the Income/Expense worksheet.</summary>
public static class FplCalculator
{
    /// <summary>
    /// FPL as a fraction (1.5 = 150%): monthly household income × 12 ÷ annual guideline.
    /// Null when the household size is unknown.
    /// </summary>
    public static decimal? Percent(decimal monthlyIncome, int householdSize, PovertyGuidelines guidelines)
    {
        var line = guidelines.AnnualGuideline(householdSize);
        if (line is null or 0m) return null;
        return monthlyIncome * 12m / line.Value;
    }

    /// <summary>True when under the program limit (Crisis Fuel: under 200%, i.e. limit 2.0). Null if unknown.</summary>
    public static bool? IsUnderLimit(decimal? percent, decimal limit) =>
        percent is null ? null : percent.Value < limit;
}
