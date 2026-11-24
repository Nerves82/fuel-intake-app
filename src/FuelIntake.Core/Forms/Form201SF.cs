using System.Globalization;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Forms;

/// <summary>A fillable box on a state PDF form: 1-based page, position and size in points from the top left.</summary>
public sealed record FormSpot(int Page, double X, double Y, double W, double H, bool IsBox, string Name);

/// <summary>A value placed on a form: text in a line, or an X in a checkbox (<see cref="Text"/> null).</summary>
public sealed record FormMark(FormSpot Spot, string? Text)
{
    public bool IsCheck => Text is null;

    /// <summary>Font size in points: 9, smaller for long text so it fits the line (never below 5.5).</summary>
    public double FontSize => Text is null ? 9 : Math.Clamp(Spot.W / Math.Max(1, Text.Length * 0.5), 5.5, 9);
}

/// <summary>The filled-in 201SF: marks for its three pages, and household members past the four rows on page 1.</summary>
public sealed record Form201SFFill(IReadOnlyList<FormMark> Marks, IReadOnlyList<MemberView> ExtraMembers);

/// <summary>
/// DCF form 201SF, Vermont Fuel Assistance Eligibility Review (rev 01/2024), filled from the intake.
/// Only answers the intake clearly gives are filled in, plus the "VT Eligibility Review" tab's answers (relationships,
/// roomers, caregivers, students, bedrooms, jobs and pay dates…); signatures are left for the client and staff.
/// </summary>
public static class Form201SF
{
    public const string Title = "Vermont Fuel Assistance Eligibility Review (201SF)";
    public const string AgencyName = "Northeast Kingdom Community Action (NEKCA)";

    /// <summary>Rows on page 1 (the client plus three more).</summary>
    public const int MemberRows = 4;

    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static Form201SFFill Fill(IntakeRecord r, IntakeCalculations c)
    {
        var marks = new List<FormMark>();
        void Text(string field, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) marks.Add(new(Spot(field), value.Trim()));
        }
        void Check(string field, bool on = true)
        {
            if (on) marks.Add(new(Spot(field), null));
        }

        // ---- page 1: client
        var members = c.Members.Where(m => m.IsPresent || m.Number == 1).ToList();
        var client = members[0];
        Text("Page1[0].Name[0]", c.ClientFullName);
        Text("Page1[0].Social_Security_Number[0]", Formatting.Ssn.Format(client.Data.Ssn));
        var physical = Join(r.Client.AddressPhysical, c.CityStateZip);
        var mailing = T(r.Client.AddressMailing);
        if (mailing.Length > 0 && !string.Equals(mailing, physical, StringComparison.OrdinalIgnoreCase))
        {
            Text("Page1[0].Mailing_Address_Street_PO_Box_Town_State_and_Zip[0]", mailing);
            Text("Page1[0].Physical_Address_if_Different__Street_House_Number_Town_State_and_Zip[0]", physical);
        }
        else Text("Page1[0].Mailing_Address_Street_PO_Box_Town_State_and_Zip[0]", physical);
        Text("Page1[0].Home_Phone_with_area_code[0]", Formatting.Phone.Format(r.Client.Phone));
        var x = r.StateReview;
        Text("Page1[0].Day__Message_Phone_with_area_code[0]", Formatting.Phone.Format(x.DayPhone));

        // ---- page 1: household, four rows
        for (var i = 0; i < Math.Min(MemberRows, members.Count); i++)
        {
            var m = members[i];
            var row = Rows[i];
            Text(row.Name, m.FullName);
            Text(row.Ssn, Formatting.Ssn.Format(m.Data.Ssn));
            Text(row.Dob, D(m.DateOfBirth));
            var b = Blocks[i];
            var who = Demographics(m.Data);
            Check(Box(b[0]), who.Hispanic == true);
            Check(Box(b[1]), who.Hispanic == false);
            Check(Box(b[2]), who.Gender == "Male");
            Check(Box(b[3]), who.Gender == "Female");
            Check(Box(b[4]), who.Race == RaceBox.Asian);
            Check(Box(b[5]), who.Race == RaceBox.Black);
            Check(Box(b[6]), who.Race == RaceBox.PacificIslander);
            Check(Box(b[7]), who.Race == RaceBox.AmericanIndian);
            Check(Box(b[8]), who.Race == RaceBox.White);
            if (CitizenshipBox(i, m.Data.Citizenship) is int box) Check(Box(box));
            Text(IdFields[i], m.Data.ImmigrationId);
            if (i > 0) Text(Relationships[i - 1], m.Data.Relationship);
        }

        // ---- page 2 (the VT Eligibility Review tab's answers come first; otherwise what the intake says)
        // 1. roomer
        YesNo("Page2[0].RadioButtonList[0]", x.RentsRoom);
        Text("Page2[0].Roomer[0]", x.RoomerName);
        Text("Page2[0].Rent[0]", M(x.RoomerRent));
        // 2. live-in caregiver
        YesNo("Page2[0].RadioButtonList[1]", x.Caregiver);
        Text("Page2[0].If_yes_name[0]", x.CaregiverName);
        Check("Page2[0].CheckBox1[0]", x.CareMedical);
        Check("Page2[0].CheckBox1[1]", x.CareHomemaker);
        // 3. full-time college student
        YesNo("Page2[0].RadioButtonList[2]", x.Student);
        Text("Page2[0].If_yes_name_2[0]", x.StudentName);
        Text("Page2[0].Classes[0]", x.StudentLivesAt);
        // 4. living situation (rent: the answer, else the Mortgage/Rent bill on the worksheet)
        var housing = T(r.Client.HousingType);
        var living = T(x.LivingSituation) is { Length: > 0 } picked ? picked
            : housing == "Own" ? "Own my home" : housing == "Rent" ? "Rent my home or apartment" : "";
        switch (living)
        {
            case "Own my home": Check("Page2[0].CheckBox1[2]"); break;
            case "Rent my home or apartment":
                Check("Page2[0].CheckBox1[3]");
                var rent = x.RentAmount ?? (r.IncomeExpenses.Expenses.TryGetValue("Rent", out var e) && e.Bill is > 0 ? e.Bill : null);
                Text("Page2[0].I_rent_my_home_or_apartment_and_pay[0]", M(rent));
                break;
            case "Life lease": Check("Page2[0].CheckBox1[5]"); break;
            case "Rent a room in someone's home":
                Check("Page2[0].CheckBox1[4]");
                Text("Page2[0].I_rent_a_room_in_the_home_of[0]", x.RoomHost);
                Text("Page2[0].and_pay[0]", M(x.RoomRent));
                break;
            case "Other":
                Check("Page2[0].CheckBox1[6]");
                Text("Page2[0].Other[0]", x.LivingOther);
                break;
        }
        // 5. who pays for heat
        var heat = T(x.HeatPayment) is { Length: > 0 } hp ? hp : r.Application.HeatInRent ? "Heat is included in my rent" : "";
        Check("Page2[0].CheckBox1[7]", heat == "Heat is included in my rent");
        Check("Page2[0].CheckBox1[8]", heat == "I pay my fuel supplier directly");
        Check("Page2[0].CheckBox1[9]", heat == "My landlord bills me for all fuel");
        // 6. type of housing
        var kind = HousingKindOf(r);
        Check("Page2[0].CheckBox1[10]", kind == "Single-family house");
        Check("Page2[0].CheckBox1[11]", kind == "Mobile home");
        Check("Page2[0].CheckBox1[12]", kind == "Apartment");
        if (kind == "Other")
        {
            Check("Page2[0].CheckBox1[13]");
            Text("Page2[0].Type_of_housing[0]", x.HousingOther);
        }
        // 7. bedrooms
        Text("Page2[0].Bedrooms[0]", x.Bedrooms);
        // 8. main heating fuel
        if (FuelBoxes.TryGetValue(T(r.FuelElectric.FuelType), out var fuelBox)) Check(fuelBox);
        else if (T(x.MainFuelOther) == "Natural gas") Check("Page2[0].CheckBox1[19]");
        else if (T(x.MainFuelOther) == "Coal") Check("Page2[0].CheckBox1[21]");
        // 9. rent based on income: the answer, else from the housing benefits on the client tab (No is never assumed)
        var b2 = r.Client.Benefits;
        var program = T(x.RentProgram) is { Length: > 0 } rp ? rp
            : b2.HousingChoiceVoucher || b2.HudVash ? "Section 8" : b2.PublicHousing ? "Public housing" : b2.PermanentSupportiveHousing ? "Subsidized housing" : "";
        var rentByIncome = T(x.RentBasedOnIncome) is { Length: > 0 } rb ? rb : program.Length > 0 ? "Yes" : "";
        YesNo("Page2[0].RadioButtonList[3]", rentByIncome);
        if (rentByIncome == "Yes")
        {
            Check("Page2[0].CheckBox1[22]", program == "Section 8");
            Check("Page2[0].CheckBox1[23]", program == "Public housing");
            Check("Page2[0].CheckBox1[24]", program == "Subsidized housing");
            Check("Page2[0].CheckBox1[25]", program == "Other");
            if (program == "Other") Text("Page2[0].Other_2[0]", x.RentProgramOther);
        }
        // 10, 11. fuel supplier and electric provider
        Text("Page2[0].FuelSupplier[0]", c.FuelVendor?.Name ?? r.FuelElectric.Vendor);
        Text("Page2[0].Address[0]", x.SupplierAddress);
        Text("Page2[0].Phone_number[0]", c.FuelVendor is null ? "" : c.VendorPhones);
        Text("Page2[0].Name_on_account[0]", x.SupplierAccountName);
        Text("Page2[0].Account_number[0]", r.FuelElectric.VendorAccount);
        Text("Page2[0].ElectricProvider[0]", c.ElectricVendor?.Name ?? r.FuelElectric.ElectricProvider);
        Text("Page2[0].Name_on_account_2[0]", x.ElectricAccountName);
        Text("Page2[0].Account_number_2[0]", r.FuelElectric.ElectricAccount);

        // ---- page 3
        // 13. jobs
        Job(Jobs[0], x.Job1);
        Job(Jobs[1], x.Job2);
        // 14. self-employment: the answer, else Yes when someone's income source is self-employment (No is never assumed)
        YesNo("Page3[0].RadioButtonList[0]", T(x.SelfEmployment) is { Length: > 0 } se ? se
            : members.Any(m => T(m.Data.IncomeSource) == "Self-employment") ? "Yes" : "");
        // 15. unearned income: the worksheet's monthly amounts, with the names of members who have that source,
        //     plus interest, adoption subsidy and rental income from the VT Eligibility Review tab
        var anyUnearned = false;
        foreach (var line in Unearned)
        {
            var amount = line.Keys.Sum(k => r.IncomeExpenses.Income.TryGetValue(k, out var v) ? v ?? 0m : 0m);
            var names = members.Where(m => line.Sources.Contains(T(m.Data.IncomeSource))).Select(m => m.FullName).Where(n => n.Length > 0).ToList();
            if (amount <= 0 && names.Count == 0) continue;
            anyUnearned = true;
            var who = string.Join(", ", names);
            if (line.Keys.Contains("Other") && !string.IsNullOrWhiteSpace(r.IncomeExpenses.IncomeOtherDescription))
                who = T(r.IncomeExpenses.IncomeOtherDescription) + (who.Length > 0 ? " - " + who : "");
            Text(line.NameField, who);
            if (amount > 0) Text(line.AmountField, M(amount));
        }
        foreach (var (names, amount, nameField, amountField) in new[]
        {
            (x.InterestNames, x.InterestAmount, "Page3[0].Name_sInterestDividends[0]", "Page3[0].fill_86[0]"),
            (x.AdoptionNames, x.AdoptionAmount, "Page3[0].Name_sAdoption_Subsidy[0]", "Page3[0].fill_88[0]"),
            (x.RentalNames, x.RentalAmount, "Page3[0].Name_sRental_Income[0]", "Page3[0].fill_89[0]"),
        })
        {
            if (T(names).Length == 0 && amount is not > 0) continue;
            anyUnearned = true;
            Text(nameField, names);
            Text(amountField, M(amount));
        }
        if (anyUnearned) Check(Radio("Page3[0].RadioButtonList[1]", yes: true));

        void YesNo(string radio, string? answer)
        {
            if (T(answer) == "Yes") Check(Radio(radio, yes: true));
            else if (T(answer) == "No") Check(Radio(radio, yes: false));
        }

        void Job(JobFields f, StateJob j)
        {
            Text(f.FirstName, j.FirstName);
            Text(f.Initial, j.Initial);
            var often = Array.IndexOf(Lists.ChoiceLists.PayFrequency.ToArray(), T(j.HowOften));
            if (often >= 0) Check(Box3(f.OftenBoxes[often]));
            if (T(j.HowOften) == "Other") Text(f.OftenOther, j.HowOftenOther);
            Text(f.Employer, j.Employer);
            Text(f.EmployerPhone, Formatting.Phone.Format(j.EmployerPhone));
            for (var k = 0; k < Math.Min(PayRows, j.Pay.Count); k++)
            {
                var pay = j.Pay[k];
                var row = f.Rows[k];
                Text(row[0], D(pay.DatePaid));
                Text(row[1], pay.Hours);
                Text(row[2], M(pay.HourlyRate));
                Text(row[3], M(pay.Gross));
                Text(row[4], M(pay.Tips));
            }
        }

        // signature block. The PDF's field names don't match the lines they sit on: these are by position.
        Text(HelperName, r.Staff.StaffName);
        Text(HelperPhone, Formatting.Phone.Format(c.StaffPhone));
        Text(HelperAgency, string.IsNullOrWhiteSpace(r.Staff.StaffName) ? "" : AgencyName);

        return new(marks, members.Skip(MemberRows).ToList());
    }

    // ------------------------------------------------------------------ demographics

    public enum RaceBox { Asian, White, Black, PacificIslander, AmericanIndian }

    public sealed record Demographic(bool? Hispanic, string? Gender, RaceBox? Race);

    /// <summary>The page 1 boxes for a member: Hispanic (Yes/No), gender (Male/Female only), race (single races only).</summary>
    public static Demographic Demographics(HouseholdMember m)
    {
        var race = T(m.Race);
        bool? hispanic = T(m.Hispanic) switch
        {
            "Yes" => true,
            "No" => false,
            _ => race is "Hispanic or Latino" or "Hispanic/Latino" ? true : null,
        };
        RaceBox? box = race switch
        {
            "Asian" => RaceBox.Asian,
            "White" => RaceBox.White,
            "Black or African American" => RaceBox.Black,
            "Native Hawaiian/Pacific Islander" => RaceBox.PacificIslander,
            "American Indian or Alaska Native" => RaceBox.AmericanIndian,
            _ => null,
        };
        var gender = T(m.Gender) is "Male" or "Female" ? T(m.Gender) : null;
        return new(hispanic, gender, box);
    }

    // ------------------------------------------------------------------ where things are

    private sealed record Row(string Name, string Ssn, string Dob);

    private static readonly Row[] Rows =
    [
        new("Page1[0].Name[1]", "Page1[0].SSNSELF[0]", "Page1[0].Date_of_BirthSELF[0]"),
        new("Page1[0].NameRow1[0]", "Page1[0].SSNRow1[0]", "Page1[0].Date_of_BirthRow1[0]"),
        new("Page1[0].NameRow1_2[0]", "Page1[0].SSNRow1_2[0]", "Page1[0].Date_of_BirthRow1_2[0]"),
        new("Page1[0].NameRow1_3[0]", "Page1[0].SSNRow1_3[0]", "Page1[0].Date_of_BirthRow1_3[0]"),
    ];

    /// <summary>
    /// Page 1 checkbox numbers per member block: Hispanic, Not Hispanic, Male, Female, Asian, Black,
    /// Native Hawaiian/Pacific Islander, American Indian/Alaska Native, White.
    /// </summary>
    public static readonly int[][] Blocks =
    [
        [0, 1, 2, 3, 4, 5, 6, 7, 8],
        [14, 15, 16, 17, 18, 19, 20, 21, 22],
        [28, 29, 30, 31, 32, 33, 34, 35, 36],
        [42, 43, 44, 45, 46, 47, 48, 49, 50],
    ];

    private static string Box(int n) => $"Page1[0].CheckBox1[{n}]";

    /// <summary>The "ID#" line under each row's citizenship boxes.</summary>
    private static readonly string[] IdFields = ["Page1[0].ID[0]", "Page1[0].ID_2[0]", "Page1[0].ID_2[1]", "Page1[0].ID_2[2]"];

    /// <summary>Relationship to the client, rows 2–4 (row 1 is printed "SELF").</summary>
    private static readonly string[] Relationships = ["Page1[0].RelationshipRow1[0]", "Page1[0].RelationshipRow1_2[0]", "Page1[0].RelationshipRow1_3[0]"];

    /// <summary>Pay dates per job on page 3.</summary>
    public const int PayRows = 5;

    private static string Box3(int n) => $"Page3[0].CheckBox1[{n}]";

    /// <summary>A job box on page 3. <see cref="OftenBoxes"/> in <c>ChoiceLists.PayFrequency</c> order; each row: date, hours, rate, gross, tips.</summary>
    private sealed record JobFields(string FirstName, string Initial, int[] OftenBoxes, string OftenOther, string Employer, string EmployerPhone, string[][] Rows);

    private static readonly JobFields[] Jobs =
    [
        new("Page3[0].FirstName[0]", "Page3[0].Initial[0]", [0, 1, 3, 2, 4], "Page3[0].HowOften[0]", "Page3[0].EmployerName[0]", "Page3[0].Employer_Phone[0]",
        [
            ["Page3[0].Date_paidRow1[0]", "Page3[0].Hours_WorkedRow1[0]", "Page3[0].fill_46[0]", "Page3[0].fill_47[0]", "Page3[0].fill_48[0]"],
            ["Page3[0].Date_paidRow2[0]", "Page3[0].Hours_WorkedRow2[0]", "Page3[0].fill_49[0]", "Page3[0].fill_50[0]", "Page3[0].fill_51[0]"],
            ["Page3[0].Date_paidRow3[0]", "Page3[0].Hours_WorkedRow3[0]", "Page3[0].fill_52[0]", "Page3[0].fill_53[0]", "Page3[0].fill_54[0]"],
            ["Page3[0].Date_paidRow4[0]", "Page3[0].Hours_WorkedRow4[0]", "Page3[0].fill_56[0]", "Page3[0].fill_57[0]", "Page3[0].fill_58[0]"],
            ["Page3[0].Date_paidRow5[0]", "Page3[0].Hours_WorkedRow5[0]", "Page3[0].fill_59[0]", "Page3[0].fill_60[0]", "Page3[0].fill_61[0]"],
        ]),
        new("Page3[0].FirstName[1]", "Page3[0].Initial[1]", [5, 6, 8, 7, 9], "Page3[0].HowOften[1]", "Page3[0].EmployerName[1]", "Page3[0].Employer_Phone[1]",
        [
            ["Page3[0].Date_paidRow1_2[0]", "Page3[0].Hours_WorkedRow1_2[0]", "Page3[0].fill_63[0]", "Page3[0].fill_64[0]", "Page3[0].fill_65[0]"],
            ["Page3[0].Date_paidRow2_2[0]", "Page3[0].Hours_WorkedRow2_2[0]", "Page3[0].fill_66[0]", "Page3[0].fill_67[0]", "Page3[0].fill_68[0]"],
            ["Page3[0].Date_paidRow3_2[0]", "Page3[0].Hours_WorkedRow3_2[0]", "Page3[0].fill_69[0]", "Page3[0].fill_70[0]", "Page3[0].fill_71[0]"],
            ["Page3[0].Date_paidRow4_2[0]", "Page3[0].Hours_WorkedRow4_2[0]", "Page3[0].fill_73[0]", "Page3[0].fill_74[0]", "Page3[0].fill_75[0]"],
            ["Page3[0].Date_paidRow5_2[0]", "Page3[0].Hours_WorkedRow5_2[0]", "Page3[0].fill_76[0]", "Page3[0].fill_77[0]", "Page3[0].fill_78[0]"],
        ]),
    ];

    /// <summary>
    /// The page 1 checkbox for a member row's citizenship. Each row's five citizenship boxes are the ones after its
    /// nine demographic boxes; on the page they run U.S. citizen, Asylee, Legal immigrant down the left column, then
    /// Refugee, Other down the right (their numbers don't follow that order, so they're sorted by position).
    /// </summary>
    public static int? CitizenshipBox(int row, string? citizenship)
    {
        var order = Array.IndexOf(Lists.ChoiceLists.Citizenship.ToArray(), citizenship?.Trim());
        if (order < 0 || row < 0 || row >= MemberRows) return null;
        var boxes = Enumerable.Range(row * 14 + 9, 5)
            .OrderBy(n => Math.Round(Spot(Box(n)).X / 20)).ThenBy(n => Spot(Box(n)).Y).ToList();
        return boxes[order];
    }

    private static readonly Dictionary<string, string> FuelBoxes = new()
    {
        ["Wood"] = "Page2[0].CheckBox1[14]",
        ["Pellet"] = "Page2[0].CheckBox1[15]",
        ["Electric"] = "Page2[0].CheckBox1[16]",
        ["Oil"] = "Page2[0].CheckBox1[17]",
        ["Propane"] = "Page2[0].CheckBox1[18]",
        ["Kerosene"] = "Page2[0].CheckBox1[20]",
    };

    private sealed record UnearnedLine(string NameField, string AmountField, string[] Keys, string[] Sources);

    /// <summary>Question 15 rows the intake can answer: worksheet income keys and member income sources.</summary>
    private static readonly UnearnedLine[] Unearned =
    [
        new("Page3[0].Name_sSocial_Security[0]", "Page3[0].fill_80[0]", ["SSA", "SSDI"], ["Social Security Retirement (SSA)", "Social Security Disability (SSDI)"]),
        new("Page3[0].Name_sSupplemental_Security_Income_SSI[0]", "Page3[0].fill_81[0]", ["SSI"], ["Supplemental Security Income (SSI)"]),
        new("Page3[0].Name_sVeterans_Benefits[0]", "Page3[0].fill_82[0]", ["VA"], ["VA Disability"]),
        new("Page3[0].Name_sUnemployment_Compensation[0]", "Page3[0].fill_83[0]", ["Unemployment"], ["Unemployment"]),
        new("Page3[0].Name_sWorkers_Compensation[0]", "Page3[0].fill_84[0]", ["WorkersComp"], ["Worker's Compensation"]),
        new("Page3[0].Name_sChild_Support_andor_Alimony[0]", "Page3[0].fill_85[0]", ["ChildSupport", "Alimony"], ["Child Support", "Alimony/Spousal Support"]),
        new("Page3[0].Name_sRetirement[0]", "Page3[0].fill_87[0]", ["Pension"], ["Pension/Retirement"]),
        new("Page3[0].Name_sOther[0]", "Page3[0].fill_90[0]", ["Other"], ["Other"]),
    ];

    // Signature block, by position (see Fill).
    public const string HelperName = "Page3[0].Signature_of_applicant[0]";
    public const string HelperPhone = "Page3[0].Name_of_person_helping_fill_out_this_form_printed[0]";
    public const string HelperAgency = "Page3[0].Signature[0]";

    /// <summary>The spot for a PDF field name, or "name#yes" / "name#no" for one side of a Yes/No pair.</summary>
    public static FormSpot Spot(string name) => name.Contains('#') ? ResolveRadio(name)
        : Form201SFLayout.Spots.FirstOrDefault(s => s.Name == name) ?? throw new InvalidOperationException($"201SF has no field {name}");

    /// <summary>A Yes/No pair shares one name; Yes is the left one.</summary>
    private static string Radio(string name, bool yes) => name + (yes ? "#yes" : "#no");

    private static FormSpot ResolveRadio(string key)
    {
        var name = key[..key.IndexOf('#')];
        var pair = Form201SFLayout.Spots.Where(s => s.Name == name).OrderBy(s => s.X).ToList();
        return key.EndsWith("#yes") ? pair[0] : pair[^1];
    }

    /// <summary>Question 6: the answer on the eligibility review tab, else what tab 1's housing type says (a typed "other" with no pick counts as Other).</summary>
    public static string HousingKindOf(IntakeRecord r)
    {
        var x = r.StateReview;
        if (T(x.HousingKind) is { Length: > 0 } picked) return picked;
        if (T(x.HousingOther).Length > 0) return "Other";
        return T(r.Client.HousingType) switch
        {
            "House" => "Single-family house",
            "Mobile Home/Trailer" => "Mobile home",
            "Apartment" => "Apartment",
            _ => "",
        };
    }

    private static string T(string? s) => s?.Trim() ?? "";
    private static string Join(string? street, string cityStateZip) =>
        string.Join(", ", new[] { T(street), cityStateZip }.Where(x => x.Length > 0));
    private static string D(DateOnly? d) => d?.ToString("MM/dd/yyyy", Us) ?? "";
    private static string M(decimal? m) => m?.ToString("#,##0.00", Us) ?? "";
}
