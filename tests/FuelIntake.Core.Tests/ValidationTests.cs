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
