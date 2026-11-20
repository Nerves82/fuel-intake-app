using System.Globalization;

namespace FuelIntake.Core.Formatting;

/// <summary>Money as printed on the forms: $1,234.50.</summary>
public static class Money
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static string Format(decimal? amount) =>
        amount is null ? "" : amount.Value.ToString("$#,##0.00", Us);

    /// <summary>Sum of the amounts, or null when every amount is blank (so the form shows nothing).</summary>
    public static decimal? SumOrNull(params decimal?[] amounts) =>
        amounts.Any(a => a.HasValue) ? amounts.Sum(a => a ?? 0m) : null;
}

/// <summary>Percent as printed: 93%.</summary>
public static class Percent
{
    public static string Format(decimal? fraction) =>
        fraction is null ? "" : (fraction.Value * 100m).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
}
