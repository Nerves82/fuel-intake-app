using System.Text;

namespace FuelIntake.Core.Contacts;

/// <summary>
/// Reads the "Fuel Team Vendor List" exported from Microsoft Lists (Export → Export to CSV).
/// Columns are found by name, so their order doesn't matter. The name column may be called
/// "Vender Name" (as in the List) or "Title" (SharePoint's internal name).
/// </summary>
public static class VendorCsv
{
    public sealed record Result(IReadOnlyList<Vendor> Vendors, IReadOnlyList<string> Warnings);

    private static readonly string[] NameHeaders = ["Vender Name", "Vendor Name", "Title", "Name"];

    public static Result Parse(string csv)
    {
        // An export "with schema" starts with a ListSchema={...} line describing the columns: skip it.
        var text = csv.TrimStart('\uFEFF');
        if (text.StartsWith("ListSchema=", StringComparison.Ordinal))
        {
            var end = text.IndexOf('\n');
            text = end < 0 ? "" : text[(end + 1)..];
        }
        var rows = ReadRows(text);
        var warnings = new List<string>();

        // Find the header row (an export "with schema" can have extra lines first).
        var headerIndex = rows.FindIndex(r => r.Any(c => NameHeaders.Contains(c.Trim(), StringComparer.OrdinalIgnoreCase)));
        if (headerIndex < 0)
            return new([], ["Couldn't find a \"Vender Name\" column. Export the Fuel Team Vendor List from Microsoft Lists as CSV."]);

        var header = rows[headerIndex].Select(h => h.Trim()).ToList();
        int Col(params string[] names) => header.FindIndex(h => names.Contains(h, StringComparer.OrdinalIgnoreCase));

        var name = Col(NameHeaders);
        var location = Col("Location");
        var emails = Col("Contact Emails", "Contact Email");
        var phones = Col("Contact Phone Numbers", "Contact Phones", "Phone");
        var offered = Col("Fuel Offered");
        var minDelivery = Col("Minimum Delivery");
        var tripFee = Col("Emergency Trip Fee");
        var pressure = Col("Preassure Test/Leak Check Fee", "Pressure Test/Leak Check Fee");
        var notes = Col("Notes");

        foreach (var (col, label) in new[] { (emails, "Contact Emails"), (phones, "Contact Phone Numbers"), (offered, "Fuel Offered") })
            if (col < 0) warnings.Add($"No \"{label}\" column found; that detail will be blank.");

        var vendors = new List<Vendor>();
        var skipped = new List<string>();
        foreach (var row in rows.Skip(headerIndex + 1))
        {
            string? Get(int i) => i >= 0 && i < row.Count && !string.IsNullOrWhiteSpace(row[i]) ? row[i].Trim() : null;
            var n = Get(name);
            if (n is null)
            {
                var hint = string.Join(", ", new[] { Get(location), Get(phones) }.Where(x => x is not null));
                if (hint.Length > 0) skipped.Add(hint.Replace("\n", " "));
                continue;
            }
            vendors.Add(new Vendor(n, Get(location), Get(emails), Get(phones), CleanChoices(Get(offered)),
                Get(minDelivery), Get(tripFee), Dollars(Get(pressure)), Get(notes)));
        }
        if (vendors.Count == 0) warnings.Add("The file has no vendor rows.");
        foreach (var s in skipped) warnings.Add($"Skipped a row with no Vender Name ({s}). Give it a name in the List to include it.");
        return new(vendors, warnings);
    }

    /// <summary>Multi-choice values can export as ["Oil","Propane"] or Oil;#Propane: turn them into "Oil, Propane".</summary>
    public static string? CleanChoices(string? value)
    {
        if (value is null) return null;
        var parts = value.Trim().TrimStart('[').TrimEnd(']')
            .Split([";#", ",", ";", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim().Trim('"').Trim())
            .Where(p => p.Length > 0);
        var joined = string.Join(", ", parts);
        return joined.Length == 0 ? null : joined;
    }

    /// <summary>
    /// Number columns export as plain numbers ("100"); show them as dollars. 0 is the List's default
    /// for an empty Number column, so it's treated as "not set".
    /// </summary>
    public static string? Dollars(string? value)
    {
        if (value is null) return null;
        if (decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d == 0 ? null : "$" + d.ToString("#,##0.##", System.Globalization.CultureInfo.InvariantCulture);
        return value;
    }

    /// <summary>The vendor list that ships with the app (Fuel Team Vendor List export).</summary>
    public static Result BuiltIn()
    {
        using var s = typeof(VendorCsv).Assembly.GetManifestResourceStream("Fuel_Team_Vendor_List.csv")
                      ?? throw new InvalidOperationException("Built-in vendor list missing.");
        using var reader = new StreamReader(s);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>CSV per RFC 4180: quoted fields may contain commas, quotes ("") and line breaks.</summary>
    public static List<List<string>> ReadRows(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var text = csv.TrimStart('﻿');

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (ch == '"') inQuotes = false;
                else field.Append(ch);
            }
            else if (ch == '"' && field.Length == 0) inQuotes = true;   // a quote mid-field is just a character
            else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                if (row.Any(c => c.Length > 0)) rows.Add(row);
                row = [];
            }
            else field.Append(ch);
        }
        row.Add(field.ToString());
        if (row.Any(c => c.Length > 0)) rows.Add(row);
        return rows;
    }
}
