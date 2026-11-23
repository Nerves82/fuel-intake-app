using FuelIntake.Core.Compliance;
using FuelIntake.Core.Model;
using FuelIntake.Core.Validation;

namespace FuelIntake.Core.Tests;

public class ComplianceTests
{
    private static ComplianceSettings Settings(params string[] required)
    {
        var c = ComplianceSettings.Defaults();
        c.Standards[1].GrantSources = ["Example Crisis Fund"];
        c.Standards[1].Required = required.ToList();
        return c;
    }

    private static IReadOnlyList<ValidationIssue> Validate(IntakeRecord r, ComplianceSettings c) =>
        IntakeValidator.Validate(r, new IntakeCalculations(r, FakeIntake.Context() with { Compliance = c }), FakeIntake.Today);

    [Fact]
    public void Catalog_ListsTheRecordsFields_WithoutBookkeeping()
    {
        var keys = FieldCatalog.All.Select(f => f.Key).ToHashSet();
        Assert.Contains("client.dateOfBirth", keys);
        Assert.Contains("household[*].ssn", keys);
        Assert.Contains("client.benefits.snap", keys);
        Assert.Contains("incomeExpenses.income.SSA", keys);
        Assert.Contains("incomeExpenses.expenses.Rent.bill", keys);
        Assert.Contains("fuelElectric.fuelOrder.fundingSource", keys);
        Assert.DoesNotContain("activity", keys);
        Assert.DoesNotContain("isTestData", keys);
        Assert.DoesNotContain("application.complianceStandard", keys);
        Assert.Equal(keys.Count, FieldCatalog.All.Count); // no duplicates
        Assert.Equal("Date of birth", FieldCatalog.Find("client.dateOfBirth")!.Label);
        Assert.True(FieldCatalog.Find("client.benefits.snap")!.IsCheck);
    }

    [Fact]
    public void Standard_ComesFromTheGrant_OrThePick()
    {
        var c = Settings();
        var r = FakeIntake.Family();
        Assert.Null(ComplianceRules.Effective(r, c));
        r.FuelElectric.FuelOrder.FundingSource = "example crisis fund";
        Assert.Equal("Crisis Fuel", ComplianceRules.Effective(r, c)!.Name);
        r.Application.ComplianceStandard = "Warmth";
        Assert.Equal("Warmth", ComplianceRules.Effective(r, c)!.Name);
    }

    [Fact]
    public void MissingRequiredFields_AreErrors_PerMemberToo()
    {
        var c = Settings("client.email", "household[*].ssn", "household[*].firstName", "client.benefits.wic", "incomeExpenses.income.SSA");
        var r = FakeIntake.Family();
        r.Client.Email = null;
        r.Household[2].Ssn = null;
        r.Client.Benefits.Wic = false;
        r.FuelElectric.FuelOrder.FundingSource = "Example Crisis Fund";

        var missing = ComplianceRules.Missing(r, c.Standards[1]);
        Assert.Contains(missing, m => m.Field.Key == "client.email");
        Assert.Contains(missing, m => m.Field.Key == "household[*].ssn" && m.Member == 3);
        Assert.DoesNotContain(missing, m => m.Field.Key == "household[*].firstName" && m.Member == 1); // the client's name is on tab 1
        Assert.Contains(missing, m => m.Field.Key == "client.benefits.wic");          // a required checkbox must be ticked
        Assert.Contains(missing, m => m.Field.Key == "incomeExpenses.income.SSA");

        var issues = Validate(r, c);
        Assert.Contains(issues, i => i.Severity == IssueSeverity.Error && i.Message.StartsWith("Required for Crisis Fuel"));

        r.FuelElectric.FuelOrder.FundingSource = null; // no standard applies: no compliance errors
        Assert.DoesNotContain(Validate(r, c), i => i.Message.StartsWith("Required for"));
    }

    [Fact]
    public void Passcode_IsStoredHashed_AndChecked()
    {
        var c = ComplianceSettings.Defaults();
        Assert.False(ComplianceRules.HasPasscode(c));
        Assert.Throws<ArgumentException>(() => ComplianceRules.SetPasscode(c, "12"));
        ComplianceRules.SetPasscode(c, "fuel-2026");
        Assert.DoesNotContain("fuel-2026", c.PasscodeHash!);
        Assert.True(ComplianceRules.CheckPasscode(c, " fuel-2026 "));
        Assert.False(ComplianceRules.CheckPasscode(c, "fuel-2025"));
        Assert.False(ComplianceRules.CheckPasscode(c, null));
    }

    [Fact]
    public void Badge_ShowsThePick_ThenTheGrant_ThenNothing()
    {
        var c = Settings();
        var r = FakeIntake.Family();
        r.FuelElectric.FuelOrder.FundingSource = null;
        r.FuelElectric.ElectricPledge.FundingSource = null;
        r.FuelElectric.Invoice.FundingSource = null;
        r.Application.ComplianceStandard = null;
        Assert.Null(ComplianceBadge.For(r, c));
        Assert.Null(ComplianceBadge.For(null, c));

        r.FuelElectric.FuelOrder.FundingSource = "Example Crisis Fund";
        Assert.Equal(new ComplianceBadge("Crisis Fuel", true, false), ComplianceBadge.For(r, c));

        r.Application.ComplianceStandard = "warmth";
        Assert.Equal(new ComplianceBadge("Warmth", false, false), ComplianceBadge.For(r, c));

        r.Application.ComplianceStandard = "Retired Standard";
        var b = ComplianceBadge.For(r, c)!;
        Assert.True(b.Unknown);
        Assert.Equal("Retired Standard", b.Name);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData("crisis fuel", "Crisis Fuel")]
    [InlineData(" Warmth ", "Warmth")]
    [InlineData("Not A Standard", null)]
    public void NewClient_UsesTheSettingsSpelling_OrNone(string? chosen, string? expected) =>
        Assert.Equal(expected, ComplianceBadge.ForNewClient(chosen, Settings()));
}
