using FuelIntake.Core.Contacts;

namespace FuelIntake.Core.Tests;

public class ContactsTests
{
    [Theory]
    [InlineData("Jane Sample", "JS")]
    [InlineData("Sample, Jane", "JS")]
    [InlineData("jane q sample", "JS")]
    [InlineData("Jane", "J")]
    [InlineData("", "")]
    public void AutoInitials(string name, string expected) => Assert.Equal(expected, StaffDirectory.AutoInitials(name));

    [Fact]
    public void Staff_FoundByNameOrEmail_IgnoringCase()
    {
        var dir = new StaffDirectory([new("Pat Example", "PE", "8025550100", "pexample@example.org")]);
        Assert.NotNull(dir.FindByName(" pat example "));
        Assert.NotNull(dir.FindByEmail("PEXAMPLE@example.org"));
        Assert.Null(dir.FindByName("Someone Else"));
    }

    private static readonly VendorDirectory Vendors = new([
        new Vendor("Example Oil", "St. Johnsbury", "orders@exampleoil.test", "802-555-0101", "Oil, Kerosene"),
        new Vendor("Example Oil", "Lancaster NH", "nh@exampleoil.test", "603-555-0102", "Oil"),
        new Vendor("Sample Power", null, "billing@samplepower.test\nhelp@samplepower.test", "800-555-0103\n802-555-0104", "Electric"),
        new Vendor("Mixed Co", "Derby", "m@mixed.test", "802-555-0105", "Electric, Wood"),
        new Vendor("Blank Offer Co", "Barton"),
    ]);

    [Fact]
    public void SameNameDifferentLocation_StayDistinct()
    {
        Assert.Equal("Example Oil - St. Johnsbury", Vendors.Vendors[0].DisplayName);
        Assert.Equal("nh@exampleoil.test", Vendors.Find("Example Oil - Lancaster NH")!.ContactEmails);
    }

    [Fact]
    public void Dropdowns_SplitFuelAndElectric()
    {
        Assert.Equal(["Example Oil - St. Johnsbury", "Example Oil - Lancaster NH", "Mixed Co - Derby", "Blank Offer Co - Barton"],
            Vendors.FuelVendors.Select(v => v.DisplayName));
        Assert.Equal(["Sample Power", "Mixed Co - Derby"], Vendors.ElectricProviders.Select(v => v.DisplayName));
    }

    [Fact]
    public void MultiLineValues_JoinWithCommas() =>
        Assert.Equal("billing@samplepower.test, help@samplepower.test", Vendor.JoinLines(Vendors.Find("Sample Power")!.ContactEmails));
}
