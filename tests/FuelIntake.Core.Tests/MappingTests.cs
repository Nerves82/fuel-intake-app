using FuelIntake.Core.Lists;
using FuelIntake.Core.Mapping;

namespace FuelIntake.Core.Tests;

public class MappingTests
{
    [Theory]
    [InlineData("Own", HousingBox.Own)]
    [InlineData("Rent", HousingBox.Rent)]
    [InlineData("Hotel/Motel Room", HousingBox.HomelessTemporaryShelter)]
    [InlineData("Apartment", HousingBox.Other)]
    [InlineData("Mobile Home/Trailer", HousingBox.Other)]
    public void Housing(string value, HousingBox box) => Assert.Equal(box, PaperMappings.HousingBoxFor(value));

    [Fact]
    public void EveryHousingOption_TicksExactlyOneBox()
    {
        foreach (var v in ChoiceLists.Housing) Assert.NotNull(PaperMappings.HousingBoxFor(v));
    }

    [Fact]
    public void EveryHouseholdTypeOption_TicksABox()
    {
        foreach (var v in ChoiceLists.HouseholdType) Assert.NotNull(PaperMappings.HouseholdTypeBoxFor(v));
    }

    [Fact]
    public void CombinedOccupation_TicksTwoBoxes()
    {
        var boxes = PaperMappings.OccupationBoxesFor("In school full-time and employed part-time");
        Assert.Equal(new HashSet<OccupationBox> { OccupationBox.EmployedPartTime, OccupationBox.Student }, boxes);
    }

    [Theory]
    [InlineData("Disabled")]
    [InlineData("Paying job")]
    [InlineData("Volunteer")]
    [InlineData("N/A")]
    public void SomeOccupations_TickNothing(string value) => Assert.Empty(PaperMappings.OccupationBoxesFor(value));

    [Fact]
    public void EveryMappedValue_IsARealDropdownOption()
    {
        foreach (var vals in PaperMappings.HousingMap.Values) Assert.All(vals, v => Assert.Contains(v, ChoiceLists.Housing));
        foreach (var vals in PaperMappings.HouseholdTypeMap.Values) Assert.All(vals, v => Assert.Contains(v, ChoiceLists.HouseholdType));
        foreach (var vals in PaperMappings.OccupationMap.Values) Assert.All(vals, v => Assert.Contains(v, ChoiceLists.Occupation));
    }
}
