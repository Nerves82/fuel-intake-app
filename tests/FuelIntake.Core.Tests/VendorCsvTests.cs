using FuelIntake.Core.Contacts;

namespace FuelIntake.Core.Tests;

public class VendorCsvTests
{
    private const string Export =
        "﻿Vender Name,Location,Minimum Delivery,Emergency Trip Fee,Preassure Test/Leak Check Fee,Contact Emails,Contact Phone Numbers,Fuel Offered,Notes\r\n" +
        "Example Oil,St. Johnsbury,100 gal,$150,$85,\"orders@exampleoil.test\nbilling@exampleoil.test\",802-555-0111,\"[\"\"Oil\"\",\"\"Kerosene\"\"]\",\"Call before noon, weekdays\"\r\n" +
        "Pretend Power,,,,,billing@pretendpower.test,800-555-0114,Electric,\r\n" +
        ",,,,,,,,\r\n";

    [Fact]
    public void ReadsColumnsByName_WithQuotesAndLineBreaks()
    {
        var r = VendorCsv.Parse(Export);
        Assert.Empty(r.Warnings);
        Assert.Equal(2, r.Vendors.Count);
        var oil = r.Vendors[0];
        Assert.Equal("Example Oil - St. Johnsbury", oil.DisplayName);
        Assert.Equal("orders@exampleoil.test, billing@exampleoil.test", Vendor.JoinLines(oil.ContactEmails));
        Assert.Equal("Oil, Kerosene", oil.FuelOffered);
        Assert.Equal("Call before noon, weekdays", oil.Notes);
        Assert.True(r.Vendors[1].OffersElectric);
    }

    [Fact]
    public void TitleHeader_IsAccepted() =>
        Assert.Single(VendorCsv.Parse("Title,Fuel Offered\nSample Propane,Propane\n").Vendors);

    [Fact]
    public void MissingNameColumn_Warns()
    {
        var r = VendorCsv.Parse("Company,Phone\nX,1\n");
        Assert.Empty(r.Vendors);
        Assert.NotEmpty(r.Warnings);
    }

    [Theory]
    [InlineData("Oil;#Propane", "Oil, Propane")]
    [InlineData("[\"Electric\"]", "Electric")]
    [InlineData("Oil, Wood", "Oil, Wood")]
    public void Choices_AreCleaned(string raw, string expected) => Assert.Equal(expected, VendorCsv.CleanChoices(raw));
}

/// <summary>The real Fuel Team Vendor List export bundled with the app.</summary>
public class BuiltInVendorListTests
{
    private static readonly VendorCsv.Result List = VendorCsv.BuiltIn();
    private static readonly VendorDirectory Dir = new(List.Vendors);

    [Fact]
    public void Reads_AllNamedVendors_SkippingTheSchemaLine()
    {
        Assert.Equal(38, List.Vendors.Count);
        Assert.DoesNotContain(List.Vendors, v => v.Name.StartsWith("ListSchema"));
        Assert.Contains(List.Warnings, w => w.Contains("no Vender Name") && w.Contains("Lyndon"));
    }

    [Fact]
    public void ElectricProviders_AreTheFourUtilities() =>
        Assert.Equal(["Green Mountain Power", "Lyndonville Electric - Lyndonville", "VT Electric Coop", "Washington Electric"],
            Dir.ElectricProviders.Select(v => v.DisplayName));

    [Fact]
    public void DuplicateNames_AreKeptApartByLocation()
    {
        Assert.NotNull(Dir.Find("CN Brown - St. Johnsbury"));
        Assert.NotNull(Dir.Find("CN Brown - Lancaster NH"));
        Assert.NotNull(Dir.Find("CN Brown - Colebrook NH"));
        Assert.Equal(3, Dir.Vendors.Count(v => v.Name == "Fred's Propane"));
        Assert.Equal(Dir.Vendors.Count, Dir.Vendors.Select(v => v.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void MultiLineContacts_AndFees_ComeThrough()
    {
        var dr = Dir.Find("Dead River - St. Johnsbury")!;
        Assert.Equal("Karie.finn@deadriver.com, Kelly.winget@deadriver.com", Vendor.JoinLines(dr.ContactEmails));
        Assert.Equal("(802) 748-8934, (802) 748-1393", Vendor.FormatPhones(dr.ContactPhones));
        Assert.Equal("$100", dr.PressureTestFee);
        Assert.Equal("Oil, Propane", dr.FuelOffered);
        Assert.Contains("OUT OF ROUTE FEE", dr.Notes);
        Assert.Null(Dir.Find("Bournes - Lyndonville")!.PressureTestFee); // 0 = not set
        Assert.Equal("(802) 626-9859", Vendor.FormatPhones(Dir.Find("Bournes - Lyndonville")!.ContactPhones));
    }
}
