namespace FuelIntake.Core.Lists;

/// <summary>
/// Dropdown options. Wording matches GoEngage exactly; several options tick paper checkboxes by
/// exact text (see <see cref="Mapping.PaperMappings"/>), so don't reword Disabled, Occupation,
/// Housing or Household type without updating the mappings.
/// </summary>
public static class ChoiceLists
{
    public static readonly IReadOnlyList<string> Housing =
    [
        "Apartment", "Community Shelter", "Homeless", "Homeless No Housing", "Hotel/Motel Room", "House",
        "Migrant Housing", "Mobile Home/Trailer", "Other", "Own", "Prison", "Rent", "Shared Housing by Choice",
        "Temporary Housing",
    ];

    public static readonly IReadOnlyList<string> HouseholdType =
    [
        "Single Person", "Two Parent Household", "Single Parent Female", "Single Parent Male",
        "Multigenerational Household", "Two Adults NO Children", "Non-related Adults with Children",
        "Household with multiple adults with no children", "Foster Family/Foster Placement", "Other",
    ];

    public static readonly IReadOnlyList<string> County = ["Caledonia", "Essex", "Orleans", "Out of County"];

    public static readonly IReadOnlyList<string> SchoolDistrict =
    [
        "Blue Mountain", "Caledonia Central", "Kingdom East School District", "NEK Choice",
        "North Country Supervisory Union", "Orleans Central", "St. Johnsbury",
    ];

    public static readonly IReadOnlyList<string> Gender = ["Female", "Male", "Non-binary", "Other"];

    public static readonly IReadOnlyList<string> YesNoLetter = ["Y", "N"];

    public static readonly IReadOnlyList<string> YesNo = ["Yes", "No"];

    public static readonly IReadOnlyList<string> Education =
    [
        "10th grade", "11th grade", "12th grade (no diploma)", "5th - 8th grade", "9th grade",
        "Associate's degree", "Bachelor's degree", "Career Ladder Level 1 Professional", "Doctorate degree",
        "High school graduate (high school diploma or equivalent)", "High School plus some college",
        "Less than or equal to 4th grade", "Master's degree", "No school completed", "Other",
        "Some college (but no degree)", "Unknown",
    ];

    public static readonly IReadOnlyList<string> Race =
    [
        "Hispanic or Latino", "Hispanic/Latino", "Middle Eastern or North African", "Two or more races", "Asian",
        "Multiracial and/or Multiethnic", "American Indian or Alaska Native", "Black or African American",
        "Native Hawaiian/Pacific Islander", "Other", "Refused", "Unknown", "White",
    ];

    public static readonly IReadOnlyList<string> Hispanic = ["Yes", "No", "N/A"];

    public static readonly IReadOnlyList<string> HealthInsurance =
    [
        "Direct-Purchase", "Employment Based", "Medicaid", "Medicare", "Military Health Care", "None",
        "State Children's Health Insurance Program", "State Health Insurance for Adults",
    ];

    public static readonly IReadOnlyList<string> IncomeSource =
    [
        "None", "Employment", "Self-employment", "Social Security Retirement (SSA)",
        "Social Security Disability (SSDI)", "Supplemental Security Income (SSI)", "Pension/Retirement",
        "Unemployment", "VA Disability", "Worker's Compensation", "Child Support", "Alimony/Spousal Support",
        "Reach Up (TANF)", "Other",
    ];

    public static readonly IReadOnlyList<string> Military = ["Active Military", "Never", "Unknown/not reported", "Veteran"];

    public static readonly IReadOnlyList<string> Occupation =
    [
        "Disabled", "Employed Full-time", "Employed full-time and in school part-time",
        "Employed Part-time", "Employed part-time and in school part-time", "In job training program",
        "In school full-time and employed part-time", "N/A", "Other", "Paying job", "Retired", "School Full Time",
        "Seasonal - Agricultural", "Seasonal - Non-agricultural", "Unemployed", "Unemployed (6 Months or More)",
        "Volunteer",
    ];

    public static readonly IReadOnlyList<string> SnapPeriod = ["month", "week", "year"];

    public static readonly IReadOnlyList<string> CrisisType = [CrisisTypes.LowOnFuel, CrisisTypes.DisconnectNotice];

    public static readonly IReadOnlyList<string> SeasonalFuelStatus = ["Active", "Pending", "Closed/Denied"];

    /// <summary>Citizenship or immigration status: the five boxes on the state's 201SF.</summary>
    public static readonly IReadOnlyList<string> Citizenship = [Citizenships.UsCitizen, Citizenships.Asylee, Citizenships.LegalImmigrant, Citizenships.Refugee, Citizenships.Other];

    public static readonly IReadOnlyList<string> FuelType = ["Oil", "Kerosene", "Propane", "Wood", "Pellet", "Electric", "Other"];
}

public static class Citizenships
{
    public const string UsCitizen = "U.S. citizen";
    public const string Asylee = "Asylee";
    public const string LegalImmigrant = "Legal immigrant";
    public const string Refugee = "Refugee";
    public const string Other = "Other";
}

/// <summary>Crisis type values (tab 3). The Application Forms Pack adds the Fuel Order or Electric Pledge by these.</summary>
public static class CrisisTypes
{
    public const string LowOnFuel = "Low on Fuel";
    public const string DisconnectNotice = "Disconnect Notice";
}
