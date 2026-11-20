using FuelIntake.Core.Mapping;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Tests;

public class IntakeCalculationsTests
{
    private static IntakeCalculations Calc(IntakeRecord r) => new(r, FakeIntake.Context());

    [Fact]
    public void Member1_TakesNameAndBirthdateFromClient()
    {
        var m1 = Calc(FakeIntake.Family()).Members[0];
        Assert.Equal("Testy Sampleton", m1.FullName);
        Assert.Equal(new DateOnly(1980, 11, 17), m1.DateOfBirth);
    }

    [Fact]
    public void Age_IsOnApplicationDate()
    {
        var c = Calc(FakeIntake.Family());
        Assert.Equal(45, c.AgeOf(c.Members[0])); // birthday is the day after the app date
        Assert.Equal(8, c.AgeOf(c.Members[2]));
    }

    [Fact]
    public void HouseholdSize_CountsNamedRows()
    {
        var r = FakeIntake.Family();
        r.Household.Add(new HouseholdMember { IncomePerMonth = 5m }); // no name: not counted
        Assert.Equal(3, Calc(r).HouseholdSize);
    }

    [Fact]
    public void IncomeAndFpl()
    {
        var c = Calc(FakeIntake.Family());
        Assert.Equal(1800m, c.IncomeTotal);
        Assert.Equal(1020m, c.ExpensesPaidTotal);
        Assert.Equal(1100m, c.ExpensesBillTotal);
        Assert.Equal(780m, c.IncomeMinusExpenses);
        Assert.True(c.HouseholdIncomeMatchesWorksheet);      // 1200 + 600 = 1800
        Assert.Equal(27320m, c.AnnualGuideline);              // household of 3
        Assert.Equal(1800m * 12 / 27320m, c.FplPercent);
        Assert.True(c.UnderIncomeLimit);
    }

    [Fact]
    public void Addresses()
    {
        var r = FakeIntake.Family();
        var c = Calc(r);
        Assert.Equal("St. Johnsbury, VT 05819", c.CityStateZip);
        Assert.Equal("1 Example Rd", c.ServiceAddressLine1);
        Assert.Equal("St. Johnsbury, VT 05819", c.ServiceAddressLine2);
        r.Client.ServiceAddressLine1 = "2 Other Ln";
        Assert.Equal("2 Other Ln", Calc(r).ServiceAddressLine1);
    }

    [Fact]
    public void PaperCheckboxes()
    {
        var c = Calc(FakeIntake.Family());
        Assert.Equal(HousingBox.Other, c.HousingBox);
        Assert.Equal(HouseholdTypeBox.TwoParentHousehold, c.HouseholdTypeBox);
        Assert.Equal(2, c.OccupationRows.Count);              // the child (N/A) gets no row
        Assert.Contains(OccupationBox.Student, c.OccupationRows[1].Boxes);
    }

    [Fact]
    public void Staff_FromListThenOverrides()
    {
        var r = FakeIntake.Family();
        var c = Calc(r);
        Assert.Equal("PE", c.StaffInitials);                  // automatic: list has no initials
        Assert.Equal("pexample@example.org", c.InvoiceEmail);
        Assert.Equal("(802) 555-0100", c.InvoicePhone);

        r.Staff.InitialsOverride = "pjx";
        r.Staff.PhoneOverride = "8025550111";
        c = Calc(r);
        Assert.Equal("PJX", c.StaffInitials);
        Assert.Equal("(802) 555-0111", c.InvoicePhone);
    }

    [Fact]
    public void Invoice_FallsBackToAgencyDefault()
    {
        var r = FakeIntake.Family();
        r.Staff.StaffName = "Not On List";
        var c = Calc(r);
        Assert.Equal("fuel@example.org", c.InvoiceEmail);
        Assert.Equal("(802) 555-0000", c.InvoicePhone);
        Assert.Equal("NL", c.StaffInitials);
    }

    [Fact]
    public void Vendors_FromListUnlessOverridden()
    {
        var r = FakeIntake.Family();
        r.FuelElectric.Vendor = "Example Oil - St. Johnsbury";
        r.FuelElectric.ElectricProvider = "Sample Power";
        var c = Calc(r);
        Assert.Equal("orders@exampleoil.test", c.VendorEmails);
        Assert.Equal("802-555-0101", c.VendorPhones);
        Assert.Equal("billing@samplepower.test", c.ElectricEmails);

        r.FuelElectric.VendorPhoneOverride = "802-555-0222";
        Assert.Equal("802-555-0222", Calc(r).VendorPhones);
    }

    [Fact]
    public void Totals_BlankUntilSomethingEntered()
    {
        var r = FakeIntake.Family();
        Assert.Null(Calc(r).FuelOrderTotal);
        r.FuelElectric.FuelOrder.Fuel = 400m;
        r.FuelElectric.FuelOrder.SpecialTrip = 75m;
        Assert.Equal(475m, Calc(r).FuelOrderTotal);
        r.FuelElectric.ElectricPledge.AmountRequired = 250m;
        Assert.Equal(250m, Calc(r).PledgeTotal);
    }

    [Fact]
    public void AccessLines()
    {
        var r = FakeIntake.Family();
        r.Client.Benefits.Snap = true;
        r.Client.Benefits.SnapAmount = 291m;
        r.Client.Benefits.SnapPeriod = "month";
        var c = Calc(r);
        Assert.Equal("SNAP $291.00/month", c.AccessNonIncomeLine);
        Assert.Equal("$1,200.00/mo - Employment", IntakeCalculations.IncomeText(c.Members[0]));
    }
}
