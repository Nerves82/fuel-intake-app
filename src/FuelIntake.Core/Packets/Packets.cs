using System.Globalization;
using FuelIntake.Core.Lists;

namespace FuelIntake.Core.Packets;

/// <summary>The printable forms, in packet (workbook tab) order.</summary>
public enum FormId { CoverSheet, GoEngageData, IntakePage1, IntakePage2, Application, Access, IncomeExpense, FuelOrder, ElectricPledge, EligibilityReview201SF }

/// <summary>A ready-made packet from the Print Packets screen.</summary>
/// <param name="Name">Shown in the picker.</param>
/// <param name="FileNamePattern">Codes: {Initial} {Last} {First} {Date}.</param>
/// <param name="Forms">Forms always included.</param>
/// <param name="AddsFuelOrderOrPledge">Adds the Fuel Order, or the Electric Pledge for a disconnect notice.</param>
/// <param name="StaffOnly">Contains SSNs and income: never upload to GoEngage.</param>
public sealed record PacketDefinition(string Name, string FileNamePattern, IReadOnlyList<FormId> Forms,
    bool AddsFuelOrderOrPledge = false, bool StaffOnly = false);

public static class PacketCatalog
{
    public static readonly IReadOnlyList<FormId> AllForms = Enum.GetValues<FormId>();

    public static readonly PacketDefinition CoverAndGoEngage = new(
        "Cover Sheet + GoEngage Data (staff only - do not upload)", "{Initial}_{Last}_cover_sheet_{Date}",
        [FormId.CoverSheet, FormId.GoEngageData], StaffOnly: true);

    public static readonly PacketDefinition IntakePackage = new(
        "Intake Form Package", "{Initial}_{Last}_signed_intake_{Date}", [FormId.IntakePage1, FormId.IntakePage2]);

    public static readonly PacketDefinition ApplicationPack = new(
        "Application Forms Pack", "{Initial}_{Last}_fuel_assistance_{Date}",
        [FormId.Application, FormId.Access, FormId.IncomeExpense], AddsFuelOrderOrPledge: true);

    /// <summary>The state's DCF form 201SF, filled from the intake (staff and client complete the rest).</summary>
    public static readonly PacketDefinition EligibilityReview = new(
        "Vermont Fuel Assistance Eligibility Review (201SF)", "{Initial}_{Last}_201SF_{Date}", [FormId.EligibilityReview201SF]);

    public static readonly PacketDefinition All = new("All forms", "{Initial}_{Last}_all_forms_{Date}", AllForms, StaffOnly: true);

    public static readonly IReadOnlyList<PacketDefinition> Packs = [CoverAndGoEngage, IntakePackage, ApplicationPack, EligibilityReview, All];

    /// <summary>File name pattern for a custom selection.</summary>
    public const string CustomFileNamePattern = "{Initial}_{Last}_forms_{Date}";

    /// <summary>Name shown in the form picker.</summary>
    public static string Title(FormId f) => f switch
    {
        FormId.CoverSheet => "Cover Sheet",
        FormId.GoEngageData => "GoEngage Data",
        FormId.IntakePage1 => "Intake page 1",
        FormId.IntakePage2 => "Intake page 2",
        FormId.Application => "Application",
        FormId.Access => "ACCESS Verification",
        FormId.IncomeExpense => "Income/Expense Worksheet",
        FormId.FuelOrder => "Fuel Order",
        FormId.ElectricPledge => "Electric Pledge",
        FormId.EligibilityReview201SF => "201SF Eligibility Review (state form)",
        _ => f.ToString(),
    };

    /// <summary>Staff-only forms (SSNs and income).</summary>
    public static bool IsStaffOnly(FormId f) => f is FormId.CoverSheet or FormId.GoEngageData;

    /// <summary>The forms a packet prints for this crisis type, in packet order.</summary>
    public static IReadOnlyList<FormId> FormsFor(PacketDefinition pack, string? crisisType)
    {
        var forms = new List<FormId>(pack.Forms);
        if (pack.AddsFuelOrderOrPledge)
            forms.Add(crisisType?.Trim() == CrisisTypes.DisconnectNotice ? FormId.ElectricPledge : FormId.FuelOrder);
        return forms.Distinct().OrderBy(f => (int)f).ToList();
    }

    /// <summary>Fills {Initial} {Last} {First} {Date} and removes characters Windows doesn't allow in file names.</summary>
    public static string ExpandFileName(string pattern, string? first, string? last, DateOnly date, string dateFormat = "MM_dd_yy")
    {
        first = first?.Trim() ?? ""; last = last?.Trim() ?? "";
        var name = pattern
            .Replace("{Initial}", first.Length > 0 ? first[..1] : "")
            .Replace("{First}", first)
            .Replace("{Last}", last)
            .Replace("{Date}", date.ToString(dateFormat, CultureInfo.InvariantCulture));
        return Storage.ClientFolders.SafeName(name);
    }
}
