namespace FuelIntake.Core.Mapping;

/// <summary>Housing type checkboxes on NEKCA Intake page 2.</summary>
public enum HousingBox { Own, Rent, HomelessTemporaryShelter, Other }

/// <summary>Household type checkboxes on NEKCA Intake page 2.</summary>
public enum HouseholdTypeBox
{
    SingleIndividual, TwoAdultsNoChildren, SingleParentFemale, SingleParentMale,
    TwoParentHousehold, NonRelatedAdultsWithChildren, MultigenerationalHousehold, Other,
}

/// <summary>Occupation checkboxes on NEKCA Intake page 2 (one row per member aged 14+).</summary>
public enum OccupationBox { EmployedFullTime, EmployedPartTime, Retired, Student, UnemployedUnder6Months, Unemployed6MonthsPlus, MigrantWorker }

/// <summary>
/// Which paper-form checkboxes a GoEngage value ticks. Ported from the workbook's HOUSING_MAP,
/// HHT_MAP and OCC_MAP. Matching is by exact GoEngage wording.
/// </summary>
public static class PaperMappings
{
    private static readonly Dictionary<HousingBox, string[]> Housing = new()
    {
        [HousingBox.Own] = ["Own"],
        [HousingBox.Rent] = ["Rent"],
        [HousingBox.HomelessTemporaryShelter] = ["Community Shelter", "Homeless", "Homeless No Housing", "Hotel/Motel Room", "Temporary Housing"],
        [HousingBox.Other] = ["Apartment", "House", "Migrant Housing", "Mobile Home/Trailer", "Other", "Prison", "Shared Housing by Choice"],
    };

    private static readonly Dictionary<HouseholdTypeBox, string[]> HouseholdType = new()
    {
        [HouseholdTypeBox.SingleIndividual] = ["Single Person"],
        [HouseholdTypeBox.TwoAdultsNoChildren] = ["Two Adults NO Children"],
        [HouseholdTypeBox.SingleParentFemale] = ["Single Parent Female"],
        [HouseholdTypeBox.SingleParentMale] = ["Single Parent Male"],
        [HouseholdTypeBox.TwoParentHousehold] = ["Two Parent Household"],
        [HouseholdTypeBox.NonRelatedAdultsWithChildren] = ["Non-related Adults with Children"],
        [HouseholdTypeBox.MultigenerationalHousehold] = ["Multigenerational Household"],
        [HouseholdTypeBox.Other] = ["Household with multiple adults with no children", "Foster Family/Foster Placement", "Other"],
    };

    // Combined values tick two boxes; Disabled, Paying job, Volunteer, In job training, Seasonal - Non-agricultural
    // and Other tick none; N/A means the member is under 14 and gets no occupation row at all.
    private static readonly Dictionary<OccupationBox, string[]> Occupation = new()
    {
        [OccupationBox.EmployedFullTime] = ["Employed Full-time", "Employed full-time and in school part-time"],
        [OccupationBox.EmployedPartTime] = ["Employed Part-time", "Employed part-time and in school part-time", "In school full-time and employed part-time"],
        [OccupationBox.Retired] = ["Retired"],
        [OccupationBox.Student] = ["School Full Time", "In school full-time and employed part-time", "Employed full-time and in school part-time", "Employed part-time and in school part-time"],
        [OccupationBox.UnemployedUnder6Months] = ["Unemployed"],
        [OccupationBox.Unemployed6MonthsPlus] = ["Unemployed (6 Months or More)"],
        [OccupationBox.MigrantWorker] = ["Seasonal - Agricultural"],
    };

    /// <summary>Occupation value meaning "no occupation row" (members under 14).</summary>
    public const string OccupationNotApplicable = "N/A";

    public static HousingBox? HousingBoxFor(string? goEngageValue) => Lookup(Housing, goEngageValue);

    public static HouseholdTypeBox? HouseholdTypeBoxFor(string? goEngageValue) => Lookup(HouseholdType, goEngageValue);

    public static IReadOnlySet<OccupationBox> OccupationBoxesFor(string? goEngageValue) =>
        Occupation.Where(kv => kv.Value.Contains(goEngageValue?.Trim())).Select(kv => kv.Key).ToHashSet();

    /// <summary>The GoEngage values that tick each box, for the "how is this mapped" help screen.</summary>
    public static IReadOnlyDictionary<HousingBox, string[]> HousingMap => Housing;
    public static IReadOnlyDictionary<HouseholdTypeBox, string[]> HouseholdTypeMap => HouseholdType;
    public static IReadOnlyDictionary<OccupationBox, string[]> OccupationMap => Occupation;

    private static TBox? Lookup<TBox>(Dictionary<TBox, string[]> map, string? value) where TBox : struct, Enum
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        foreach (var kv in map)
            if (kv.Value.Contains(v)) return kv.Key;
        return null;
    }
}
