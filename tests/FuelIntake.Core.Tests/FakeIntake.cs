using FuelIntake.Core.Contacts;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Tests;

/// <summary>Obviously fake test data. Never use real client details in tests.</summary>
internal static class FakeIntake
{
    public static readonly DateOnly Today = new(2026, 11, 20);

    public static IntakeRecord Family()
    {
        var r = new IntakeRecord { IsTestData = true };
        r.Staff.AppDate = new DateOnly(2026, 11, 16);
        r.Staff.StaffName = "Pat Example";
        r.Client.FirstName = "Testy";
        r.Client.LastName = "Sampleton";
        r.Client.DateOfBirth = new DateOnly(1980, 11, 17); // turns 46 the day after the app date
        r.Client.AddressPhysical = "1 Example Rd";
        r.Client.City = "St. Johnsbury";
        r.Client.State = "VT";
        r.Client.Zip = "05819";
        r.Client.Phone = "802-555-0199";
        r.Client.HousingType = "Mobile Home/Trailer";
        r.Client.HouseholdType = "Two Parent Household";
        r.Household =
        [
            new() { Ssn = "000123456", IncomePerMonth = 1200m, IncomeSource = "Employment", Occupation = "Employed Full-time" },
            new() { FirstName = "Demo", LastName = "Sampleton", DateOfBirth = new DateOnly(1982, 1, 1), IncomePerMonth = 600m,
                    IncomeSource = "Employment", Occupation = "In school full-time and employed part-time" },
            new() { FirstName = "Kid", LastName = "Sampleton", DateOfBirth = new DateOnly(2018, 5, 5), Occupation = "N/A" },
        ];
        r.IncomeExpenses.Income["Earned"] = 1800m;
        r.IncomeExpenses.Expenses["Rent"] = new ExpenseAmounts { PaidLast30Days = 900m, Bill = 900m };
        r.IncomeExpenses.Expenses["Electricity"] = new ExpenseAmounts { PaidLast30Days = 120m, Bill = 200m };
        r.Application.CrisisType = "Low on Fuel";
        return r;
    }

    public static CalculationContext Context() => new(
        new AgencySettings { DefaultInvoiceEmail = "fuel@example.org", DefaultInvoicePhone = "8025550000" },
        new StaffDirectory([new("Pat Example", null, "802-555-0100", "pexample@example.org")]),
        new VendorDirectory([
            new Vendor("Example Oil", "St. Johnsbury", "orders@exampleoil.test", "802-555-0101", "Oil"),
            new Vendor("Sample Power", null, "billing@samplepower.test", "800-555-0103", "Electric"),
        ]),
        Today);
}
