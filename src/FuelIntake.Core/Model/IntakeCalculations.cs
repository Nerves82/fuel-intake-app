using FuelIntake.Core.Contacts;
using FuelIntake.Core.Eligibility;
using FuelIntake.Core.Formatting;
using FuelIntake.Core.Mapping;

namespace FuelIntake.Core.Model;

/// <summary>A household member as the forms see it (member 1's name and date of birth come from the client).</summary>
/// <param name="Number">1-based row number.</param>
public sealed record MemberView(int Number, string LastName, string FirstName, DateOnly? DateOfBirth, HouseholdMember Data)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
    public bool IsPresent => FirstName.Length > 0 || LastName.Length > 0;
}

/// <summary>One occupation row on Intake page 2.</summary>
public sealed record OccupationRow(string Name, IReadOnlySet<OccupationBox> Boxes);

/// <summary>
/// Every calculated value the workbook produced, computed from an <see cref="IntakeRecord"/>.
/// Nothing here is stored; recompute whenever the record changes.
/// </summary>
public sealed class IntakeCalculations(IntakeRecord record, CalculationContext context)
{
    private readonly IntakeRecord r = record;
    private readonly CalculationContext ctx = context;

    // ------------------------------------------------------------------ client & address

    public string ClientFullName => $"{T(r.Client.FirstName)} {T(r.Client.LastName)}".Trim();

    /// <summary>"St. Johnsbury, VT 05819".</summary>
    public string CityStateZip
    {
        get
        {
            var city = T(r.Client.City);
            var stateZip = $"{T(r.Client.State)} {T(r.Client.Zip)}".Trim();
            return (city + (city.Length > 0 && stateZip.Length > 0 ? ", " : "") + stateZip).Trim();
        }
    }

    public string ServiceAddressLine1 => Blank(r.Client.ServiceAddressLine1) ? T(r.Client.AddressPhysical) : T(r.Client.ServiceAddressLine1);

    public string ServiceAddressLine2 => Blank(r.Client.ServiceAddressLine2) ? CityStateZip : T(r.Client.ServiceAddressLine2);

    public HousingBox? HousingBox => PaperMappings.HousingBoxFor(r.Client.HousingType);

    public HouseholdTypeBox? HouseholdTypeBox => PaperMappings.HouseholdTypeBoxFor(r.Client.HouseholdType);

    /// <summary>ACCESS non-income line from SNAP: "SNAP $291.00/month".</summary>
    public string AccessNonIncomeLine
    {
        get
        {
            var b = r.Client.Benefits;
            if (!b.Snap || b.SnapAmount is null) return "";
            return "SNAP " + Money.Format(b.SnapAmount) + (Blank(b.SnapPeriod) ? "" : "/" + T(b.SnapPeriod));
        }
    }

    // ------------------------------------------------------------------ household

    /// <summary>Household rows, member 1 = the client. Always at least one row.</summary>
    public IReadOnlyList<MemberView> Members
    {
        get
        {
            var list = new List<MemberView>();
            var first = r.Household.Count > 0 ? r.Household[0] : new HouseholdMember();
            list.Add(new MemberView(1, T(r.Client.LastName), T(r.Client.FirstName), r.Client.DateOfBirth, first));
            for (var i = 1; i < r.Household.Count; i++)
            {
                var m = r.Household[i];
                list.Add(new MemberView(i + 1, T(m.LastName), T(m.FirstName), m.DateOfBirth, m));
            }
            return list;
        }
    }

    /// <summary>Number of rows with a first or last name.</summary>
    public int HouseholdSize => Members.Count(m => m.IsPresent);

    /// <summary>Whole years on the application date (or today if no application date).</summary>
    public int? AgeOf(MemberView m)
    {
        if (m.DateOfBirth is not { } dob) return null;
        var asOf = r.Staff.AppDate ?? ctx.Today;
        var age = asOf.Year - dob.Year;
        if (asOf < dob.AddYears(age)) age--;
        return age < 0 ? null : age;
    }

    /// <summary>ACCESS "Income (quantity and source)": "$1,250.00/mo - Social Security Retirement (SSA)".</summary>
    public static string IncomeText(MemberView m)
    {
        var parts = new List<string>();
        if (m.Data.IncomePerMonth is { } inc) parts.Add(Money.Format(inc) + "/mo");
        if (!Blank(m.Data.IncomeSource)) parts.Add(T(m.Data.IncomeSource));
        return string.Join(" - ", parts);
    }

    /// <summary>Sum of the household "Income per month" column.</summary>
    public decimal HouseholdIncomeColumnTotal => Members.Sum(m => m.Data.IncomePerMonth ?? 0m);

    /// <summary>Intake page 2 occupation rows: the first four members whose occupation isn't blank or N/A.</summary>
    public IReadOnlyList<OccupationRow> OccupationRows =>
        Members.Where(m => !Blank(m.Data.Occupation) && T(m.Data.Occupation) != PaperMappings.OccupationNotApplicable)
            .Take(4)
            .Select(m => new OccupationRow(m.FullName, PaperMappings.OccupationBoxesFor(m.Data.Occupation)))
            .ToList();

    // ------------------------------------------------------------------ income & eligibility

    public decimal IncomeTotal => r.IncomeExpenses.Income.Values.Sum(v => v ?? 0m);

    public decimal ExpensesPaidTotal => r.IncomeExpenses.Expenses.Values.Sum(e => e.PaidLast30Days ?? 0m);

    public decimal ExpensesBillTotal => r.IncomeExpenses.Expenses.Values.Sum(e => e.Bill ?? 0m);

    /// <summary>Income − expenses paid in the last 30 days.</summary>
    public decimal IncomeMinusExpenses => IncomeTotal - ExpensesPaidTotal;

    /// <summary>The household income column (tab 2) should equal the worksheet total (tab 4).</summary>
    public bool HouseholdIncomeMatchesWorksheet => Math.Round(HouseholdIncomeColumnTotal - IncomeTotal, 2) == 0m;

    public decimal? AnnualGuideline => ctx.Settings.PovertyGuidelines.AnnualGuideline(HouseholdSize);

    /// <summary>FPL as a fraction (1.5 = 150%), or null when household size is 0.</summary>
    public decimal? FplPercent => FplCalculator.Percent(IncomeTotal, HouseholdSize, ctx.Settings.PovertyGuidelines);

    /// <summary>"FPL less than 200%" checkbox.</summary>
    public bool? UnderIncomeLimit => FplCalculator.IsUnderLimit(FplPercent, ctx.Settings.IncomeLimit);

    // ------------------------------------------------------------------ staff

    /// <summary>The Staff-list entry for the assigned staff, else the copy saved with the client.</summary>
    private StaffMember? StaffRow => ctx.Staff.FindByName(r.Staff.StaffName)
        ?? (r.Staff.StaffSnapshot is { } s && string.Equals(s.Name.Trim(), r.Staff.StaffName?.Trim(), StringComparison.OrdinalIgnoreCase)
            ? new StaffMember(s.Name, s.Initials, s.Phone, s.Email) : null);

    /// <summary>Override, else Staff list, else automatic from the name.</summary>
    public string StaffInitials =>
        !Blank(r.Staff.InitialsOverride) ? T(r.Staff.InitialsOverride).ToUpperInvariant()
        : !Blank(StaffRow?.Initials) ? T(StaffRow!.Initials).ToUpperInvariant()
        : StaffDirectory.AutoInitials(r.Staff.StaffName);

    /// <summary>Raw staff phone: override, else Staff list.</summary>
    public string StaffPhone => !Blank(r.Staff.PhoneOverride) ? T(r.Staff.PhoneOverride) : T(StaffRow?.Phone);

    public string StaffEmail => !Blank(r.Staff.EmailOverride) ? T(r.Staff.EmailOverride) : T(StaffRow?.Email);

    /// <summary>Fuel Order "send invoice to" email: staff email, else agency default.</summary>
    public string InvoiceEmail => StaffEmail.Length > 0 ? StaffEmail : T(ctx.Settings.DefaultInvoiceEmail);

    /// <summary>Fuel Order "send invoice to" phone, formatted: staff phone, else agency default.</summary>
    public string InvoicePhone => Phone.Format(StaffPhone.Length > 0 ? StaffPhone : ctx.Settings.DefaultInvoicePhone);

    // ------------------------------------------------------------------ vendors

    public Vendor? FuelVendor => ctx.Vendors.Find(r.FuelElectric.Vendor);

    public Vendor? ElectricVendor => ctx.Vendors.Find(r.FuelElectric.ElectricProvider);

    /// <summary>Fuel Order contact line 1: override, else vendor list emails.</summary>
    public string VendorEmails => Override(r.FuelElectric.VendorEmailOverride, FuelVendor?.ContactEmails);

    /// <summary>Fuel Order contact line 2: override, else vendor list phones.</summary>
    public string VendorPhones => Override(r.FuelElectric.VendorPhoneOverride, FuelVendor?.ContactPhones);

    public string ElectricEmails => Override(r.FuelElectric.ElectricEmailOverride, ElectricVendor?.ContactEmails);

    public string ElectricPhones => Override(r.FuelElectric.ElectricPhoneOverride, ElectricVendor?.ContactPhones);

    // ------------------------------------------------------------------ order totals (blank when nothing entered)

    public decimal? FuelOrderTotal
    {
        get { var o = r.FuelElectric.FuelOrder; return Money.SumOrNull(o.Fuel, o.SpecialTrip, o.PressureTest, o.StartUp); }
    }

    /// <summary>Actual fuel cost to enter in GoEngage.</summary>
    public decimal? InvoiceTotal
    {
        get { var i = r.FuelElectric.Invoice; return Money.SumOrNull(i.Fuel, i.SpecialTrip, i.PressureTest, i.StartUp); }
    }

    public decimal? PledgeTotal
    {
        get { var p = r.FuelElectric.ElectricPledge; return Money.SumOrNull(p.AmountRequired, p.ReconnectionFee); }
    }

    // ------------------------------------------------------------------ helpers

    private static string T(string? s) => s?.Trim() ?? "";
    private static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
    private static string Override(string? over, string? fromList) => Blank(over) ? Vendor.JoinLines(fromList) : T(over);
}
