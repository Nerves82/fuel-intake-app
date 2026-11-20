using FuelIntake.Core.Model;

namespace FuelIntake.Core.TestData;

/// <summary>
/// Obviously fake sample intakes for trying the app. Loaded only into test clients
/// (folder names starting with "_TEST "), never into a real client's file.
/// Names, addresses, phones (555-01xx) and SSNs (000-xx-xxxx, never issued) are invented.
/// </summary>
public static class SampleIntakes
{
    public sealed record Sample(string Name, string Description, Func<string, string, DateOnly, IntakeRecord> Build);

    public static readonly IReadOnlyList<Sample> All =
    [
        new("Single senior, low on oil", "One person, Social Security, Fuel Order", SingleSenior),
        new("Family of 4, electric disconnect", "Two adults, two kids, Electric Pledge", FamilyDisconnect),
    ];

    /// <summary>Keeps the test client's own first/last name so the record matches its folder.</summary>
    private static IntakeRecord Base(string first, string last, DateOnly today)
    {
        var r = new IntakeRecord { IsTestData = true };
        r.Staff.AppDate = today;
        r.Client.FirstName = first;
        r.Client.LastName = last;
        r.Client.AddressPhysical = "12 Pretend Pond Rd";
        r.Client.City = "St. Johnsbury";
        r.Client.State = "VT";
        r.Client.Zip = "05819";
        r.Client.County = "Caledonia";
        r.Client.SchoolDistrict = "St. Johnsbury";
        r.Client.Phone = "8025550142";
        r.Client.PrimaryLanguage = "English";
        r.Client.TranslatorNeeded = "N";
        return r;
    }

    public static IntakeRecord SingleSenior(string first, string last, DateOnly today)
    {
        var r = Base(first, last, today);
        r.Client.DateOfBirth = new DateOnly(1951, 4, 9);
        r.Client.HousingType = "Own";
        r.Client.HouseholdType = "Single Person";
        r.Client.Benefits.Snap = true;
        r.Client.Benefits.SnapAmount = 94m;
        r.Client.Benefits.SnapPeriod = "month";
        r.Client.Benefits.SnapVerifiedDate = today;
        r.Client.Interests.FuelAssistance = true;
        r.Household =
        [
            new() { Gender = "Female", Disabled = "N", Education = "High school graduate (high school diploma or equivalent)",
                    Race = "White", Hispanic = "No", HealthInsurance = "Medicare", IncomePerMonth = 1480m,
                    IncomeSource = "Social Security Retirement (SSA)", MilitaryStatus = "Never", Ssn = "000-12-3401",
                    Occupation = "Retired", IncomeVerifiedDate = today },
        ];
        r.Application.CrisisType = "Low on Fuel";
        r.Application.CurrentQuantity = "1/8 tank";
        r.Application.SeasonalFuelStatus = "Active";
        r.Application.Circumstance = "Furnace repair used the fuel budget";
        r.IncomeExpenses.Income["SSA"] = 1480m;
        r.IncomeExpenses.Expenses["HomeHeating"] = new() { PaidLast30Days = 250m, Bill = 250m };
        r.IncomeExpenses.Expenses["Electricity"] = new() { PaidLast30Days = 85m, Bill = 85m };
        r.IncomeExpenses.Expenses["Groceries"] = new() { PaidLast30Days = 300m };
        r.IncomeExpenses.IncomeChangeNextMonth = "No";
        r.IncomeExpenses.AssetChecking = 212.40m;
        r.IncomeExpenses.DocumentsAttached30Days = true;
        r.FuelElectric.FuelType = "Oil";
        r.FuelElectric.SeasonalFuelAmountGranted = 1200m;
        r.FuelElectric.SeasonalFuelDateGranted = today.AddDays(-40);
        r.FuelElectric.VendorAccount = "TEST-1001";
        r.FuelElectric.FuelOrder.Authorized = true;
        r.FuelElectric.FuelOrder.Date = today;
        r.FuelElectric.FuelOrder.TankSize = "275 gal";
        r.FuelElectric.FuelOrder.FuelRemaining = "1/8 tank";
        r.FuelElectric.FuelOrder.FuelLasts = "2 days";
        r.FuelElectric.FuelOrder.Fuel = 450m;
        r.FuelElectric.FuelOrder.Gallons = "125";
        r.FuelElectric.FuelOrder.EstimatedDelivery = today.AddDays(2);
        return r;
    }

    public static IntakeRecord FamilyDisconnect(string first, string last, DateOnly today)
    {
        var r = Base(first, last, today);
        r.Client.DateOfBirth = new DateOnly(1988, 8, 21);
        r.Client.HousingType = "Rent";
        r.Client.HouseholdType = "Two Parent Household";
        r.Client.Benefits.Snap = true;
        r.Client.Benefits.SnapAmount = 512m;
        r.Client.Benefits.SnapPeriod = "month";
        r.Client.Benefits.SnapVerifiedDate = today;
        r.Client.Benefits.Wic = true;
        r.Household =
        [
            new() { Gender = "Male", Disabled = "N", Race = "White", Hispanic = "No", HealthInsurance = "Medicaid",
                    IncomePerMonth = 1900m, IncomeSource = "Employment", MilitaryStatus = "Never", Ssn = "000-12-3402",
                    Occupation = "Employed Full-time", IncomeVerifiedDate = today },
            new() { FirstName = "Partner", LastName = last, DateOfBirth = new DateOnly(1990, 2, 3), Gender = "Female",
                    Disabled = "N", Race = "White", Hispanic = "No", HealthInsurance = "Medicaid", IncomePerMonth = 650m,
                    IncomeSource = "Employment", MilitaryStatus = "Never", Ssn = "000-12-3403",
                    Occupation = "Employed part-time and in school part-time", IncomeVerifiedDate = today },
            new() { FirstName = "Kiddo", LastName = last, DateOfBirth = new DateOnly(2016, 6, 1), Gender = "Female",
                    HealthInsurance = "Medicaid", Ssn = "000-12-3404", Occupation = "N/A" },
            new() { FirstName = "Tot", LastName = last, DateOfBirth = new DateOnly(2021, 10, 12), Gender = "Male",
                    HealthInsurance = "Medicaid", Ssn = "000-12-3405", Occupation = "N/A" },
        ];
        r.Application.CrisisType = "Disconnect Notice";
        r.Application.ElectricForHeat = true;
        r.Application.ElectricComponent = "Pellet stove";
        r.Application.SeasonalFuelStatus = "Pending";
        r.Application.Circumstance = "Hours cut at work";
        r.IncomeExpenses.Income["Earned"] = 2550m;
        r.IncomeExpenses.Expenses["Rent"] = new() { PaidLast30Days = 1100m, Bill = 1100m };
        r.IncomeExpenses.Expenses["Electricity"] = new() { PaidLast30Days = 0m, Bill = 389.12m };
        r.IncomeExpenses.Expenses["Groceries"] = new() { PaidLast30Days = 650m };
        r.IncomeExpenses.Expenses["Childcare"] = new() { PaidLast30Days = 300m, Bill = 300m };
        r.IncomeExpenses.IncomeChangeNextMonth = "Yes";
        r.IncomeExpenses.IncomeChangeExplanation = "Hours back to full time next month";
        r.FuelElectric.FuelType = "Pellet";
        r.FuelElectric.ElectricAccount = "TEST-2002";
        r.FuelElectric.ElectricPledge.Authorized = true;
        r.FuelElectric.ElectricPledge.Date = today;
        r.FuelElectric.ElectricPledge.DisconnectNoticeAttached = true;
        r.FuelElectric.ElectricPledge.AmountRequired = 389.12m;
        r.FuelElectric.ElectricPledge.DiscountProgram = "No";
        r.FuelElectric.ElectricPledge.DisconnectDate = today.AddDays(5);
        return r;
    }
}
