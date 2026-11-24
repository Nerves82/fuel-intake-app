using FuelIntake.Core.Compliance;
using FuelIntake.Core.Lists;
using FuelIntake.Core.Model;
using FuelIntake.Core.Rules;
using FuelIntake.Core.Validation;

namespace FuelIntake.Core.Tests;

public class CrisisFuelRulesTests
{
    private static IReadOnlyList<ValidationIssue> Validate(IntakeRecord r) =>
        IntakeValidator.Validate(r, new IntakeCalculations(r, FakeIntake.Context() with { Compliance = ComplianceSettings.Defaults() }), FakeIntake.Today);

    private static IntakeRecord CrisisFuel()
    {
        var r = FakeIntake.Family();
        r.Application.ComplianceStandard = "Crisis Fuel";
        return r;
    }

    private static List<ValidationIssue> From(IReadOnlyList<ValidationIssue> all, string ruleId) => all.Where(i => i.Rule == ruleId).ToList();

    [Fact]
    public void Catalog_IdsAreUnique_AndEveryFieldRuleExists()
    {
        Assert.Equal(CrisisFuelRules.All.Count, CrisisFuelRules.All.Select(r => r.Id).Distinct().Count());
        Assert.All(ComplianceChecks.All, c => Assert.NotNull(c.Rule));
        Assert.Equal(["3104-ssn"], CrisisFuelRules.ForField("household[3].ssn").Select(r => r.Id));
        Assert.Contains(CrisisFuelRules.ForField("incomeExpenses.income.SSA"), r => r.Id == "3102-income");
        Assert.Contains(CrisisFuelRules.ForField("application.referrals.neto"), r => r.Id == "3110");
        Assert.Empty(CrisisFuelRules.ForField("client.email"));
        Assert.Empty(CrisisFuelRules.ForField(null));
    }

    [Fact]
    public void Defaults_CrisisFuelHasTheRules_AndOnlyRealFields()
    {
        var c = ComplianceSettings.Defaults();
        var cf = c.Standards.Single(s => s.Name == "Crisis Fuel");
        Assert.True(cf.HasAllCrisisFuelRules);
        Assert.All(cf.Required, k => Assert.NotNull(FieldCatalog.Find(k)));
        Assert.Empty(c.Standards.Single(s => s.Name == "Warmth").Checks);

        var old = new ComplianceStandard { Name = "Crisis Fuel", Required = ["client.email"] };
        old.AddCrisisFuelRules();
        old.AddCrisisFuelRules();
        Assert.Contains("client.email", old.Required);                     // keeps what was there
        Assert.Equal(old.Required.Count, old.Required.Distinct().Count()); // no duplicates
        Assert.True(old.HasAllCrisisFuelRules);
    }

    [Fact]
    public void ChecksOnlyRunForTheStandard()
    {
        var r = FakeIntake.Family();
        r.Application.ComplianceStandard = "Warmth";
        Assert.DoesNotContain(Validate(r), i => i.Rule is not null);
    }

    [Fact]
    public void IncomeOver200_IsAnError_AndSecondBenefitNeedsOver185()
    {
        var r = CrisisFuel();
        r.IncomeExpenses.Income["Earned"] = 20000m; // far over
        var issues = Validate(r);
        var over = Assert.Single(From(issues, "3102-income"));
        Assert.Equal(IssueSeverity.Error, over.Severity);
        Assert.StartsWith("Crisis Fuel rule 3102(c)(3):", over.Message);

        r = CrisisFuel();
        r.Application.SecondCrisis = true;
        Assert.Single(From(Validate(r), "3109-limits"));
    }

    [Fact]
    public void Seasonal_NeededUnlessOver185()
    {
        var r = CrisisFuel();
        r.Application.SeasonalFuelStatus = null;
        Assert.Equal("application.seasonalFuelStatus", Assert.Single(From(Validate(r), "3102-seasonal")).Path);
        r.Application.SeasonalFuelStatus = "Pending";
        Assert.Empty(From(Validate(r), "3102-seasonal"));
    }

    [Fact]
    public void BulkLimit_125Gallons_ForLiquidFuels()
    {
        var r = CrisisFuel();
        r.FuelElectric.FuelType = "Oil";
        r.FuelElectric.FuelOrder.Gallons = "150 gal";
        Assert.Equal("fuelElectric.fuelOrder.gallons", Assert.Single(From(Validate(r), "3108-bulk")).Path);
        r.FuelElectric.FuelOrder.Gallons = "125";
        Assert.Empty(From(Validate(r), "3108-bulk"));
        r.FuelElectric.FuelType = "Wood";
        r.FuelElectric.FuelOrder.Gallons = "150";
        Assert.Empty(From(Validate(r), "3108-bulk"));
    }

    [Fact]
    public void Supplier_RequiredUnlessHeatIsElectric()
    {
        var r = CrisisFuel();
        r.FuelElectric.FuelType = "Oil";
        r.FuelElectric.Vendor = null; r.FuelElectric.VendorAccount = null;
        Assert.Equal(2, From(Validate(r), "3104-supplier").Count);
        r.FuelElectric.FuelType = "Electric";
        Assert.Empty(From(Validate(r), "3104-supplier"));
    }

    [Fact]
    public void Emergency_TankOver25Percent_OrNoNotice()
    {
        var r = CrisisFuel();
        r.Application.CrisisType = CrisisTypes.LowOnFuel;
        r.FuelElectric.FuelType = "Oil";
        r.FuelElectric.FuelOrder.FuelRemaining = "1/2 tank";
        Assert.Single(From(Validate(r), "3102-emergency"));
        r.FuelElectric.FuelOrder.FuelRemaining = "1/8";
        Assert.Empty(From(Validate(r), "3102-emergency"));

        r.Application.CrisisType = CrisisTypes.DisconnectNotice;
        r.FuelElectric.ElectricPledge.DisconnectNoticeAttached = false;
        Assert.Single(From(Validate(r), "3102-emergency"));
    }

    [Theory]
    [InlineData("1/4", 0.25)]
    [InlineData("about 1/8 tank", 0.125)]
    [InlineData("30%", 0.30)]
    [InlineData(".2", 0.2)]
    [InlineData("few bags", null)]
    public void Fraction_ReadsTankLevels(string text, double? expected) =>
        Assert.Equal(expected is null ? null : (decimal?)expected, ComplianceChecks.Fraction(text));

    [Theory]
    [InlineData("2026-11-23", false, false)] // the Monday before the last Monday in November 2026 (Nov 30): too early
    [InlineData("2026-11-30", false, true)]
    [InlineData("2027-04-09", false, true)]  // second Friday in April 2027
    [InlineData("2027-04-12", false, false)]
    [InlineData("2027-04-30", true, true)]   // metered: last business day of April 2027 (a Friday)
    [InlineData("2027-05-03", true, false)]
    public void ApplicationSeason(string date, bool metered, bool inside)
    {
        var d = DateOnly.Parse(date);
        var (opens, closes) = CrisisFuelRules.ApplicationWindow(d, metered);
        Assert.Equal(inside, d >= opens && d <= closes);
    }

    [Fact]
    public void Reminders_AreInfoOnly()
    {
        var r = CrisisFuel();
        r.Application.LifeThreatening = true;
        var reminders = From(Validate(r), "3107-deadline");
        Assert.NotEmpty(reminders);
        Assert.All(reminders, i => Assert.Equal(IssueSeverity.Info, i.Severity));
    }
}
