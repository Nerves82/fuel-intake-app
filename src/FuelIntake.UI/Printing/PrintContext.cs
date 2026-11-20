using System.Globalization;
using FuelIntake.Core.Formatting;
using FuelIntake.Core.Model;

namespace FuelIntake.UI.Printing;

/// <summary>Everything a printed form needs, plus the formatting used on paper.</summary>
public sealed record PrintContext(IntakeRecord R, IntakeCalculations C, AgencySettings Settings, DateOnly Today)
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    /// <summary>11/20/2026</summary>
    public static string D(DateOnly? d) => d?.ToString("MM/dd/yyyy", Us) ?? "";

    /// <summary>2:30 PM</summary>
    public static string T(TimeOnly? t) => t?.ToString("h:mm tt", Us) ?? "";

    /// <summary>1,234.50 (for lines already labelled "$")</summary>
    public static string M(decimal? m) => m?.ToString("#,##0.00", Us) ?? "";

    /// <summary>$1,234.50</summary>
    public static string MD(decimal? m) => Money.Format(m);

    public static string Ph(string? p) => Phone.Format(p);

    public static string Ssn(string? s) => FuelIntake.Core.Formatting.Ssn.Format(s);

    /// <summary>The application date, used beside signatures.</summary>
    public string AppDate => D(R.Staff.AppDate);
}
