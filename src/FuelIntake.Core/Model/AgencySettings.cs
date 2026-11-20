using FuelIntake.Core.Eligibility;

namespace FuelIntake.Core.Model;

/// <summary>Agency-wide settings (the workbook's Settings tab). Edited by staff, not per client.</summary>
public sealed class AgencySettings
{
    public PovertyGuidelines PovertyGuidelines { get; set; } = PovertyGuidelines.Hhs2026;

    /// <summary>Crisis Fuel income limit as a fraction of FPL: 2.0 = under 200%.</summary>
    public decimal IncomeLimit { get; set; } = 2.0m;

    /// <summary>Fuel Order "send invoice to" email when the staff member has none.</summary>
    public string? DefaultInvoiceEmail { get; set; }

    /// <summary>Fuel Order "send invoice to" phone when the staff member has none.</summary>
    public string? DefaultInvoicePhone { get; set; }

    /// <summary>Client folders live under Documents\&lt;this&gt; in phase 1.</summary>
    public string ClientsRootFolderName { get; set; } = "Client Files - Fuel 26-27";

    /// <summary>Between last and first name in client folder names: "Smith, Jane".</summary>
    public string FolderNameSeparator { get; set; } = ", ";

    /// <summary>Date format used in file names, e.g. 11_20_26.</summary>
    public string FileDateFormat { get; set; } = "MM_dd_yy";

    /// <summary>
    /// Template folder, relative to the user's profile folder (phase 1). Forms the app doesn't draw
    /// itself (the consent forms) are copied from here into each new client folder.
    /// </summary>
    public string TemplateFolder { get; set; } =
        @"Northeast Kingdom Community Action, Inc\3025 Crisis Fuel (07 31) - General\Navigator\Masters\Fuel Assistance - Applications and Forms";

    /// <summary>Template files copied into each new client folder (exact names).</summary>
    public List<string> TemplateFiles { get; set; } = ["FA (pg. 2.3-2.6) - Consent Forms.pdf"];
}
