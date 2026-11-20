using FuelIntake.Core.Model;

namespace FuelIntake.Core.TestData;

/// <summary>
/// Obviously fake sample intakes for trying the app. Loaded only into test clients
/// (folder names starting with "_TEST "), never into a real client's file.
/// Names, addresses, phones (555-01xx) and SSNs (000-xx-xxxx, never issued) are invented.
/// </summary>
public static class SampleIntakes
{
    /// <param name="Name">Shown in the "Practice with test data" list.</param>
    /// <param name="Description">One line on what the case exercises.</param>
    /// <param name="Build">Makes the record for a test client's first/last name and today's date.</param>
    /// <param name="HasErrors">True for cases meant to show Review-tab errors.</param>
    /// <param name="SavedId">Set for sets staff saved themselves (they can be deleted).</param>
    public sealed record Sample(string Name, string Description, Func<string, string, DateOnly, IntakeRecord> Build,
        bool HasErrors = false, string? SavedId = null);

    public static readonly IReadOnlyList<Sample> All =
    [
        new("Single senior, low on oil", "One person, Social Security, Fuel Order", SingleSenior),
        new("Family of 4, electric disconnect", "Two adults, two kids, Electric Pledge", FamilyDisconnect),
        new("Over income, denied (phone application)", "Single earner over 200% FPL, denial boxes, phone app", OverIncomeDenied),
        new("Household of 10, propane with fees", "Every household row filled, special trip + pressure test, invoice received", LargeHousehold),
        new("Multigenerational, SSDI + SSI, pellets", "Grandparent, parents, child; electric runs the pellet stove", Multigenerational),
        new("Temporary shelter, kerosene, second crisis", "Hotel/Motel, no SNAP, second crisis assist, after-hours", ShelterKerosene),
        new("Needs fixing (Review tab practice)", "Bad phone, zip and SSN, income mismatch, missing crisis type", NeedsFixing, HasErrors: true),
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

    public static IntakeRecord OverIncomeDenied(string first, string last, DateOnly today)
    {
        var r = Base(first, last, today);
        r.Client.DateOfBirth = new DateOnly(1979, 3, 14);
        r.Client.HousingType = "House";
        r.Client.HouseholdType = "Single Person";
        r.Client.Email = "testy.example@example.org";
        r.Household =
        [
            new() { Gender = "Male", Disabled = "N", Education = "Bachelor's degree", Race = "White", Hispanic = "No",
                    HealthInsurance = "Employment Based", IncomePerMonth = 4200m, IncomeSource = "Employment",
                    MilitaryStatus = "Veteran", Ssn = "000-12-3406", Occupation = "Employed Full-time", IncomeVerifiedDate = today },
        ];
        r.Application.CrisisType = "Low on Fuel";
        r.Application.CurrentQuantity = "1/4 tank";
        r.Application.SeasonalFuelStatus = "Closed/Denied";
        r.Application.PhoneApplication = true;
        r.Application.PhoneApplicationReason = "No transportation this week";
        r.Application.AppealNotice = true;
        r.Application.Determination.Denied = true;
        r.Application.Determination.DenyOver200 = true;
        r.Application.Determination.DenyNoSfb = true;
        r.Application.Referrals.FinancialCoaching = true;
        r.IncomeExpenses.Income["Earned"] = 4200m;
        r.IncomeExpenses.Expenses["Rent"] = new() { PaidLast30Days = 1350m, Bill = 1350m };
        r.IncomeExpenses.Expenses["AutoLoan"] = new() { PaidLast30Days = 410m, Bill = 410m };
        r.IncomeExpenses.Expenses["CreditCard"] = new() { PaidLast30Days = 200m, Bill = 1900m };
        r.IncomeExpenses.IncomeChangeNextMonth = "No";
        r.IncomeExpenses.AssetSavings = 650m;
        r.IncomeExpenses.DocumentsAttached30Days = true;
        r.FuelElectric.FuelType = "Oil";
        return r;
    }

    public static IntakeRecord LargeHousehold(string first, string last, DateOnly today)
    {
        var r = Base(first, last, today);
        r.Client.DateOfBirth = new DateOnly(1975, 1, 30);
        r.Client.AddressMailing = "PO Box 000, St. Johnsbury, VT 05819";
        r.Client.Email = "family.example@example.org";
        r.Client.PrimaryLanguage = "Other";
        r.Client.LanguageOther = "Pretendish";
        r.Client.EnglishFluency = "Some";
        r.Client.TranslatorNeeded = "Y";
        r.Client.ServiceAddressLine1 = "14 Make Believe Ln";
        r.Client.HousingType = "Rent";
        r.Client.HouseholdType = "Multigenerational Household";
        var b = r.Client.Benefits;
        b.Snap = true; b.SnapAmount = 180m; b.SnapPeriod = "week"; b.SnapVerifiedDate = today;
        b.Wic = b.Liheap = b.HousingChoiceVoucher = b.ChildcareVoucher = b.AcaSubsidy = true;
        var i = r.Client.Interests;
        i.FoodClothing = i.HousingSupport = i.FuelAssistance = i.EconomicPrograms = i.YouthPrograms = i.FamilySupports = i.Tefap = true;
        i.Other = true; i.OtherText = "Car repair help";
        string[] names = ["Partner", "Elder", "Teen", "Twin A", "Twin B", "Kid", "Toddler", "Baby", "Cousin"];
        int[] years = [1977, 1950, 2010, 2014, 2014, 2017, 2022, 2025, 2003];
        r.Household =
        [
            new() { Gender = "Female", Disabled = "N", Education = "11th grade", Race = "White", Hispanic = "No", HealthInsurance = "Medicaid",
                    IncomePerMonth = 1500m, IncomeSource = "Employment", MilitaryStatus = "Never", Ssn = "000-22-1001",
                    Occupation = "Employed Part-time", IncomeVerifiedDate = today },
        ];
        for (var n = 0; n < names.Length; n++)
            r.Household.Add(new HouseholdMember
            {
                FirstName = names[n], LastName = last, DateOfBirth = new DateOnly(years[n], 6, 1 + n),
                Gender = n % 2 == 0 ? "Male" : "Female", Disabled = n == 1 ? "Y" : "N",
                Race = n == 8 ? "Two or more races" : "White", Hispanic = "No", HealthInsurance = n == 1 ? "Medicare" : "Medicaid",
                IncomePerMonth = n switch { 0 => 900m, 1 => 1100m, 8 => 600m, _ => null },
                IncomeSource = n switch { 0 => "Self-employment", 1 => "Social Security Retirement (SSA)", 8 => "Employment", _ => null },
                MilitaryStatus = n == 1 ? "Veteran" : "Never", Ssn = $"000-22-10{n + 2:00}",
                Occupation = years[n] > 2012 ? "N/A" : n switch { 0 => "Seasonal - Agricultural",
                                                                   1 => "Retired", 2 => "School Full Time", _ => "Employed full-time and in school part-time" },
                IncomeVerifiedDate = n is 0 or 1 or 8 ? today : null,
            });
        r.Application.CrisisType = "Low on Fuel";
        r.Application.CurrentQuantity = "Under 10%";
        r.Application.LifeThreatening = true;
        r.Application.SeasonalFuelStatus = "Active";
        r.Application.Circumstance = "Furnace repair and a large medical bill used the fuel money";
        r.Application.Referrals.Neto = r.Application.Referrals.WholeFamily = r.Application.Referrals.Vita = true;
        r.Application.Determination.CrisisEligible = true;
        r.Application.Determination.CrisisEligibleDate = today;
        r.Application.Determination.CrisisEligibleTime = new TimeOnly(10, 30);
        r.IncomeExpenses.Income["Earned"] = 3000m;
        r.IncomeExpenses.Income["SSA"] = 1100m;
        foreach (var line in IncomeExpenseCatalog.ExpenseLines.Take(12))
            r.IncomeExpenses.Expenses[line.Key] = new() { PaidLast30Days = 50m + line.Label.Length, Bill = 60m + line.Label.Length };
        r.IncomeExpenses.IncomeChangeNextMonth = "Yes";
        r.IncomeExpenses.IncomeChangeExplanation = "Seasonal work ends next month";
        r.IncomeExpenses.AssetChecking = 120m; r.IncomeExpenses.AssetCash = 40m;
        r.IncomeExpenses.Notes = "Cousin moved in last month and contributes to rent.";
        r.IncomeExpenses.DocumentsAttached30Days = true;
        var fe = r.FuelElectric;
        fe.FuelType = "Propane";
        fe.SeasonalFuelAmountGranted = 1650m; fe.SeasonalFuelDateGranted = today.AddDays(-60);
        fe.VendorAccount = "TEST-3003";
        fe.FuelOrder.Authorized = true; fe.FuelOrder.Date = today; fe.FuelOrder.Time = new TimeOnly(10, 45);
        fe.FuelOrder.SfbRemaining = 0m; fe.FuelOrder.TankSize = "500 gal"; fe.FuelOrder.FuelRemaining = "8%"; fe.FuelOrder.FuelLasts = "1 day";
        fe.FuelOrder.Fuel = 600m; fe.FuelOrder.Gallons = "150"; fe.FuelOrder.SpecialTrip = 150m; fe.FuelOrder.PressureTest = 100m;
        fe.FuelOrder.EstimatedDelivery = today.AddDays(1); fe.FuelOrder.FundingSource = "Crisis Fuel";
        fe.Invoice.Received = true; fe.Invoice.Fuel = 588.75m; fe.Invoice.Gallons = "147.2"; fe.Invoice.SpecialTrip = 150m;
        fe.Invoice.PressureTest = 100m; fe.Invoice.DeliveryDate = today.AddDays(1); fe.Invoice.FundingSource = "Crisis Fuel";
        return r;
    }

    public static IntakeRecord Multigenerational(string first, string last, DateOnly today)
    {
        var r = Base(first, last, today);
        r.Client.DateOfBirth = new DateOnly(1958, 9, 9);
        r.Client.HousingType = "Mobile Home/Trailer";
        r.Client.HouseholdType = "Multigenerational Household";
        r.Client.Benefits.Snap = true; r.Client.Benefits.SnapAmount = 350m; r.Client.Benefits.SnapPeriod = "month";
        r.Client.Benefits.SnapVerifiedDate = today;
        r.Household =
        [
            new() { Gender = "Female", Disabled = "Y", Race = "White", Hispanic = "No", HealthInsurance = "Medicare",
                    IncomePerMonth = 1240m, IncomeSource = "Social Security Disability (SSDI)", MilitaryStatus = "Never",
                    Ssn = "000-33-2001", Occupation = "Disabled", IncomeVerifiedDate = today },
            new() { FirstName = "Son", LastName = last, DateOfBirth = new DateOnly(1985, 2, 2), Gender = "Male", Disabled = "Y",
                    Race = "White", Hispanic = "No", HealthInsurance = "Medicaid", IncomePerMonth = 967m,
                    IncomeSource = "Supplemental Security Income (SSI)", MilitaryStatus = "Never", Ssn = "000-33-2002",
                    Occupation = "Unemployed (6 Months or More)", IncomeVerifiedDate = today },
            new() { FirstName = "Daughter-in-law", LastName = last, DateOfBirth = new DateOnly(1988, 7, 7), Gender = "Female",
                    Disabled = "N", Race = "Black or African American", Hispanic = "No", HealthInsurance = "Medicaid",
                    IncomePerMonth = 800m, IncomeSource = "Employment", MilitaryStatus = "Never", Ssn = "000-33-2003",
                    Occupation = "Seasonal - Non-agricultural", IncomeVerifiedDate = today },
            new() { FirstName = "Grandkid", LastName = last, DateOfBirth = new DateOnly(2012, 12, 12), Gender = "Non-binary",
                    Race = "Two or more races", Hispanic = "No", HealthInsurance = "State Children's Health Insurance Program",
                    Ssn = "000-33-2004", Occupation = "School Full Time" },
        ];
        r.Application.CrisisType = "Disconnect Notice";
        r.Application.ElectricForHeat = true;
        r.Application.ElectricComponent = "Pellet stove auger and fan";
        r.Application.SeasonalFuelStatus = "Active";
        r.Application.Circumstance = "Son's SSI was delayed two months";
        r.IncomeExpenses.Income["SSDI"] = 1240m;
        r.IncomeExpenses.Income["SSI"] = 967m;
        r.IncomeExpenses.Income["Earned"] = 800m;
        r.IncomeExpenses.Expenses["Rent"] = new() { PaidLast30Days = 450m, Bill = 450m };
        r.IncomeExpenses.Expenses["Electricity"] = new() { PaidLast30Days = 0m, Bill = 612.40m };
        r.IncomeExpenses.Expenses["Medical"] = new() { PaidLast30Days = 95m };
        r.IncomeExpenses.IncomeChangeNextMonth = "Yes";
        r.IncomeExpenses.IncomeChangeExplanation = "SSI back pay expected";
        r.FuelElectric.FuelType = "Pellet";
        r.FuelElectric.ElectricAccount = "TEST-4004";
        var p = r.FuelElectric.ElectricPledge;
        p.Authorized = true; p.Date = today; p.Time = new TimeOnly(14, 5); p.DisconnectNoticeAttached = true;
        p.AmountRequired = 450m; p.ReconnectionFee = 45m; p.DiscountProgram = "Yes"; p.DisconnectDate = today.AddDays(3);
        p.FundingSource = "Crisis Fuel"; p.RemainingBalance = 162.40m; p.NextBill = 140m; p.NextBillDue = today.AddDays(30);
        p.ClientResponsibilities = "Pay remaining $162.40 by the next bill date; enroll in the discount program.";
        return r;
    }

    public static IntakeRecord ShelterKerosene(string first, string last, DateOnly today)
    {
        var r = Base(first, last, today);
        r.Client.DateOfBirth = new DateOnly(1969, 11, 1);
        r.Client.AddressPhysical = "Pretend Motel, Room 7";
        r.Client.HousingType = "Hotel/Motel Room";
        r.Client.HouseholdType = "Two Adults NO Children";
        r.Household =
        [
            new() { Gender = "Female", Disabled = "N", Race = "American Indian or Alaska Native", Hispanic = "No",
                    HealthInsurance = "None", IncomePerMonth = 0m, IncomeSource = "None", MilitaryStatus = "Never",
                    Ssn = "000-44-3001", Occupation = "Unemployed", IncomeVerifiedDate = today },
            new() { FirstName = "Friend", LastName = "Example", DateOfBirth = new DateOnly(1971, 4, 4), Gender = "Male", Disabled = "N",
                    Race = "White", Hispanic = "Yes", HealthInsurance = "Medicaid", IncomePerMonth = 720m,
                    IncomeSource = "Unemployment", MilitaryStatus = "Veteran", Ssn = "000-44-3002", Occupation = "Unemployed",
                    IncomeVerifiedDate = today },
        ];
        r.Application.CrisisType = "Low on Fuel";
        r.Application.AfterHours = true;
        r.Application.SecondCrisis = true;
        r.Application.CurrentQuantity = "Empty";
        r.Application.SeasonalFuelStatus = "Pending";
        r.Application.Circumstance = "Lost housing; moving to a rental with a kerosene heater";
        r.Application.Referrals.CommunityConnections = true;
        r.IncomeExpenses.Income["Unemployment"] = 720m;
        r.IncomeExpenses.Expenses["Phone"] = new() { PaidLast30Days = 45m, Bill = 45m };
        r.FuelElectric.FuelType = "Kerosene";
        r.FuelElectric.FuelOrder.Authorized = true; r.FuelElectric.FuelOrder.Date = today;
        r.FuelElectric.FuelOrder.TankSize = "55 gal drum"; r.FuelElectric.FuelOrder.FuelRemaining = "Empty";
        r.FuelElectric.FuelOrder.Fuel = 210m; r.FuelElectric.FuelOrder.Gallons = "45";
        return r;
    }

    /// <summary>Deliberately wrong entries, to practise with the Review tab.</summary>
    public static IntakeRecord NeedsFixing(string first, string last, DateOnly today)
    {
        var r = SingleSenior(first, last, today);
        r.Client.Phone = "555-0142";
        r.Client.Zip = "5819";
        r.Household[0].Ssn = "12-345";
        r.Household[0].IncomePerMonth = 1600m; // worksheet says 1,480
        r.Application.CrisisType = null;
        r.Staff.AppDate = null;
        return r;
    }
}
