using System.Globalization;
using System.Text.RegularExpressions;
using FuelIntake.Core.Lists;
using FuelIntake.Core.Model;
using FuelIntake.Core.Rules;
using FuelIntake.Core.Validation;

namespace FuelIntake.Core.Compliance;

/// <summary>
/// A rule test a compliance standard can switch on (beyond "these fields are required"), e.g. Crisis Fuel's
/// 200% income limit. The tests are written here; standards only choose which apply (by <see cref="Id"/>).
/// </summary>
public sealed record ComplianceCheck(string Id, string Title, string RuleId, Func<IntakeRecord, IntakeCalculations, DateOnly, IEnumerable<CheckResult>> Run)
{
    public RuleRef? Rule => CrisisFuelRules.Find(RuleId);
}

/// <summary>A problem a check found (becomes a <see cref="ValidationIssue"/>).</summary>
public sealed record CheckResult(IssueSeverity Severity, string Section, string Field, string Message, string? Path = null);

public static class ComplianceChecks
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static readonly IReadOnlyList<ComplianceCheck> All =
    [
        new("cf-income-limit", "Income at or under 200% of the poverty guidelines", "3102-income", (r, c, _) =>
            c.FplPercent is > 2.0m
                ? [new(IssueSeverity.Error, "Income & Expenses", "FPL", $"Income is {Pct(c.FplPercent)} of the poverty guidelines; Crisis Fuel allows up to 200%.")]
                : []),

        new("cf-seasonal", "Receives or has applied for seasonal fuel (unless income is over 185%)", "3102-seasonal", (r, c, _) =>
            c.FplPercent is not > 1.85m && T(r.Application.SeasonalFuelStatus) is "" or "Closed/Denied"
                ? [new(IssueSeverity.Warning, "Application", "Seasonal fuel status",
                    "The household must receive seasonal fuel assistance or apply for it now (not needed when income is over 185% of the guidelines).",
                    "application.seasonalFuelStatus")]
                : []),

        new("cf-benefits-per-season", "No more crisis benefits than the season allows", "3109-limits", (r, c, _) =>
            r.Application.SecondCrisis && c.FplPercent is not > 1.85m
                ? [new(IssueSeverity.Error, "Application", "Second crisis",
                    "This is a second crisis benefit this season: only households with income over 185% (up to 200%) of the guidelines can get two.",
                    "application.secondCrisis")]
                : []),

        new("cf-citizenship", "A household member is a citizen, national or qualified immigrant", "3102-citizen", (r, c, _) =>
        {
            var known = c.Members.Where(m => m.IsPresent || m.Number == 1).Select(m => T(m.Data.Citizenship)).Where(x => x.Length > 0).ToList();
            return known.Count > 0 && known.All(x => x == Citizenships.Other)
                ? [new(IssueSeverity.Warning, "Household", "Citizenship",
                    "A household member must be a U.S. citizen or national, or a qualified immigrant; everyone recorded so far is \"Other\".")]
                : [];
        }),

        new("cf-application-season", "Application date within the crisis application season", "3104-season", (r, c, today) =>
        {
            var date = r.Staff.AppDate;
            if (date is null) return [];
            var metered = IsMetered(r);
            var (opens, closes) = CrisisFuelRules.ApplicationWindow(date.Value, metered);
            return date.Value < opens || date.Value > closes
                ? [new(IssueSeverity.Warning, "Client", "Application date",
                    $"{D(date.Value)} is outside the crisis application season for {(metered ? "electricity, natural gas or metered propane" : "bulk fuels")} " +
                    $"({D(opens)} to {D(closes)}), unless DCF has changed the dates.", "staff.appDate")]
                : [];
        }),

        new("cf-supplier", "Fuel supplier and account number given", "3104-supplier", (r, c, _) =>
        {
            // when electricity is the main heat, the electric provider is the fuel supplier
            if (T(r.FuelElectric.FuelType) == "Electric") return [];
            var list = new List<CheckResult>();
            if (T(r.FuelElectric.Vendor).Length == 0)
                list.Add(new(IssueSeverity.Error, "Fuel & Electric", "Fuel vendor", "Enter the main heating fuel supplier.", "fuelElectric.vendor"));
            if (T(r.FuelElectric.VendorAccount).Length == 0)
                list.Add(new(IssueSeverity.Error, "Fuel & Electric", "Vendor account #", "Enter the account number the fuel supplier has for this home.", "fuelElectric.vendorAccount"));
            return list;
        }),

        new("cf-bulk-limit", "Fuel order within the bulk delivery limit", "3108-bulk", (r, c, _) =>
        {
            var fuel = T(r.FuelElectric.FuelType);
            var gallons = Number(r.FuelElectric.FuelOrder.Gallons);
            return fuel is "Oil" or "Kerosene" or "Propane" && gallons is > 125m
                ? [new(IssueSeverity.Error, "Fuel & Electric", "Quantity (gallons)",
                    $"{gallons.Value.ToString("0.#", Us)} gallons is over the 125-gallon limit for {fuel.ToLowerInvariant()}.", "fuelElectric.fuelOrder.gallons")]
                : [];
        }),

        new("cf-emergency", "A heating emergency is shown (tank 25% or less, or a disconnect notice)", "3102-emergency", (r, c, _) =>
        {
            var crisis = T(r.Application.CrisisType);
            if (crisis == CrisisTypes.DisconnectNotice && !r.FuelElectric.ElectricPledge.DisconnectNoticeAttached)
                return [new(IssueSeverity.Warning, "Fuel & Electric", "Disconnect notice",
                    "Crisis type is a disconnect notice, but no notice is marked as attached (a service limiter counts as one).",
                    "fuelElectric.electricPledge.disconnectNoticeAttached")];
            if (crisis == CrisisTypes.LowOnFuel && T(r.FuelElectric.FuelType) is "Oil" or "Kerosene" or "Propane"
                && Fraction(r.FuelElectric.FuelOrder.FuelRemaining ?? r.Application.CurrentQuantity) is > 0.25m and var left)
                return [new(IssueSeverity.Warning, "Fuel & Electric", "Fuel remaining",
                    $"The tank is about {left:P0} full; a heating emergency is 25% full or less.",
                    r.FuelElectric.FuelOrder.FuelRemaining is null ? "application.currentQuantity" : "fuelElectric.fuelOrder.fuelRemaining")];
            return [];
        }),

        new("cf-circumstances", "Unforeseen circumstances described", "3102-circumstances", (r, c, _) =>
            T(r.Application.Circumstance).Length == 0
                ? [new(IssueSeverity.Warning, "Application", "Circumstance",
                    "Describe the unforeseen circumstances that kept the household from heating the home" +
                    (r.Application.HeatInRent ? " (heat is in the rent: say whether the landlord can't be reached or health or safety is threatened)." : "."),
                    "application.circumstance")]
                : []),

        new("cf-landlord", "Landlord verification when electricity runs a shared heating system", "3107-landlord", (r, c, _) =>
            T(r.Application.CrisisType) == CrisisTypes.DisconnectNotice && r.Application.ElectricForHeat
                && T(r.Client.HousingType) == "Apartment" && T(r.Application.LandlordVerification).Length == 0
                ? [new(IssueSeverity.Warning, "Application", "Landlord verification",
                    "Electric service runs the heat in a multi-family building: landlord verification is needed if the heating system is outside the unit.",
                    "application.landlordVerification")]
                : []),

        new("cf-referrals", "At least one referral made", "3110", (r, c, _) =>
        {
            var f = r.Application.Referrals;
            return f.Neto || f.AgencyOnAging || f.FinancialCoaching || f.CommunityConnections || f.WholeFamily || f.Vita
                ? []
                : [new(IssueSeverity.Warning, "Application", "Referrals", "Refer the household to services that help prevent future fuel emergencies (and weatherization).")];
        }),

        new("cf-reminders", "Reminders: verification due date, life-threatening, after-hours, special trip", "3107-deadline", (r, c, _) =>
        {
            var list = new List<CheckResult>();
            if (r.Staff.AppDate is { } d)
                list.Add(new(IssueSeverity.Info, "Client", "Application date", $"All verification is due by {D(d.AddDays(5))} (five calendar days).", "staff.appDate"));
            if (r.Application.LifeThreatening)
                list.Add(new(IssueSeverity.Info, "Application", "Life-threatening",
                    "Life-threatening crisis: resolve within 18 hours of finding the household eligible; medical documentation is due within 30 days.", "application.lifeThreatening"));
            if (r.Application.AfterHours)
                list.Add(new(IssueSeverity.Info, "Application", "After hours", "After-hours help: the household must turn in a completed written application as soon as possible.", "application.afterHours"));
            if (r.FuelElectric.FuelOrder.SpecialTrip is > 0)
                list.Add(new(IssueSeverity.Info, "Fuel & Electric", "Special trip",
                    "The applicant signs a notice that special-trip charges won't be paid again.", "fuelElectric.fuelOrder.specialTrip"));
            return list;
        }),
    ];

    /// <summary>The checks Crisis Fuel uses (rules 3100–3113).</summary>
    public static IReadOnlyList<string> CrisisFuel => All.Where(c => c.Id.StartsWith("cf-", StringComparison.Ordinal)).Select(c => c.Id).ToList();

    /// <summary>Fields rule 3104(e) and (m) require at application (the fuel supplier is a check: not needed when heat is electric).</summary>
    public static IReadOnlyList<string> CrisisFuelRequired =>
        ["household[*].ssn", "fuelElectric.electricProvider", "fuelElectric.electricAccount", "fuelElectric.fuelType",
         "application.crisisType", "staff.appDate"];

    public static ComplianceCheck? Find(string id) => All.FirstOrDefault(c => c.Id == id);

    /// <summary>Electricity, natural gas or metered propane (rule 3104(l)'s later closing date).</summary>
    public static bool IsMetered(IntakeRecord r) =>
        T(r.FuelElectric.FuelType) == "Electric" || r.Application.ElectricPropane
        || (T(r.FuelElectric.FuelType).Length == 0 && T(r.Application.CrisisType) == CrisisTypes.DisconnectNotice);

    /// <summary>"1/4", "1/8 tank", "30%", "0.2" → a fraction; null when there's no number.</summary>
    public static decimal? Fraction(string? text)
    {
        var s = T(text);
        if (Regex.Match(s, @"(\d+(?:\.\d+)?)\s*/\s*(\d+(?:\.\d+)?)") is { Success: true } f
            && decimal.TryParse(f.Groups[2].Value, NumberStyles.Number, Us, out var den) && den != 0)
            return decimal.Parse(f.Groups[1].Value, Us) / den;
        if (Regex.Match(s, @"(\d+(?:\.\d+)?)\s*%") is { Success: true } p) return decimal.Parse(p.Groups[1].Value, Us) / 100m;
        if (Regex.Match(s, @"^(0?\.\d+)$") is { Success: true } d) return decimal.Parse(d.Groups[1].Value, Us);
        return null;
    }

    private static decimal? Number(string? text) =>
        Regex.Match(T(text).Replace(",", ""), @"\d+(?:\.\d+)?") is { Success: true } m ? decimal.Parse(m.Value, Us) : null;

    private static string T(string? s) => s?.Trim() ?? "";
    private static string D(DateOnly d) => d.ToString("MMM d, yyyy", Us);
    private static string Pct(decimal? p) => p is { } v ? (v * 100).ToString("0.#", Us) + "%" : "?";
}
