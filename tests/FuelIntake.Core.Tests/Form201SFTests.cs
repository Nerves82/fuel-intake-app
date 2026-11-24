using FuelIntake.Core.Forms;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Tests;

public class Form201SFTests
{
    private static Form201SFFill Fill(IntakeRecord r) => Form201SF.Fill(r, new IntakeCalculations(r, FakeIntake.Context()));

    private static string? TextAt(Form201SFFill f, string field) =>
        f.Marks.FirstOrDefault(m => m.Spot == Form201SF.Spot(field) && !m.IsCheck)?.Text;

    private static bool Ticked(Form201SFFill f, string field) =>
        f.Marks.Any(m => m.Spot == Form201SF.Spot(field) && m.IsCheck);

    [Fact]
    public void Layout_HasEveryFieldTheFillUses_OnLetterPages()
    {
        Assert.Equal(238, Form201SFLayout.Spots.Count);
        Assert.All(Form201SFLayout.Spots, s =>
        {
            Assert.InRange(s.Page, 1, 3);
            Assert.InRange(s.X, 0, 612); Assert.InRange(s.Y, 0, 792);
            Assert.True(s.X + s.W <= 612 && s.Y + s.H <= 792);
        });
        // a full record touches most of the fields; Spot() throws for any name that isn't on the form
        var f = Fill(Full());
        Assert.True(f.Marks.Count >= 30, $"{f.Marks.Count} marks");
    }

    [Fact]
    public void Page1_ClientAddressAndHousehold()
    {
        var f = Fill(FakeIntake.Family());
        Assert.Equal("Testy Sampleton", TextAt(f, "Page1[0].Name[0]"));
        Assert.Equal("000-12-3456", TextAt(f, "Page1[0].Social_Security_Number[0]"));
        Assert.Equal("1 Example Rd, St. Johnsbury, VT 05819", TextAt(f, "Page1[0].Mailing_Address_Street_PO_Box_Town_State_and_Zip[0]"));
        Assert.Null(TextAt(f, "Page1[0].Physical_Address_if_Different__Street_House_Number_Town_State_and_Zip[0]"));
        Assert.Equal("(802) 555-0199", TextAt(f, "Page1[0].Home_Phone_with_area_code[0]"));
        Assert.Equal("Testy Sampleton", TextAt(f, "Page1[0].Name[1]"));
        Assert.Equal("11/17/1980", TextAt(f, "Page1[0].Date_of_BirthSELF[0]"));
        Assert.Equal("Demo Sampleton", TextAt(f, "Page1[0].NameRow1[0]"));
        Assert.Equal("Kid Sampleton", TextAt(f, "Page1[0].NameRow1_2[0]"));
        Assert.Null(TextAt(f, "Page1[0].NameRow1_3[0]"));
        Assert.Empty(f.ExtraMembers);
    }

    [Fact]
    public void Page1_SeparateMailingAddress_FillsBoth()
    {
        var r = FakeIntake.Family();
        r.Client.AddressMailing = "PO Box 99, St. Johnsbury, VT 05819";
        var f = Fill(r);
        Assert.Equal("PO Box 99, St. Johnsbury, VT 05819", TextAt(f, "Page1[0].Mailing_Address_Street_PO_Box_Town_State_and_Zip[0]"));
        Assert.Equal("1 Example Rd, St. Johnsbury, VT 05819", TextAt(f, "Page1[0].Physical_Address_if_Different__Street_House_Number_Town_State_and_Zip[0]"));
    }

    [Fact]
    public void Page1_Demographics_TickTheMatchingBoxes()
    {
        var r = FakeIntake.Family();
        r.Household[0].Hispanic = "No"; r.Household[0].Gender = "Female"; r.Household[0].Race = "White";
        r.Household[1].Hispanic = "N/A"; r.Household[1].Race = "Hispanic/Latino"; r.Household[1].Gender = "Non-binary";
        r.Household[2].Race = "Two or more races"; r.Household[2].Gender = "Male";
        var f = Fill(r);
        var b = Form201SF.Blocks;
        string Box(int n) => $"Page1[0].CheckBox1[{n}]";
        Assert.True(Ticked(f, Box(b[0][1])));  // not Hispanic
        Assert.True(Ticked(f, Box(b[0][3])));  // female
        Assert.True(Ticked(f, Box(b[0][8])));  // white
        Assert.True(Ticked(f, Box(b[1][0])));  // Hispanic, from the race
        Assert.False(b[1][2..].Any(n => Ticked(f, Box(n)))); // no gender or race box for non-binary / Hispanic race
        Assert.True(Ticked(f, Box(b[2][2])));  // male
        Assert.False(b[2][4..].Any(n => Ticked(f, Box(n)))); // two or more races: no single box
        Assert.False(b[3].Any(n => Ticked(f, Box(n))));      // nobody in row 4
    }

    [Fact]
    public void Page1_BoxBlocks_LineUpWithTheirRows()
    {
        // each block's boxes sit below its own name row and above the next one
        string[] rows = ["Page1[0].Name[1]", "Page1[0].NameRow1[0]", "Page1[0].NameRow1_2[0]", "Page1[0].NameRow1_3[0]"];
        for (var i = 0; i < 4; i++)
        {
            var top = Form201SF.Spot(rows[i]).Y;
            var bottom = i < 3 ? Form201SF.Spot(rows[i + 1]).Y : 792;
            Assert.All(Form201SF.Blocks[i], n => Assert.InRange(Form201SF.Spot($"Page1[0].CheckBox1[{n}]").Y, top, bottom));
            // ethnicity/gender column on the left, race in the middle
            Assert.All(Form201SF.Blocks[i][..4], n => Assert.True(Form201SF.Spot($"Page1[0].CheckBox1[{n}]").X < 100));
            Assert.All(Form201SF.Blocks[i][4..], n => Assert.InRange(Form201SF.Spot($"Page1[0].CheckBox1[{n}]").X, 240, 310));
        }
    }

    [Fact]
    public void MoreThanFourMembers_GoOnAContinuationList()
    {
        var r = FakeIntake.Family();
        r.Household.Add(new() { FirstName = "Extra", LastName = "Fakeson" });
        r.Household.Add(new() { FirstName = "Another", LastName = "Fakeson" });
        var f = Fill(r);
        Assert.Equal("Extra Fakeson", TextAt(f, "Page1[0].NameRow1_3[0]"));
        Assert.Equal(["Another Fakeson"], f.ExtraMembers.Select(m => m.FullName));
    }

    [Fact]
    public void Page2_HousingFuelAndVendors()
    {
        var r = Full();
        var f = Fill(r);
        Assert.True(Ticked(f, "Page2[0].CheckBox1[3]"));  // rent
        Assert.Equal("900.00", TextAt(f, "Page2[0].I_rent_my_home_or_apartment_and_pay[0]"));
        Assert.True(Ticked(f, "Page2[0].CheckBox1[17]")); // oil
        Assert.True(Ticked(f, "Page2[0].CheckBox1[7]"));  // heat in rent
        Assert.True(Ticked(f, "Page2[0].RadioButtonList[3]#yes"));
        Assert.True(Ticked(f, "Page2[0].CheckBox1[22]")); // section 8
        Assert.Equal("Example Oil", TextAt(f, "Page2[0].FuelSupplier[0]"));
        Assert.Equal("(802) 555-0101", TextAt(f, "Page2[0].Phone_number[0]"));
        Assert.Equal("FAKE-123", TextAt(f, "Page2[0].Account_number[0]"));
        Assert.Equal("Sample Power", TextAt(f, "Page2[0].ElectricProvider[0]"));
        Assert.Equal("FAKE-456", TextAt(f, "Page2[0].Account_number_2[0]"));
    }

    [Fact]
    public void Page2_UnknownAnswers_StayBlank()
    {
        var r = FakeIntake.Family(); // mobile home, no fuel type, no housing benefits
        var f = Fill(r);
        Assert.True(Ticked(f, "Page2[0].CheckBox1[11]")); // mobile home
        Assert.False(Ticked(f, "Page2[0].CheckBox1[2]") || Ticked(f, "Page2[0].CheckBox1[3]")); // neither own nor rent
        Assert.False(Ticked(f, "Page2[0].RadioButtonList[3]#yes") || Ticked(f, "Page2[0].RadioButtonList[3]#no"));
        Assert.Null(TextAt(f, "Page2[0].FuelSupplier[0]"));
    }

    [Fact]
    public void Page3_UnearnedIncome_WithNamesAndAmounts()
    {
        var f = Fill(Full());
        Assert.True(Ticked(f, "Page3[0].RadioButtonList[1]#yes"));
        Assert.Equal("Testy Sampleton", TextAt(f, "Page3[0].Name_sSocial_Security[0]"));
        Assert.Equal("1,400.00", TextAt(f, "Page3[0].fill_80[0]")); // SSA + SSDI
        Assert.Equal("250.00", TextAt(f, "Page3[0].fill_85[0]"));  // child support
        Assert.Equal("help from a fake relative", TextAt(f, "Page3[0].Name_sOther[0]"));
        Assert.Equal("40.00", TextAt(f, "Page3[0].fill_90[0]"));
        Assert.Null(TextAt(f, "Page3[0].fill_81[0]"));             // no SSI
        Assert.True(Ticked(f, "Page3[0].RadioButtonList[0]#yes")); // self-employment
        Assert.Equal("Pat Example", TextAt(f, Form201SF.HelperName));
        Assert.Equal("(802) 555-0100", TextAt(f, Form201SF.HelperPhone));
        Assert.Equal(Form201SF.AgencyName, TextAt(f, Form201SF.HelperAgency));
    }

    [Fact]
    public void Page3_NoUnearnedIncome_LeavesQuestion15Blank()
    {
        var f = Fill(FakeIntake.Family());
        Assert.False(Ticked(f, "Page3[0].RadioButtonList[1]#yes"));
        Assert.False(Ticked(f, "Page3[0].RadioButtonList[0]#yes"));
    }

    [Fact]
    public void Radios_YesIsTheLeftBox()
    {
        Assert.True(Form201SF.Spot("Page3[0].RadioButtonList[1]#yes").X < Form201SF.Spot("Page3[0].RadioButtonList[1]#no").X);
        Assert.True(Form201SF.Spot("Page2[0].RadioButtonList[3]#yes").X < Form201SF.Spot("Page2[0].RadioButtonList[3]#no").X);
    }

    [Fact]
    public void LongText_ShrinksToFit()
    {
        var spot = Form201SF.Spot("Page2[0].Account_number[0]");
        Assert.Equal(9, new FormMark(spot, "12345").FontSize);
        Assert.True(new FormMark(spot, new string('9', 60)).FontSize < 9);
    }

    private static IntakeRecord Full()
    {
        var r = FakeIntake.Family();
        r.Client.HousingType = "Rent";
        r.Application.HeatInRent = true;
        r.Client.Benefits.HousingChoiceVoucher = true;
        r.FuelElectric.FuelType = "Oil";
        r.FuelElectric.Vendor = "Example Oil - St. Johnsbury";
        r.FuelElectric.VendorAccount = "FAKE-123";
        r.FuelElectric.ElectricProvider = "Sample Power";
        r.FuelElectric.ElectricAccount = "FAKE-456";
        r.Household[0].IncomeSource = "Social Security Retirement (SSA)";
        r.Household[1].IncomeSource = "Self-employment";
        r.IncomeExpenses.Income["SSA"] = 1000m;
        r.IncomeExpenses.Income["SSDI"] = 400m;
        r.IncomeExpenses.Income["ChildSupport"] = 250m;
        r.IncomeExpenses.Income["Other"] = 40m;
        r.IncomeExpenses.IncomeOtherDescription = "help from a fake relative";
        return r;
    }
}

public class CitizenshipTests
{
    [Theory]
    [InlineData(0, "U.S. citizen", 10)]
    [InlineData(0, "Asylee", 11)]
    [InlineData(0, "Legal immigrant", 9)]
    [InlineData(0, "Refugee", 12)]
    [InlineData(0, "Other", 13)]
    [InlineData(1, "U.S. citizen", 24)]
    [InlineData(1, "Legal immigrant", 23)]
    [InlineData(1, "Other", 27)]
    public void Boxes_MatchTheirPlaceOnThePage(int row, string value, int box) =>
        Assert.Equal(box, FuelIntake.Core.Forms.Form201SF.CitizenshipBox(row, value));

    [Fact]
    public void EveryRow_HasFiveDistinctBoxes_InItsOwnBlock()
    {
        for (var row = 0; row < FuelIntake.Core.Forms.Form201SF.MemberRows; row++)
        {
            var boxes = FuelIntake.Core.Lists.ChoiceLists.Citizenship.Select(v => FuelIntake.Core.Forms.Form201SF.CitizenshipBox(row, v)!.Value).ToList();
            Assert.Equal(5, boxes.Distinct().Count());
            Assert.All(boxes, b => Assert.InRange(b, row * 14 + 9, row * 14 + 13));
        }
        Assert.Null(FuelIntake.Core.Forms.Form201SF.CitizenshipBox(0, null));
        Assert.Null(FuelIntake.Core.Forms.Form201SF.CitizenshipBox(0, "Martian"));
    }

    [Fact]
    public void Fill_TicksEachMembersStatus()
    {
        var r = FakeIntake.Family();
        r.Household[0].Citizenship = "U.S. citizen";
        r.Household[1].Citizenship = "Refugee";
        var f = FuelIntake.Core.Forms.Form201SF.Fill(r, new FuelIntake.Core.Model.IntakeCalculations(r, FakeIntake.Context()));
        bool Ticked(int n) => f.Marks.Any(m => m.IsCheck && m.Spot == FuelIntake.Core.Forms.Form201SF.Spot($"Page1[0].CheckBox1[{n}]"));
        Assert.True(Ticked(10));
        Assert.True(Ticked(26)); // row 2, Refugee
        Assert.False(Ticked(9) || Ticked(11) || Ticked(12) || Ticked(13));
    }

    [Fact]
    public void Standards_201SFRequiresIt_CrisisFuelChecksIt()
    {
        var c = FuelIntake.Core.Compliance.ComplianceSettings.Defaults();
        Assert.Contains("household[*].citizenship", c.Standards.Single(s => s.Name == FuelIntake.Core.Compliance.ComplianceSettings.Vermont201SFName).Required);
        Assert.NotNull(FuelIntake.Core.Compliance.FieldCatalog.Find("household[*].citizenship"));

        var r = FakeIntake.Family();
        r.Application.ComplianceStandard = "Crisis Fuel";
        foreach (var m in r.Household) m.Citizenship = "Other";
        var issues = FuelIntake.Core.Validation.IntakeValidator.Validate(r, new FuelIntake.Core.Model.IntakeCalculations(r, FakeIntake.Context() with { Compliance = c }), FakeIntake.Today);
        Assert.Contains(issues, i => i.Rule == "3102-citizen");
        r.Household[0].Citizenship = "Legal immigrant";
        issues = FuelIntake.Core.Validation.IntakeValidator.Validate(r, new FuelIntake.Core.Model.IntakeCalculations(r, FakeIntake.Context() with { Compliance = c }), FakeIntake.Today);
        Assert.DoesNotContain(issues, i => i.Rule == "3102-citizen");
    }
}
