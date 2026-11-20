using FuelIntake.Core.Model;
using FuelIntake.Core.Validation;

namespace FuelIntake.Core.Tests;

public class ValidationTests
{
    private static IReadOnlyList<ValidationIssue> Check(IntakeRecord r) =>
        IntakeValidator.Validate(r, new IntakeCalculations(r, FakeIntake.Context()), FakeIntake.Today);

    [Fact]
    public void GoodIntake_HasNoErrors() =>
        Assert.DoesNotContain(Check(FakeIntake.Family()), i => i.Severity == IssueSeverity.Error);

    [Fact]
    public void BadPhoneZipAndSsn_AreErrors()
    {
        var r = FakeIntake.Family();
        r.Client.Phone = "555-0199";
        r.Client.Zip = "5819";
        r.Household[1].Ssn = "12-34";
        var errors = Check(r).Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Field).ToList();
        Assert.Contains("Phone number", errors);
        Assert.Contains("Zip", errors);
        Assert.Contains("Demo Sampleton: SSN", errors);
    }

    [Fact]
    public void IncomeMismatch_IsWarning()
    {
        var r = FakeIntake.Family();
        r.IncomeExpenses.Income["Earned"] = 1000m;
        Assert.Contains(Check(r), i => i.Field == "Total income" && i.Severity == IssueSeverity.Warning);
    }

    [Fact]
    public void OverIncomeLimit_IsWarning()
    {
        var r = FakeIntake.Family();
        r.IncomeExpenses.Income["Earned"] = 6000m;
        Assert.Contains(Check(r), i => i.Field == "FPL");
    }
}

public class SsnValidationTests
{
    [Theory]
    [InlineData("12345678")]   // 8 digits: must be re-entered with the leading zero
    [InlineData("12-34")]
    public void ShortSsn_IsAnError(string ssn)
    {
        var r = FakeIntake.Family();
        r.Household[1].Ssn = ssn;
        var issues = IntakeValidator.Validate(r, new IntakeCalculations(r, FakeIntake.Context()), FakeIntake.Today);
        Assert.Contains(issues, i => i.Field == "Demo Sampleton: SSN" && i.Severity == IssueSeverity.Error);
    }
}

public class SampleIntakeTests
{
    [Fact]
    public void Samples_AreTestData_AndHaveNoErrors()
    {
        foreach (var s in FuelIntake.Core.TestData.SampleIntakes.All)
        {
            var r = s.Build("Testy", "Sampleton", FakeIntake.Today);
            Assert.True(r.IsTestData);
            var issues = IntakeValidator.Validate(r, new IntakeCalculations(r, FakeIntake.Context()), FakeIntake.Today);
            Assert.DoesNotContain(issues, i => i.Severity == IssueSeverity.Error);
            Assert.True(new IntakeCalculations(r, FakeIntake.Context()).HouseholdIncomeMatchesWorksheet, s.Name);
        }
    }

    [Fact]
    public void Percent_Formats() => Assert.Equal("93%", FuelIntake.Core.Formatting.Percent.Format(0.934m));
}
