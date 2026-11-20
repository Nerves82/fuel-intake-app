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
