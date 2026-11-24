using FuelIntake.Core.Compliance;
using FuelIntake.Core.Forms;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Tests;

public class StateReviewTests
{
    private static Form201SFFill Fill(IntakeRecord r) => Form201SF.Fill(r, new IntakeCalculations(r, FakeIntake.Context()));
    private static string? TextAt(Form201SFFill f, string field) => f.Marks.FirstOrDefault(m => m.Spot == Form201SF.Spot(field) && !m.IsCheck)?.Text;
    private static bool Ticked(Form201SFFill f, string field) => f.Marks.Any(m => m.Spot == Form201SF.Spot(field) && m.IsCheck);

    [Fact]
    public void Tabs_ComeFromTheStandard()
    {
        var c = ComplianceSettings.Defaults();
        var r = FakeIntake.Family();
        Assert.Empty(InputTabs.For(r, c));
        r.Application.ComplianceStandard = ComplianceSettings.Vermont201SFName;
        Assert.Equal([InputTabs.Vermont201SF], InputTabs.For(r, c).Select(t => t.Id));
        r.Application.ComplianceStandard = "Warmth";
        Assert.Empty(InputTabs.For(r, c));
        c.Standards.Single(s => s.Name == "Warmth").ExtraTabs.Add(InputTabs.Vermont201SF);
        Assert.Single(InputTabs.For(r, c));
        c.Standards.Single(s => s.Name == "Warmth").ExtraTabs.Add("no-such-tab");
        Assert.Single(InputTabs.For(r, c));
    }

    [Fact]
    public void Catalog_OffersTheTabsFields_ForRequiring()
    {
        var f = FieldCatalog.Find("stateReview.bedrooms");
        Assert.NotNull(f);
        Assert.Equal(InputTabs.Vermont201SFTitle, f!.Tab);
        Assert.NotNull(FieldCatalog.Find("stateReview.job1.employer"));
        Assert.NotNull(FieldCatalog.Find("household[*].relationship"));
    }

    [Fact]
    public void Page1_RelationshipsAndImmigrationIds()
    {
        var r = FakeIntake.Family();
        r.Household[1].Relationship = "Spouse";
        r.Household[1].ImmigrationId = "FAKE-ID-1";
        r.StateReview.DayPhone = "8025550123";
        var f = Fill(r);
        Assert.Equal("Spouse", TextAt(f, "Page1[0].RelationshipRow1[0]"));
        Assert.Equal("FAKE-ID-1", TextAt(f, "Page1[0].ID_2[0]"));
        Assert.Equal("(802) 555-0123", TextAt(f, "Page1[0].Day__Message_Phone_with_area_code[0]"));
    }

    [Fact]
    public void Page2_AnswersWinOverWhatTheIntakeImplies()
    {
        var r = FakeIntake.Family();
        r.Client.HousingType = "Own";
        var x = r.StateReview;
        x.RentsRoom = "Yes"; x.RoomerName = "Fake Roomer"; x.RoomerRent = 300m;
        x.Caregiver = "No";
        x.LivingSituation = "Rent a room in someone's home"; x.RoomHost = "Fake Host"; x.RoomRent = 400m;
        x.HeatPayment = "My landlord bills me for all fuel";
        x.Bedrooms = "3";
        x.RentBasedOnIncome = "No";
        x.SupplierAccountName = "Testy Sampleton";
        var f = Fill(r);
        Assert.True(Ticked(f, "Page2[0].RadioButtonList[0]#yes"));
        Assert.Equal("Fake Roomer", TextAt(f, "Page2[0].Roomer[0]"));
        Assert.True(Ticked(f, "Page2[0].RadioButtonList[1]#no"));
        Assert.False(Ticked(f, "Page2[0].CheckBox1[2]"));   // not "own", though tab 1 says Own
        Assert.True(Ticked(f, "Page2[0].CheckBox1[4]"));    // rents a room
        Assert.Equal("400.00", TextAt(f, "Page2[0].and_pay[0]"));
        Assert.True(Ticked(f, "Page2[0].CheckBox1[9]"));    // landlord bills
        Assert.Equal("3", TextAt(f, "Page2[0].Bedrooms[0]"));
        Assert.True(Ticked(f, "Page2[0].RadioButtonList[3]#no"));
        Assert.Equal("Testy Sampleton", TextAt(f, "Page2[0].Name_on_account[0]"));
    }

    [Fact]
    public void Question6_OwnAnswerElseTab1()
    {
        var r = FakeIntake.Family();
        r.Client.HousingType = "Mobile Home/Trailer";
        Assert.True(Ticked(Fill(r), "Page2[0].CheckBox1[11]"));
        r.Client.HousingType = "Own";                       // answers question 4, not 6
        Assert.Equal("", Form201SF.HousingKindOf(r));
        r.StateReview.HousingKind = "Single-family house";
        Assert.True(Ticked(Fill(r), "Page2[0].CheckBox1[10]"));
        r.StateReview.HousingKind = "Other"; r.StateReview.HousingOther = "Duplex";
        var f = Fill(r);
        Assert.True(Ticked(f, "Page2[0].CheckBox1[13]"));
        Assert.Equal("Duplex", TextAt(f, "Page2[0].Type_of_housing[0]"));
        Assert.False(Ticked(f, "Page2[0].CheckBox1[10]"));
    }

    [Fact]
    public void Page3_JobsAndOtherIncome()
    {
        var r = FakeIntake.Family();
        var j = r.StateReview.Job2;
        j.FirstName = "Demo"; j.Initial = "Q"; j.HowOften = "Every two weeks"; j.Employer = "Example Mill"; j.EmployerPhone = "8025550199";
        j.Pay = [new() { DatePaid = new DateOnly(2026, 11, 6), Hours = "40", HourlyRate = 15m, Gross = 600m, Tips = 0m },
                 new() { DatePaid = new DateOnly(2026, 11, 20), Hours = "38.5", Gross = 577.5m }];
        r.StateReview.SelfEmployment = "No";
        r.StateReview.RentalNames = "Testy Sampleton"; r.StateReview.RentalAmount = 250m;
        var f = Fill(r);
        Assert.Equal("Demo", TextAt(f, "Page3[0].FirstName[1]"));
        Assert.True(Ticked(f, "Page3[0].CheckBox1[8]"));    // job 2, every two weeks
        Assert.Equal("Example Mill", TextAt(f, "Page3[0].EmployerName[1]"));
        Assert.Equal("(802) 555-0199", TextAt(f, "Page3[0].Employer_Phone[1]"));
        Assert.Equal("11/06/2026", TextAt(f, "Page3[0].Date_paidRow1_2[0]"));
        Assert.Equal("38.5", TextAt(f, "Page3[0].Hours_WorkedRow2_2[0]"));
        Assert.Equal("577.50", TextAt(f, "Page3[0].fill_67[0]"));
        Assert.True(Ticked(f, "Page3[0].RadioButtonList[0]#no"));
        Assert.Equal("250.00", TextAt(f, "Page3[0].fill_89[0]"));
        Assert.True(Ticked(f, "Page3[0].RadioButtonList[1]#yes"));
    }

    [Fact]
    public void JobBoxes_FollowThePayFrequencyList()
    {
        // weekly, twice a month, every two weeks on the top line; monthly, other below (job 1)
        var r = FakeIntake.Family();
        foreach (var (often, box) in new[] { ("Weekly", 0), ("Twice a month", 1), ("Every two weeks", 3), ("Monthly", 2), ("Other", 4) })
        {
            r.StateReview.Job1.HowOften = often;
            Assert.True(Ticked(Fill(r), $"Page3[0].CheckBox1[{box}]"), often);
        }
    }
}
