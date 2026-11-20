using FuelIntake.Core.Model;
using FuelIntake.Core.Validation;

namespace FuelIntake.Core.Tests;

public class GrantSourceTests
{
    private static readonly List<GrantSource> List =
    [
        new() { Name = "Example Crisis Fund", Code = "TEST-01", MaxPerAssist = 500m },
        new() { Name = "Example Electric Fund", ForFuel = false, MaxPerAssist = 300m },
        new() { Name = "Old Example Grant", Active = false },
    ];

    private static IReadOnlyList<ValidationIssue> Check(IntakeRecord r, List<GrantSource> grants)
    {
        var ctx = FakeIntake.Context();
        ctx = ctx with { Settings = new AgencySettings { GrantSources = grants } };
        return IntakeValidator.Validate(r, new IntakeCalculations(r, ctx), FakeIntake.Today);
    }

    [Fact]
    public void For_OffersActiveSourcesForTheRightUse()
    {
        Assert.Equal(["Example Crisis Fund"], GrantSources.For(List, GrantUse.Fuel).Select(g => g.Name));
        Assert.Equal(["Example Crisis Fund", "Example Electric Fund"], GrantSources.For(List, GrantUse.Electric).Select(g => g.Name));
        Assert.Equal("TEST-01", GrantSources.Find(List, "  example crisis FUND ")?.Code);
        Assert.Null(GrantSources.Find(List, "Something else"));
    }

    [Fact]
    public void ListProblems_SpotsDuplicatesAndMissingUse()
    {
        Assert.Empty(GrantSources.ListProblems(List));
        var bad = new List<GrantSource> { new() { Name = "A" }, new() { Name = " a " }, new() { Name = "B", ForFuel = false, ForElectric = false } };
        Assert.Equal(2, GrantSources.ListProblems(bad).Count);
    }

    [Fact]
    public void Validator_WarnsOverLimit_WrongUse_AndMissing()
    {
        var r = FakeIntake.Family();
        var o = r.FuelElectric.FuelOrder;
        o.Authorized = true;
        o.Fuel = 600m;
        Assert.Contains(Check(r, List), i => i.Field == "Fuel Order funding source" && i.Message.StartsWith("Pick"));

        o.FundingSource = "example crisis fund";
        Assert.Contains(Check(r, List), i => i.Field == "Fuel Order funding source" && i.Message.Contains("over the Example Crisis Fund limit"));

        o.Fuel = 450m;
        Assert.DoesNotContain(Check(r, List), i => i.Field == "Fuel Order funding source");

        o.FundingSource = "Example Electric Fund";
        Assert.Contains(Check(r, List), i => i.Field == "Fuel Order funding source" && i.Message.Contains("isn't set up for fuel"));

        o.FundingSource = "Typed by hand";
        Assert.Contains(Check(r, List), i => i.Field == "Fuel Order funding source" && i.Severity == IssueSeverity.Info);
    }

    [Fact]
    public void Validator_SaysNothing_WhenTheAgencyHasNoList()
    {
        var r = FakeIntake.Family();
        r.FuelElectric.FuelOrder.Authorized = true;
        Assert.DoesNotContain(Check(r, []), i => i.Field.EndsWith("funding source"));
        r.FuelElectric.FuelOrder.FundingSource = "Anything";
        Assert.DoesNotContain(Check(r, []), i => i.Field.EndsWith("funding source"));
    }
}
