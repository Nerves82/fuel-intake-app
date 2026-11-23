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
/// Only answers the intake clearly gives are filled in; the rest (relationships, citizenship, pay stubs,
/// roomers, caregivers, students, bedrooms, signatures) is left for the client and staff to complete.
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
        }

        // ---- page 2
        // 4. living situation (rent: the Mortgage/Rent bill on the worksheet)
        var housing = T(r.Client.HousingType);
        if (housing == "Own") Check("Page2[0].CheckBox1[2]");
        if (housing == "Rent")
        {
            Check("Page2[0].CheckBox1[3]");
            if (r.IncomeExpenses.Expenses.TryGetValue("Rent", out var rent) && rent.Bill is > 0)
                Text("Page2[0].I_rent_my_home_or_apartment_and_pay[0]", M(rent.Bill));
        }
        // 5. heat included in rent
        Check("Page2[0].CheckBox1[7]", r.Application.HeatInRent);
        // 6. type of housing
        Check("Page2[0].CheckBox1[10]", housing == "House");
        Check("Page2[0].CheckBox1[11]", housing == "Mobile Home/Trailer");
        Check("Page2[0].CheckBox1[12]", housing == "Apartment");
        // 8. main heating fuel
        if (FuelBoxes.TryGetValue(T(r.FuelElectric.FuelType), out var fuelBox)) Check(fuelBox);
        // 9. rent based on income: from the housing benefits on the client tab (No is never assumed)
        var b2 = r.Client.Benefits;
        var section8 = b2.HousingChoiceVoucher || b2.HudVash;
        if (section8 || b2.PublicHousing || b2.PermanentSupportiveHousing)
        {
            Check(Radio("Page2[0].RadioButtonList[3]", yes: true));
            Check("Page2[0].CheckBox1[22]", section8);
            Check("Page2[0].CheckBox1[23]", b2.PublicHousing);
            Check("Page2[0].CheckBox1[24]", b2.PermanentSupportiveHousing);
        }
        // 10, 11. fuel supplier and electric provider
        Text("Page2[0].FuelSupplier[0]", c.FuelVendor?.Name ?? r.FuelElectric.Vendor);
        Text("Page2[0].Phone_number[0]", c.FuelVendor is null ? "" : c.VendorPhones);
        Text("Page2[0].Account_number[0]", r.FuelElectric.VendorAccount);
        Text("Page2[0].ElectricProvider[0]", c.ElectricVendor?.Name ?? r.FuelElectric.ElectricProvider);
        Text("Page2[0].Account_number_2[0]", r.FuelElectric.ElectricAccount);

        // ---- page 3
        // 14. self-employment (Yes only; No is never assumed)
        if (members.Any(m => T(m.Data.IncomeSource) == "Self-employment"))
            Check(Radio("Page3[0].RadioButtonList[0]", yes: true));
        // 15. unearned income: the worksheet's monthly amounts, with the names of members who have that source
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
        if (anyUnearned) Check(Radio("Page3[0].RadioButtonList[1]", yes: true));

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

    private static string T(string? s) => s?.Trim() ?? "";
    private static string Join(string? street, string cityStateZip) =>
        string.Join(", ", new[] { T(street), cityStateZip }.Where(x => x.Length > 0));
    private static string D(DateOnly? d) => d?.ToString("MM/dd/yyyy", Us) ?? "";
    private static string M(decimal? m) => m?.ToString("#,##0.00", Us) ?? "";
}
