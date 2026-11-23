using FuelIntake.Core.Model;

namespace FuelIntake.Core.Tests;

public class HouseholdNamesTests
{
    [Fact]
    public void Different_ListsBlankAndOtherNames_NotTheClient()
    {
        var r = FuelIntake.Core.TestData.SampleIntakes.FamilyDisconnect("Jane", "Smith", FakeIntake.Today);
        r.Household[1].LastName = null;
        r.Household[2].LastName = " smith ";
        r.Household[3].LastName = "Example";
        Assert.Equal([r.Household[1], r.Household[3]], HouseholdNames.Different(r));

        r.Client.LastName = "  ";
        Assert.Empty(HouseholdNames.Different(r));
    }

    [Fact]
    public void Apply_SetsTheName_AndUndoPutsItBack()
    {
        var r = FuelIntake.Core.TestData.SampleIntakes.FamilyDisconnect("Jane", "Smith", FakeIntake.Today);
        r.Household[1].LastName = null;
        r.Household[3].LastName = "Example";

        var changes = HouseholdNames.Apply(r, HouseholdNames.Different(r));
        Assert.Equal(2, changes.Count);
        Assert.All(r.Household.Skip(1), m => Assert.Equal("Smith", m.LastName));

        HouseholdNames.Undo(changes);
        Assert.Null(r.Household[1].LastName);
        Assert.Equal("Example", r.Household[3].LastName);
        Assert.Equal("Smith", r.Household[2].LastName);
    }
}
