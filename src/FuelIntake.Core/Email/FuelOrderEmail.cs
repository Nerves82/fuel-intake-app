using System.Globalization;
using System.Text.RegularExpressions;
using FuelIntake.Core.Formatting;
using FuelIntake.Core.Model;
using FuelIntake.Core.Packets;

namespace FuelIntake.Core.Email;

/// <summary>An email ready to open in the mail app (staff check it and press Send themselves).</summary>
public sealed record EmailDraft(IReadOnlyList<string> To, string Subject, string Body);

/// <summary>
/// The email that sends a Fuel Order to the vendor: To from the vendor list (or the override on tab 5),
/// subject and message from Agency settings (with defaults), and the Fuel Order PDF attached.
/// </summary>
public static partial class FuelOrderEmail
{
    public const string DefaultSubject = "Fuel Order - {ClientName} - {ServiceAddress}";

    public const string DefaultBody =
        """
        Hello {Vendor},

        Attached is a Fuel Order from NEKCA Crisis Fuel for {ClientName} at {ServiceAddress}, {ServiceTown}.

        Fuel type: {FuelType}
        Amount authorized: {OrderTotal}
        Gallons: {Gallons}
        Account #: {Account}

        Please send the invoice to {InvoiceEmail} after delivery.

        Thank you,
        {StaffName}
        {StaffPhone}
        """;

    /// <summary>The words staff can use in the subject and message, with what each becomes.</summary>
    public static readonly IReadOnlyList<(string Token, string Meaning)> Placeholders =
    [
        ("{ClientName}", "client's first and last name"),
        ("{First}", "client's first name"),
        ("{Last}", "client's last name"),
        ("{ServiceAddress}", "delivery address (street)"),
        ("{ServiceTown}", "delivery town, state and zip"),
        ("{ClientPhone}", "client's phone"),
        ("{Vendor}", "fuel vendor"),
        ("{Account}", "vendor account #"),
        ("{FuelType}", "fuel type"),
        ("{FuelAmount}", "fuel $ on the order"),
        ("{OrderTotal}", "order total incl. fees"),
        ("{Gallons}", "gallons"),
        ("{DeliveryDate}", "estimated delivery date"),
        ("{FundingSource}", "grant/funding source"),
        ("{OrderDate}", "date the order was authorized"),
        ("{StaffName}", "staff name"),
        ("{StaffPhone}", "staff phone"),
        ("{StaffEmail}", "staff email"),
        ("{InvoiceEmail}", "where invoices go"),
    ];

    /// <summary>File name pattern for the Fuel Order PDF the email attaches.</summary>
    public const string PdfFileNamePattern = "{Initial}_{Last}_fuel_order_{Date}";

    public static EmailDraft Build(IntakeRecord r, IntakeCalculations calc, AgencySettings settings)
    {
        var values = Values(r, calc);
        var subject = Fill(string.IsNullOrWhiteSpace(settings.FuelOrderEmailSubject) ? DefaultSubject : settings.FuelOrderEmailSubject, values);
        var body = FillBody(string.IsNullOrWhiteSpace(settings.FuelOrderEmailBody) ? DefaultBody : settings.FuelOrderEmailBody, values);
        return new EmailDraft(Addresses(calc.VendorEmails), OneLine(subject), body);
    }

    /// <summary>Reasons the email can't be made yet (shown on the Review tab).</summary>
    public static IReadOnlyList<string> Problems(IntakeRecord r, IntakeCalculations calc)
    {
        var p = new List<string>();
        if (string.IsNullOrWhiteSpace(r.FuelElectric.Vendor)) p.Add("Pick the fuel vendor on tab 5.");
        else if (Addresses(calc.VendorEmails).Count == 0)
            p.Add("The vendor has no email on the vendor list. Add one there, or type it in the vendor email box on tab 5.");
        if (r.FuelElectric.FuelOrder.Fuel is null) p.Add("Enter the fuel amount on the Fuel Order (tab 5).");
        return p;
    }

    /// <summary>Email addresses found in a vendor's contact emails (any separators, "Name &lt;a@b&gt;" allowed).</summary>
    public static IReadOnlyList<string> Addresses(string? text) =>
        EmailPattern().Matches(text ?? "").Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>The Fuel Order PDF's name for today.</summary>
    public static string PdfFileName(IntakeRecord r, DateOnly today, string dateFormat = "MM_dd_yy") =>
        PacketCatalog.ExpandFileName(PdfFileNamePattern, r.Client.FirstName, r.Client.LastName, today, dateFormat) + ".pdf";

    /// <summary>
    /// The newest Fuel Order PDF already in the client's folder ("J_Smith_fuel_order_11_23_26.pdf",
    /// newest by the date in its name), or null if there isn't one.
    /// </summary>
    public static string? FindSavedPdf(IEnumerable<string> fileNames, IntakeRecord r, string dateFormat = "MM_dd_yy")
    {
        var prefix = PacketCatalog.ExpandFileName("{Initial}_{Last}_fuel_order_", r.Client.FirstName, r.Client.LastName, default);
        return fileNames
            .Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .Select(f => (File: f, Date: DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(f)[prefix.Length..].Split(' ')[0],
                dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateOnly.MinValue))
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.File, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.File)
            .FirstOrDefault();
    }

    private static Dictionary<string, string> Values(IntakeRecord r, IntakeCalculations calc)
    {
        var o = r.FuelElectric.FuelOrder;
        static string D(DateOnly? d) => d?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) ?? "";
        return new(StringComparer.OrdinalIgnoreCase)
        {
            ["ClientName"] = calc.ClientFullName,
            ["First"] = r.Client.FirstName?.Trim() ?? "",
            ["Last"] = r.Client.LastName?.Trim() ?? "",
            ["ServiceAddress"] = calc.ServiceAddressLine1,
            ["ServiceTown"] = calc.ServiceAddressLine2,
            ["ClientPhone"] = Phone.IsValid(r.Client.Phone) ? Phone.Format(r.Client.Phone) : r.Client.Phone?.Trim() ?? "",
            ["Vendor"] = calc.FuelVendor?.Name.Trim() ?? r.FuelElectric.Vendor?.Trim() ?? "",
            ["Account"] = r.FuelElectric.VendorAccount?.Trim() ?? "",
            ["FuelType"] = r.FuelElectric.FuelType?.Trim() ?? "",
            ["FuelAmount"] = o.Fuel is null ? "" : Money.Format(o.Fuel),
            ["OrderTotal"] = calc.FuelOrderTotal is null ? "" : Money.Format(calc.FuelOrderTotal),
            ["Gallons"] = o.Gallons?.Trim() ?? "",
            ["DeliveryDate"] = D(o.EstimatedDelivery),
            ["FundingSource"] = o.FundingSource?.Trim() ?? "",
            ["OrderDate"] = D(o.Date),
            ["StaffName"] = r.Staff.StaffName?.Trim() ?? "",
            ["StaffPhone"] = Phone.IsValid(calc.StaffPhone) ? Phone.Format(calc.StaffPhone) : calc.StaffPhone,
            ["StaffEmail"] = calc.StaffEmail,
            ["InvoiceEmail"] = calc.InvoiceEmail,
        };
    }

    /// <summary>Replaces {Placeholders}; unknown ones are left as typed so a typo is easy to spot.</summary>
    public static string Fill(string template, IReadOnlyDictionary<string, string> values) =>
        TokenPattern().Replace(template, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

    /// <summary>
    /// Fills the message. A line that is only "Label: {Placeholder}" is left out when that detail is blank
    /// (no "Gallons:" with nothing after it); "Hello {Vendor}," with no vendor becomes "Hello,".
    /// </summary>
    public static string FillBody(string template, IReadOnlyDictionary<string, string> values)
    {
        var lines = new List<string>();
        foreach (var line in template.Replace("\r\n", "\n").Split('\n'))
        {
            var m = LabelLinePattern().Match(line);
            if (m.Success && values.TryGetValue(m.Groups[1].Value, out var v) && string.IsNullOrWhiteSpace(v)) continue;
            lines.Add(Regex.Replace(Fill(line, values), @"[ \t]+([,.])", "$1"));
        }
        return string.Join("\r\n", lines).Trim();
    }

    private static string OneLine(string s) => Regex.Replace(Regex.Replace(s, @"\s+", " "), @"( - )+$", "").Trim().Trim('-').Trim();

    [GeneratedRegex(@"[A-Za-z0-9._%+'-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\{([A-Za-z]+)\}")]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"^\s*[^:{}]{1,40}:\s*\{([A-Za-z]+)\}\s*$")]
    private static partial Regex LabelLinePattern();
}
