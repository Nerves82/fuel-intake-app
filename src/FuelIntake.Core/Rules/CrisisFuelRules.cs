namespace FuelIntake.Core.Rules;

/// <summary>
/// One rule (or part of one) from the Vermont Crisis Fuel Assistance Rules, in plain words.
/// <see cref="Page"/> is the page number printed on the rules.
/// </summary>
public sealed record RuleRef(string Id, string Cite, string Title, string Summary, int Page);

/// <summary>
/// The Vermont DCF Crisis Fuel Assistance Rules 3100–3113 (effective 06/22/2024), summarised for quick reference while
/// filling in an intake. The summaries are NEKCA's plain-language notes, not the rules themselves: the official text
/// (<see cref="OfficialUrl"/>) governs. Update this list when DCF revises the rules.
/// </summary>
public static class CrisisFuelRules
{
    public const string Name = "Vermont Crisis Fuel Assistance Rules";
    public const string Effective = "06/22/2024";
    public const string OfficialUrl = "https://outside.vermont.gov/dept/DCF/Shared%20Documents/ESD/Rules/3100-Crisis-Fuel.pdf";

    public static readonly IReadOnlyList<RuleRef> All =
    [
        new("3100", "3100", "Crisis Fuel Assistance",
            "Crisis help for low-income households at risk of being without heat. The Seasonal Fuel rules (2900–2927) also apply where they don't conflict.", 1),
        new("3101-disabled", "3101(a)(2)", "\"Disabled\"",
            "Receives SSI, Social Security disability or blindness payments, an SSI supplement based on disability, disability retirement from a government agency, Railroad Retirement disability, or Vermont Medicaid because of a permanent disability.", 2),
        new("3101-elderly", "3101(a)(3)", "\"Elderly\"", "60 years of age or older.", 2),
        new("3101-household", "3101(a)(4)", "\"Fuel household\"",
            "Everyone who lives together as one economic unit and either buys heating fuel together or pays for heat through rent.", 2),
        new("3101-primary-fuel", "3101(a)", "\"Primary heating fuel\"", "The fuel that supplies the largest share of the home's heat.", 3),
        new("3102-residents", "3102(c)(1)", "Vermont residents", "Every household member must be a Vermont resident when applying.", 4),
        new("3102-citizen", "3102(c)(2)", "Citizenship", "A household member must be a U.S. citizen or national, or a qualified immigrant.", 4),
        new("3102-income", "3102(c)(3)", "Income limit",
            "Total gross income of all fuel household members must not exceed 200% of the federal poverty guidelines. Count only income actually received in the 30 days before the application; don't use income tax forms.", 4),
        new("3102-seasonal", "3102(c)(4)", "Seasonal fuel assistance",
            "The household must receive seasonal fuel assistance, or apply for it at the same time (or as soon as possible if circumstances prevent it), unless its income is over 185% of the poverty guidelines.", 4),
        new("3102-emergency", "3102(c)(5)", "Heating emergency",
            "There must be a home heating emergency: the main tank is 25% full or less, there's a week or less of wood, pellets or coal, or the utility that supplies the main heat (or runs the heating equipment) has issued a disconnection notice.", 5),
        new("3102-circumstances", "3102(c)(6)", "Unforeseen circumstances", "The household must show that unforeseen, extenuating circumstances kept it from heating the home.", 5),
        new("3102-heat-in-rent", "3102(d)", "Heat included in rent",
            "A household whose heat is included in the rent can qualify only if the landlord can't be reached to authorize more fuel, or a member's health or safety is threatened and no household member caused the emergency.", 5),
        new("3103", "3103", "Life-threatening heating crisis",
            "Imminent risk of being without heat, and a member can't move to a heated place because of a documented medical condition. Reasonable efforts to resolve it within 18 hours of finding the household eligible; medical documentation is due under 3107(h).", 6),
        new("3104-separate", "3104(a)", "One application per request", "Each crisis request needs its own application.", 7),
        new("3104-in-person", "3104(b)", "Applying in person",
            "Apply in person unless urgency, weather, illness, age or disability, or other circumstances make that unreasonable.", 7),
        new("3104-aaa", "3104(c)", "Area Agency on Aging", "Designated Area Agency on Aging staff may apply for an elderly or disabled person who can't apply in person or remotely.", 7),
        new("3104-recalculate", "3104(d)", "Recalculate each time", "Household income and size are recalculated for every application.", 7),
        new("3104-ssn", "3104(e)", "Social Security numbers", "Social Security numbers are required for all household members (Seasonal Fuel rule 2909).", 7),
        new("3104-identity", "3104(g), (j)", "Identity and immigration status",
            "New applicants must verify identity and immigration status; the application may be denied if they can't be verified.", 7),
        new("3104-season", "3104(k), (l)", "When applications are accepted",
            "Oil, kerosene, bulk propane, coal, firewood and pellets: last Monday in November to the second Friday in April. Electricity, natural gas and metered propane: last Monday in November to the last business day in April. DCF may change these dates (3104(n)).", 8),
        new("3104-supplier", "3104(m)", "Supplier and account information",
            "At application: the main heating fuel supplier's name and contact information, the supplier account number for the home, and the electricity provider's name and account number.", 8),
        new("3105", "3105", "After-hours assistance",
            "Only for households with no heat. A household helped after hours must submit a completed written application as soon as reasonably possible (3105(i)).", 9),
        new("3106", "3106", "Crisis fuel heating season", "The last Monday in November to April 30 (the Commissioner may change these dates).", 11),
        new("3107-income", "3107(a), (b)", "Verifying income",
            "Verify the income of everyone in the fuel household, unless it was verified in the past 30 days and nothing changed. A person's own statement of income isn't enough.", 12),
        new("3107-landlord", "3107(c)", "Landlord verification",
            "Needed when the crisis is about electric service (avoiding or reversing a disconnection), that service runs the heating system of a multi-family building, and the heating system is outside the applicant's unit.", 12),
        new("3107-deadline", "3107(d)", "Verification deadline",
            "All verification is due at application or within five calendar days of the application date (DCF may extend or waive it for a disaster, serious illness, an employer or landlord who can't provide it, and similar reasons).", 12),
        new("3107-life", "3107(h)", "Medical documentation", "For a life-threatening crisis, a licensed medical professional must document the medical condition within 30 calendar days of the application.", 13),
        new("3108-bulk", "3108(a)(1)", "Bulk fuel limit",
            "A minimum delivery of no more than 125 gallons of oil, propane, kerosene or other liquid fuel (less if the supplier allows a smaller minimum), one ton of coal or pellets, or one cord of wood.", 14),
        new("3108-metered", "3108(a)(2)", "Metered service limit",
            "With a disconnection notice for a metered service (gas, electricity, propane): no more than half the latest bill when it covers two months, the prior month's bill, or the minimum the utility needs to stop the disconnection. A service limiter counts as a shut-off notice.", 14),
        new("3108-other", "3108(a)(3)–(5)", "Start-up and other costs",
            "Heating unit start-up charges including pressure tests, utility charges needed to run the heating unit, and fuel in cans to keep heat going for now.", 14),
        new("3108-special-trip", "3108(b)", "Special trip",
            "For an empty tank, a special-trip charge may be paid if there's no other way to heat the home for now. The applicant signs a notice that it won't be paid again.", 14),
        new("3108-no-deposits", "3108(e)", "No deposits", "Deposits can't be authorized or paid for a household.", 15),
        new("3109-limits", "3109(b), (c)", "How many benefits per season",
            "A household receiving or eligible for seasonal fuel benefits can get one crisis benefit per heating season; a household eligible because its income is over 185% (up to 200%) of the poverty guidelines can get two.", 16),
        new("3110", "3110", "Referrals", "Refer applicants to services that help prevent future fuel emergencies; every household helped must accept weatherization services.", 17),
        new("3112", "3112", "Notice and appeal rights", "Give written notice of appeal rights whenever a decision affects benefits.", 19),
    ];

    public static RuleRef? Find(string? id) => All.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The rules that apply to a field, by its record path ("fuelElectric.fuelType", "household[2].ssn",
    /// "incomeExpenses.income.SSA"). Empty when none do.
    /// </summary>
    public static IReadOnlyList<RuleRef> ForField(string? path)
    {
        if (string.IsNullOrEmpty(path)) return [];
        var key = System.Text.RegularExpressions.Regex.Replace(path, @"\[\d+\]", "[*]");
        var ids = FieldRules.Where(f => f.Key.EndsWith('*') ? key.StartsWith(f.Key[..^1], StringComparison.Ordinal) : f.Key == key)
            .SelectMany(f => f.Value).Distinct();
        return ids.Select(Find).OfType<RuleRef>().ToList();
    }

    /// <summary>Field path (a trailing * matches everything under it) → rule ids.</summary>
    private static readonly Dictionary<string, string[]> FieldRules = new()
    {
        ["staff.appDate"] = ["3104-season", "3106", "3107-deadline"],
        ["household[*].ssn"] = ["3104-ssn"],
        ["household[*].incomeSource"] = ["3102-income", "3107-income"],
        ["household[*].incomePerMonth"] = ["3102-income", "3107-income"],
        ["household[*].disabled"] = ["3101-disabled"],
        ["client.dateOfBirth"] = ["3101-elderly"],
        ["household[*].dateOfBirth"] = ["3101-elderly"],
        ["incomeExpenses.income.*"] = ["3102-income", "3107-income"],
        ["application.crisisType"] = ["3102-emergency", "3108-bulk", "3108-metered"],
        ["application.currentQuantity"] = ["3102-emergency"],
        ["application.secondCrisis"] = ["3109-limits"],
        ["application.seasonalFuelStatus"] = ["3102-seasonal"],
        ["application.heatInRent"] = ["3102-heat-in-rent"],
        ["application.circumstance"] = ["3102-circumstances", "3102-heat-in-rent"],
        ["application.lifeThreatening"] = ["3103", "3107-life"],
        ["application.afterHours"] = ["3105"],
        ["application.aaaOnBehalf"] = ["3104-aaa"],
        ["application.phoneApplication"] = ["3104-in-person"],
        ["application.phoneApplicationReason"] = ["3104-in-person"],
        ["application.landlordVerification"] = ["3107-landlord"],
        ["application.electricForHeat"] = ["3107-landlord"],
        ["application.appealNotice"] = ["3112"],
        ["application.referrals.*"] = ["3110"],
        ["fuelElectric.fuelType"] = ["3101-primary-fuel", "3104-season", "3108-bulk"],
        ["fuelElectric.vendor"] = ["3104-supplier"],
        ["fuelElectric.vendorAccount"] = ["3104-supplier"],
        ["fuelElectric.electricProvider"] = ["3104-supplier"],
        ["fuelElectric.electricAccount"] = ["3104-supplier"],
        ["fuelElectric.fuelOrder.fuelRemaining"] = ["3102-emergency"],
        ["fuelElectric.fuelOrder.fuelLasts"] = ["3102-emergency"],
        ["fuelElectric.fuelOrder.gallons"] = ["3108-bulk"],
        ["fuelElectric.fuelOrder.fuel"] = ["3108-bulk"],
        ["fuelElectric.fuelOrder.specialTrip"] = ["3108-special-trip"],
        ["fuelElectric.fuelOrder.pressureTest"] = ["3108-other"],
        ["fuelElectric.fuelOrder.startUp"] = ["3108-other"],
        ["fuelElectric.electricPledge.disconnectNoticeAttached"] = ["3102-emergency", "3108-metered"],
        ["fuelElectric.electricPledge.amountRequired"] = ["3108-metered"],
    };

    // ---- dates (3104(k), (l))

    /// <summary>The application window for the heating season an application date falls in or near.</summary>
    /// <param name="metered">Electricity, natural gas or metered propane (open to the last business day in April).</param>
    public static (DateOnly Opens, DateOnly Closes) ApplicationWindow(DateOnly date, bool metered)
    {
        var startYear = date.Month >= 7 ? date.Year : date.Year - 1;
        var opens = LastWeekday(startYear, 11, DayOfWeek.Monday);
        var closes = metered ? LastBusinessDay(startYear + 1, 4) : NthWeekday(startYear + 1, 4, DayOfWeek.Friday, 2);
        return (opens, closes);
    }

    private static DateOnly LastWeekday(int year, int month, DayOfWeek day)
    {
        var d = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        while (d.DayOfWeek != day) d = d.AddDays(-1);
        return d;
    }

    private static DateOnly NthWeekday(int year, int month, DayOfWeek day, int n)
    {
        var d = new DateOnly(year, month, 1);
        while (d.DayOfWeek != day) d = d.AddDays(1);
        return d.AddDays(7 * (n - 1));
    }

    private static DateOnly LastBusinessDay(int year, int month)
    {
        var d = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(-1);
        return d;
    }
}
