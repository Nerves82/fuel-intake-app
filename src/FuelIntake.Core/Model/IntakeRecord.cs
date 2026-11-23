namespace FuelIntake.Core.Model;

/// <summary>
/// Everything entered for one client. Saved as intake.json in the client's folder.
/// Property names follow the workbook's field names where practical, to keep porting traceable.
/// Calculated values (totals, FPL, initials…) are NOT stored; see <see cref="IntakeCalculations"/>.
/// </summary>
public sealed class IntakeRecord
{
    /// <summary>File format version. Bump when the shape changes, and teach the loader to upgrade older files.</summary>
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>True for test data; the app refuses to save test data over a real client and vice versa.</summary>
    public bool IsTestData { get; set; }

    /// <summary>Who created this intake, and when.</summary>
    public StaffStamp? CreatedBy { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Change history: who changed which fields, newest last. See <see cref="Activity.ActivityLog"/>.</summary>
    public List<Activity.ActivityEntry> Activity { get; set; } = [];

    /// <summary>Staff notes on this client, oldest first.</summary>
    public List<StaffNote> Notes { get; set; } = [];

    public StaffSection Staff { get; set; } = new();
    public ClientSection Client { get; set; } = new();
    public List<HouseholdMember> Household { get; set; } = [];
    public ApplicationSection Application { get; set; } = new();
    public IncomeExpenseSection IncomeExpenses { get; set; } = new();
    public FuelElectricSection FuelElectric { get; set; } = new();
}

/// <summary>Tab 1 "Application &amp; staff".</summary>
public sealed class StaffSection
{
    public DateOnly? AppDate { get; set; }
    public string? CaseNumber { get; set; }
    /// <summary>Picked from the Staff list (or typed).</summary>
    public string? StaffName { get; set; }
    public string? InitialsOverride { get; set; }
    public string? PhoneOverride { get; set; }
    public string? EmailOverride { get; set; }

    /// <summary>
    /// Copy of the assigned staff member's Staff-list entry, saved with the client so their details print
    /// correctly on any computer, and so they can be added to another staff member's Staff list.
    /// </summary>
    public StaffContact? StaffSnapshot { get; set; }
}

/// <summary>A timestamped, authored note. Deleting removes it; the activity log keeps a record.</summary>
public sealed class StaffNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset At { get; set; }
    public StaffStamp By { get; set; } = new("", null);
    public string Text { get; set; } = "";
    /// <summary>The field this note is about (right-click → Add note), or null for a general note.</summary>
    public NoteFieldRef? About { get; set; }
}

/// <summary>
/// Points a note at a field. <see cref="Screen"/> is the tab name, <see cref="Key"/> identifies the field
/// on that tab, <see cref="Label"/> is what staff see ("Household › Member 2 › SSN").
/// </summary>
public sealed record NoteFieldRef(string Screen, string Key, string Label);

/// <summary>A person who worked on the intake.</summary>
public sealed record StaffStamp(string Name, string? Email);

/// <summary>A Staff-list entry saved with the client file.</summary>
public sealed record StaffContact(string Name, string? Initials, string? Phone, string? Email);

/// <summary>Tab 1 client, contact, service address, housing, benefits and interests.</summary>
public sealed class ClientSection
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? LegalRepName { get; set; }

    public string? AddressPhysical { get; set; }
    public string? City { get; set; }
    public string? State { get; set; } = "VT";
    public string? Zip { get; set; }
    public string? County { get; set; }
    public string? SchoolDistrict { get; set; }
    public string? AddressMailing { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PrimaryLanguage { get; set; }
    public string? LanguageOther { get; set; }
    public string? EnglishFluency { get; set; }
    /// <summary>"Y" or "N".</summary>
    public string? TranslatorNeeded { get; set; }

    /// <summary>Blank = use the physical address.</summary>
    public string? ServiceAddressLine1 { get; set; }
    /// <summary>Blank = use City/Town, State, Zip.</summary>
    public string? ServiceAddressLine2 { get; set; }

    /// <summary>GoEngage housing value.</summary>
    public string? HousingType { get; set; }
    /// <summary>GoEngage household type value.</summary>
    public string? HouseholdType { get; set; }

    public BenefitsReceived Benefits { get; set; } = new();
    public ServicesOfInterest Interests { get; set; } = new();
}

public sealed class BenefitsReceived
{
    public bool Snap { get; set; }
    public decimal? SnapAmount { get; set; }
    /// <summary>"month", "week" or "year".</summary>
    public string? SnapPeriod { get; set; }
    /// <summary>Date SNAP was looked up in ACCESS; fills the ACCESS non-income line.</summary>
    public DateOnly? SnapVerifiedDate { get; set; }
    public bool Wic { get; set; }
    public bool Liheap { get; set; }
    public bool HousingChoiceVoucher { get; set; }
    public bool PublicHousing { get; set; }
    public bool PermanentSupportiveHousing { get; set; }
    public bool HudVash { get; set; }
    public bool ChildcareVoucher { get; set; }
    public bool AcaSubsidy { get; set; }
}

public sealed class ServicesOfInterest
{
    public bool FoodClothing { get; set; }
    public bool HousingSupport { get; set; }
    public bool FuelAssistance { get; set; }
    public bool EconomicPrograms { get; set; }
    public bool YouthPrograms { get; set; }
    public bool FamilySupports { get; set; }
    public bool Tefap { get; set; }
    public bool CommunityJustice { get; set; }
    public bool Other { get; set; }
    public string? OtherText { get; set; }
}

/// <summary>
/// Tab 2 row. Member 1 is the client: name and date of birth come from <see cref="ClientSection"/>
/// (see <see cref="IntakeCalculations.Members"/>), so they are not stored on member 1.
/// </summary>
public sealed class HouseholdMember
{
    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    /// <summary>"Y" or "N".</summary>
    public string? Disabled { get; set; }
    public string? Education { get; set; }
    public string? Race { get; set; }
    public string? Hispanic { get; set; }
    public string? HealthInsurance { get; set; }
    public decimal? IncomePerMonth { get; set; }
    public string? IncomeSource { get; set; }
    /// <summary>What the income is, when the income source is "Other".</summary>
    public string? IncomeSourceOther { get; set; }
    public string? MilitaryStatus { get; set; }
    /// <summary>Stored as text so leading zeros survive.</summary>
    public string? Ssn { get; set; }
    /// <summary>GoEngage occupation (members 14+); "N/A" for younger members.</summary>
    public string? Occupation { get; set; }
    public DateOnly? IncomeVerifiedDate { get; set; }
}

/// <summary>Tab 3: NEKCA Fuel Assistance Application.</summary>
public sealed class ApplicationSection
{
    public bool AaaOnBehalf { get; set; }
    public bool AfterHours { get; set; }
    public bool SecondCrisis { get; set; }
    /// <summary><see cref="Lists.CrisisTypes"/> value.</summary>
    public string? CrisisType { get; set; }
    public string? CurrentQuantity { get; set; }
    public bool ElectricForHeat { get; set; }
    public bool ElectricPropane { get; set; }
    public string? ElectricComponent { get; set; }
    /// <summary>"Yes" or "No".</summary>
    public string? LandlordVerification { get; set; }
    public bool HeatInRent { get; set; }
    public bool RuleException { get; set; }
    public bool LifeThreatening { get; set; }
    public string? Circumstance { get; set; }
    /// <summary>"Active", "Pending" or "Closed/Denied".</summary>
    public string? SeasonalFuelStatus { get; set; }
    public bool AppealNotice { get; set; }
    public bool PhoneApplication { get; set; }
    public string? PhoneApplicationReason { get; set; }

    public Referrals Referrals { get; set; } = new();
    public Determination Determination { get; set; } = new();
}

public sealed class Referrals
{
    public bool Neto { get; set; }
    public bool AgencyOnAging { get; set; }
    public bool FinancialCoaching { get; set; }
    public bool CommunityConnections { get; set; }
    public bool WholeFamily { get; set; }
    public bool Vita { get; set; }
}

public sealed class Determination
{
    public bool Denied { get; set; }
    public bool DenyExpeditedSfa { get; set; }
    public bool DenyOver200 { get; set; }
    public bool DenyNoSfb { get; set; }
    public bool DenyNoDocs { get; set; }
    public bool DenyNoCrisis { get; set; }
    public bool DenyResources { get; set; }
    public bool DenyMaxGrants { get; set; }
    public bool DenyNoCircumstance { get; set; }
    public bool DenyFundsUse { get; set; }
    public bool DenyPriorConditions { get; set; }

    public bool WarmthEligible { get; set; }
    public DateOnly? WarmthDate { get; set; }
    public bool VliteEligible { get; set; }
    public DateOnly? VliteDate { get; set; }
    public bool CrisisEligible { get; set; }
    public DateOnly? CrisisEligibleDate { get; set; }
    public TimeOnly? CrisisEligibleTime { get; set; }

    public bool CheckIntake { get; set; }
    public bool CheckAccess { get; set; }
    public bool CheckConsents { get; set; }
    public bool CheckServiceNote { get; set; }
    public bool CheckMissingDocs { get; set; }
}

/// <summary>Tab 4: Income/Expense worksheet. Lines are keyed by <see cref="IncomeExpenseCatalog"/> keys.</summary>
public sealed class IncomeExpenseSection
{
    /// <summary>Gross monthly income by line key (e.g. "SSA").</summary>
    public Dictionary<string, decimal?> Income { get; set; } = [];

    /// <summary>What the "Other" income line is (e.g. "Help from family").</summary>
    public string? IncomeOtherDescription { get; set; }

    /// <summary>Expenses by line key (e.g. "Rent").</summary>
    public Dictionary<string, ExpenseAmounts> Expenses { get; set; } = [];

    /// <summary>"Yes" or "No".</summary>
    public string? IncomeChangeNextMonth { get; set; }
    public string? IncomeChangeExplanation { get; set; }

    public decimal? AssetChecking { get; set; }
    public decimal? AssetSavings { get; set; }
    public decimal? AssetCash { get; set; }
    public decimal? AssetCdsStocksBonds { get; set; }
    public decimal? AssetRetirement { get; set; }
    public string? Notes { get; set; }

    public bool DocumentsAttached30Days { get; set; }
}

public sealed class ExpenseAmounts
{
    public decimal? PaidLast30Days { get; set; }
    public decimal? Bill { get; set; }
}

/// <summary>Tab 5: Seasonal Fuel, Fuel Order, invoice and Electric Pledge.</summary>
public sealed class FuelElectricSection
{
    public decimal? SeasonalFuelAmountGranted { get; set; }
    public DateOnly? SeasonalFuelDateGranted { get; set; }
    public string? FuelType { get; set; }

    /// <summary>Vendor display name as picked ("Name - Location"), or typed.</summary>
    public string? Vendor { get; set; }
    public string? VendorEmailOverride { get; set; }
    public string? VendorPhoneOverride { get; set; }
    public string? VendorAccount { get; set; }

    public string? ElectricProvider { get; set; }
    public string? ElectricEmailOverride { get; set; }
    public string? ElectricPhoneOverride { get; set; }
    public string? ElectricAccount { get; set; }

    public string? AccessNotes { get; set; }

    public FuelOrder FuelOrder { get; set; } = new();
    public FuelInvoice Invoice { get; set; } = new();
    public ElectricPledge ElectricPledge { get; set; } = new();
}

public sealed class FuelOrder
{
    public bool Authorized { get; set; }
    public DateOnly? Date { get; set; }
    public TimeOnly? Time { get; set; }
    public decimal? SfbRemaining { get; set; }
    public string? TankSize { get; set; }
    public string? FuelRemaining { get; set; }
    public string? FuelLasts { get; set; }
    public decimal? Fuel { get; set; }
    public string? Gallons { get; set; }
    public decimal? SpecialTrip { get; set; }
    public decimal? PressureTest { get; set; }
    public decimal? StartUp { get; set; }
    public DateOnly? EstimatedDelivery { get; set; }
    public string? FundingSource { get; set; }
}

/// <summary>Fuel Order "after invoice received". The total is the actual fuel cost entered in GoEngage.</summary>
public sealed class FuelInvoice
{
    public bool Received { get; set; }
    public decimal? Fuel { get; set; }
    public string? Gallons { get; set; }
    public decimal? SpecialTrip { get; set; }
    public decimal? PressureTest { get; set; }
    public decimal? StartUp { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string? FundingSource { get; set; }
}

public sealed class ElectricPledge
{
    public bool Authorized { get; set; }
    public DateOnly? Date { get; set; }
    public TimeOnly? Time { get; set; }
    public bool DisconnectNoticeAttached { get; set; }
    public decimal? AmountRequired { get; set; }
    public decimal? ReconnectionFee { get; set; }
    /// <summary>"Yes" or "No".</summary>
    public string? DiscountProgram { get; set; }
    public DateOnly? DisconnectDate { get; set; }
    public string? FundingSource { get; set; }
    public decimal? RemainingBalance { get; set; }
    public decimal? NextBill { get; set; }
    public DateOnly? NextBillDue { get; set; }
    public string? ClientResponsibilities { get; set; }
}
