using FuelIntake.Core.Contacts;
using FuelIntake.Core.Email;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Tests;

public class FuelOrderEmailTests
{
    private static (IntakeRecord R, IntakeCalculations C) Order(AgencySettings? settings = null)
    {
        var r = FuelIntake.Core.TestData.SampleIntakes.SingleSenior("Testy", "Sampleton", FakeIntake.Today);
        r.FuelElectric.Vendor = "Example Oil - St. Johnsbury";
        r.FuelElectric.VendorAccount = "TEST-1";
        r.FuelElectric.FuelOrder.Fuel = 400m;
        r.FuelElectric.FuelOrder.SpecialTrip = 75m;
        r.FuelElectric.FuelOrder.Gallons = null;
        var ctx = FakeIntake.Context() with
        {
            Vendors = new VendorDirectory([new Vendor("Example Oil", "St. Johnsbury", "Orders <orders@example.com>; billing@example.com")]),
            Settings = settings ?? new AgencySettings(),
        };
        return (r, new IntakeCalculations(r, ctx));
    }

    [Fact]
    public void Build_FillsToSubjectAndMessage_FromTheDefaults()
    {
        var (r, c) = Order();
        var mail = FuelOrderEmail.Build(r, c, new AgencySettings());
        Assert.Equal(["orders@example.com", "billing@example.com"], mail.To);
        Assert.Equal("Fuel Order - Testy Sampleton - 12 Pretend Pond Rd", mail.Subject);
        Assert.StartsWith("Hello Example Oil,", mail.Body);
        Assert.Contains("Amount authorized: $475.00", mail.Body);
        Assert.Contains("Account #: TEST-1", mail.Body);
        Assert.DoesNotContain("Gallons", mail.Body); // blank detail: its line is left out
        Assert.DoesNotContain("{", mail.Body);
    }

    [Fact]
    public void Build_UsesTheAgencyWording_AndKeepsUnknownPlaceholders()
    {
        var s = new AgencySettings { FuelOrderEmailSubject = "Order for {Last}", FuelOrderEmailBody = "Hi {Vendor}, order for {First}. {Typo}" };
        var (r, c) = Order(s);
        var mail = FuelOrderEmail.Build(r, c, s);
        Assert.Equal("Order for Sampleton", mail.Subject);
        Assert.Equal("Hi Example Oil, order for Testy. {Typo}", mail.Body);
    }

    [Fact]
    public void Problems_NeedVendorEmailAndAmount()
    {
        var (r, c) = Order();
        Assert.Empty(FuelOrderEmail.Problems(r, c));
        r.FuelElectric.FuelOrder.Fuel = null;
        r.FuelElectric.Vendor = "Somebody Not On The List";
        Assert.Equal(2, FuelOrderEmail.Problems(r, new IntakeCalculations(r, FakeIntake.Context())).Count);
    }

    [Fact]
    public void FindSavedPdf_PicksTheNewestFuelOrder()
    {
        var (r, _) = Order();
        string[] files = ["T_Sampleton_fuel_order_12_30_25.pdf", "T_Sampleton_fuel_order_01_05_26.pdf", "T_Sampleton_all_forms_02_01_26.pdf", "intake.json"];
        Assert.Equal("T_Sampleton_fuel_order_01_05_26.pdf", FuelOrderEmail.FindSavedPdf(files, r));
        Assert.Null(FuelOrderEmail.FindSavedPdf(["T_Sampleton_all_forms_02_01_26.pdf"], r));
        Assert.Equal("T_Sampleton_fuel_order_11_20_26.pdf", FuelOrderEmail.PdfFileName(r, new DateOnly(2026, 11, 20)));
    }
}
