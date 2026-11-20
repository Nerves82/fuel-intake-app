using FuelIntake.Core.Model;
using FuelIntake.Core.Validation;
using FuelIntake.Core.TestData;

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
        foreach (var s in FuelIntake.Core.TestData.SampleIntakes.All.Where(x => !x.HasErrors))
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


public class NeedsFixingSampleTests
{
    [Fact]
    public void NeedsFixing_ShowsErrors()
    {
        var s = FuelIntake.Core.TestData.SampleIntakes.All.Single(x => x.HasErrors);
        var r = s.Build("Testy", "Sampleton", FakeIntake.Today);
        var issues = IntakeValidator.Validate(r, new IntakeCalculations(r, FakeIntake.Context()), FakeIntake.Today);
        Assert.Contains(issues, i => i.Field == "Phone number");
        Assert.Contains(issues, i => i.Field == "Zip");
        Assert.Contains(issues, i => i.Field.EndsWith("SSN"));
        Assert.Contains(issues, i => i.Field == "Total income");
        Assert.Contains(issues, i => i.Field == "Crisis type");
    }

    [Fact]
    public void LargeHousehold_FillsTenRows_AndIsUnderLimitCheckable()
    {
        var r = FuelIntake.Core.TestData.SampleIntakes.LargeHousehold("Testy", "Sampleton", FakeIntake.Today);
        var c = new IntakeCalculations(r, FakeIntake.Context());
        Assert.Equal(10, c.HouseholdSize);
        Assert.Equal(838.75m, c.InvoiceTotal);
        Assert.True(c.HouseholdIncomeMatchesWorksheet);
    }
}

public class TestDataSetTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void RealClient_CannotBeSaved()
    {
        var r = FakeIntake.Family();
        r.IsTestData = false;
        Assert.Throws<InvalidOperationException>(() => TestDataSetRules.FromRecord(r, "Copy", null, null, At));
    }

    [Fact]
    public void Save_StripsHistory_AndBuild_UsesNewNameAndDate()
    {
        var r = SampleIntakes.FamilyDisconnect("Testy", "Sampleton", FakeIntake.Today);
        r.Staff.StaffName = "Pat Example";
        r.Notes.Add(new StaffNote { Text = "call back", At = At });
        r.CreatedBy = new StaffStamp("Pat Example", null);
        var set = TestDataSetRules.FromRecord(r, "  Disconnect variant ", "  ", new StaffStamp("Pat Example", null), At);

        Assert.Equal("Disconnect variant", set.Name);
        Assert.Null(set.Description);
        Assert.Empty(set.Record.Notes);
        Assert.Null(set.Record.CreatedBy);
        Assert.Null(set.Record.Staff.StaffName);
        r.Client.City = "Changed later";
        Assert.Equal("St. Johnsbury", set.Record.Client.City); // a copy, not the live record

        var made = TestDataSetRules.AsSample(set).Build("Practice", "Person", FakeIntake.Today.AddDays(3));
        Assert.True(made.IsTestData);
        Assert.Equal("Practice", made.Client.FirstName);
        Assert.Equal("Person", made.Client.LastName);
        Assert.Equal(FakeIntake.Today.AddDays(3), made.Staff.AppDate);
        Assert.Equal(4, made.Household.Count);
        Assert.All(made.Household.Skip(1), m => Assert.Equal("Person", m.LastName)); // family shares the new last name
        Assert.Equal(389.12m, made.FuelElectric.ElectricPledge.AmountRequired);
    }

    [Fact]
    public void Json_RoundTrips_AndRejectsNonTestRecords()
    {
        var set = TestDataSetRules.FromRecord(SampleIntakes.LargeHousehold("Testy", "Sampleton", FakeIntake.Today), "Big", "ten people", null, At);
        var back = TestDataSetRules.FromJson(TestDataSetRules.ToJson(set));
        Assert.NotNull(back);
        Assert.Equal(10, back!.Record.Household.Count);
        Assert.Equal("ten people", back.Description);

        back.Record.IsTestData = false;
        Assert.Null(TestDataSetRules.FromJson(TestDataSetRules.ToJson(back)));
        Assert.Null(TestDataSetRules.FromJson("not json"));
    }

    [Fact]
    public async Task MemoryStore_SavesReplacesDeletes()
    {
        var store = new MemoryTestDataSets();
        var a = TestDataSetRules.FromRecord(SampleIntakes.SingleSenior("T", "S", FakeIntake.Today), "B set", null, null, At);
        await store.SaveAsync(a);
        await store.SaveAsync(TestDataSetRules.FromRecord(SampleIntakes.SingleSenior("T", "S", FakeIntake.Today), "A set", null, null, At));
        a.Description = "edited";
        await store.SaveAsync(a);
        var list = await store.ListAsync();
        Assert.Equal(["A set", "B set"], list.Select(x => x.Name));
        Assert.Equal("edited", list[1].Description);
        await store.DeleteAsync(a.Id);
        Assert.Single(await store.ListAsync());
    }
}
