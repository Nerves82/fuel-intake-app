using FuelIntake.Core.Compliance;
using FuelIntake.Core.Model;
using FuelIntake.Core.Validation;

namespace FuelIntake.Core.Tests;

public class FieldIssueTests
{
    private static IReadOnlyList<ValidationIssue> Validate(IntakeRecord r, ComplianceSettings? c = null) =>
        IntakeValidator.Validate(r, new IntakeCalculations(r, FakeIntake.Context() with { Compliance = c }), FakeIntake.Today);

    [Fact]
    public void Paths_FindEveryPartOfTheRecord()
    {
        var r = FakeIntake.Family();
        var p = new RecordPaths(r);
        Assert.Equal("client.zip", p.PathOf(r.Client, nameof(ClientSection.Zip)));
        Assert.Equal("client.benefits.snapAmount", p.PathOf(r.Client.Benefits, nameof(BenefitsReceived.SnapAmount)));
        Assert.Equal("household[2].ssn", p.PathOf(r.Household[2], nameof(HouseholdMember.Ssn)));
        Assert.Equal("fuelElectric.fuelOrder.fundingSource", p.PathOf(r.FuelElectric.FuelOrder, nameof(FuelOrder.FundingSource)));
        Assert.Equal("incomeExpenses.income.Earned", p.PathOfEntry(r.IncomeExpenses.Income, "Earned"));
        Assert.Equal("incomeExpenses.expenses.Rent.bill", p.PathOf(r.IncomeExpenses.Expenses["Rent"], nameof(ExpenseAmounts.Bill)));
        Assert.Null(p.PathOf(new HouseholdMember(), "Ssn")); // not part of this record
    }

    [Fact]
    public void Paths_UseTheComplianceKeys()
    {
        var r = FakeIntake.Family();
        var p = new RecordPaths(r);
        // every catalog key resolves for some object in a record (member rules match any member)
        Assert.True(FieldCatalog.Find("household[*].ssn") is not null);
        Assert.True(RecordPaths.Matches("household[*].ssn", p.PathOf(r.Household[1], "Ssn")!));
        Assert.False(RecordPaths.Matches("household[*].ssn", "household[1].dateOfBirth"));
        Assert.True(RecordPaths.Matches("client.zip", "client.zip"));
    }

    [Fact]
    public void Issues_SayWhichFieldTheyAreAbout()
    {
        var r = FakeIntake.Family();
        r.Client.Zip = "581";
        r.Client.Phone = "555";
        r.Household[2].Ssn = "12";
        r.Application.CrisisType = null;
        r.Household[1].IncomeSource = "Other";
        var issues = Validate(r);
        string? PathFor(string text) => issues.First(i => i.Message.Contains(text)).Path;
        Assert.Equal("client.zip", PathFor("Zip code"));
        Assert.Equal("client.phone", PathFor("Phone number"));
        Assert.Equal("household[2].ssn", PathFor("SSN must be 9 digits"));
        Assert.Equal("application.crisisType", PathFor("crisis type"));
        Assert.Equal("household[1].incomeSourceOther", PathFor("Income source is \"Other\""));
    }

    [Fact]
    public void ComplianceIssues_PointAtTheMembersField()
    {
        var c = ComplianceSettings.Defaults();
        c.Standards[0].Required = ["household[*].ssn", "client.email"];
        var r = FakeIntake.Family();
        r.Application.ComplianceStandard = "Warmth";
        r.Client.Email = null;
        var paths = Validate(r, c).Where(i => i.Message.StartsWith("Required")).Select(i => i.Path).ToList();
        Assert.Contains("client.email", paths);
        Assert.Contains("household[1].ssn", paths);   // member 2 has no SSN
        Assert.Contains("household[2].ssn", paths);
        Assert.DoesNotContain("household[0].ssn", paths); // the client's is filled in
    }

    [Fact]
    public void Standard201SF_IsBuiltIn_AndOnlyUsesRealFields()
    {
        var c = ComplianceSettings.Defaults();
        Assert.True(c.Has201SF);
        var s = c.Standards.Single(x => x.Name == ComplianceSettings.Vermont201SFName);
        Assert.All(s.Required, key => Assert.NotNull(FieldCatalog.Find(key)));
        Assert.Empty(s.GrantSources); // applies when picked, never by grant source
        Assert.False(new ComplianceSettings { Standards = [new() { Name = "Warmth" }] }.Has201SF);
    }

    [Fact]
    public void Standard201SF_FlagsWhatTheFormWouldBeMissing()
    {
        var r = FakeIntake.Family();
        r.Application.ComplianceStandard = ComplianceSettings.Vermont201SFName;
        var missing = Validate(r, ComplianceSettings.Defaults()).Where(i => i.Message.StartsWith("Required for Vermont")).Select(i => i.Path).ToList();
        Assert.Contains("fuelElectric.fuelType", missing);
        Assert.Contains("fuelElectric.vendorAccount", missing);
        Assert.Contains("household[1].ssn", missing);
        Assert.DoesNotContain("client.zip", missing);
        Assert.DoesNotContain("client.firstName", missing);
    }
}
