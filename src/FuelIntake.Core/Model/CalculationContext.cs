using FuelIntake.Core.Contacts;

namespace FuelIntake.Core.Model;

/// <summary>Everything outside the client record that calculations need.</summary>
public sealed record CalculationContext(
    AgencySettings Settings,
    StaffDirectory Staff,
    VendorDirectory Vendors,
    DateOnly Today)
{
    public static CalculationContext Default(DateOnly today) =>
        new(new AgencySettings(), StaffDirectory.Empty, VendorDirectory.Empty, today);
}
