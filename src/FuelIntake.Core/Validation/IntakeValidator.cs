using FuelIntake.Core.Lists;
using FuelIntake.Core.Formatting;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Validation;

public enum IssueSeverity { Info, Warning, Error }

/// <summary>A problem to show before printing. <paramref name="Section"/> names the screen ("Client", "Household"…).</summary>
/// <param name="Path">The field it's about, as a <see cref="RecordPaths"/> path ("client.zip", "household[2].ssn"), so the
/// screen can mark that field. Null for issues about several fields or a total.</param>
/// <param name="Rule">The rule it comes from (a <see cref="Rules.CrisisFuelRules"/> id), for a "?" link to it.</param>
public sealed record ValidationIssue(IssueSeverity Severity, string Section, string Field, string Message, string? Path = null, string? Rule = null);

/// <summary>
/// Checks an intake for mistakes the workbook used to catch with cell validation, plus cross-tab checks.
/// Errors should be fixed before printing; warnings are worth a look.
/// </summary>
public static class IntakeValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(IntakeRecord r, IntakeCalculations calc, DateOnly today)
    {
        var issues = new List<ValidationIssue>();
        void Add(IssueSeverity s, string section, string field, string msg, string? path = null) => issues.Add(new(s, section, field, msg, path));

        // Client
        if (string.IsNullOrWhiteSpace(r.Client.FirstName) || string.IsNullOrWhiteSpace(r.Client.LastName))
            Add(IssueSeverity.Error, "Client", "Name", "Enter the client's first and last name.",
                string.IsNullOrWhiteSpace(r.Client.FirstName) ? "client.firstName" : "client.lastName");
        if (r.Staff.AppDate is null)
            Add(IssueSeverity.Warning, "Client", "Application date", "No application date: forms will print without a date.", "staff.appDate");
        else if (r.Staff.AppDate > today)
            Add(IssueSeverity.Warning, "Client", "Application date", "The application date is in the future.", "staff.appDate");
        if (string.IsNullOrWhiteSpace(r.Staff.StaffName))
            Add(IssueSeverity.Warning, "Client", "Staff name", "Pick the staff member.", "staff.staffName");
        CheckPhone(r.Client.Phone, "Client", "Phone number", "client.phone");
        CheckPhone(r.Staff.PhoneOverride, "Client", "Staff phone", "staff.phoneOverride");
        if (!string.IsNullOrWhiteSpace(r.Client.Zip) && !Zip.IsValid(r.Client.Zip))
            Add(IssueSeverity.Error, "Client", "Zip", "Zip code must be exactly 5 numbers, e.g. 05819.", "client.zip");
        if (r.Client.DateOfBirth > today)
            Add(IssueSeverity.Error, "Client", "Date of birth", "The client's date of birth is in the future.", "client.dateOfBirth");
        if (r.Client.Benefits.Snap && r.Client.Benefits.SnapAmount is null)
            Add(IssueSeverity.Warning, "Client", "SNAP amount", "SNAP is ticked but no amount is entered.", "client.benefits.snapAmount");

        // Household
        foreach (var m in calc.Members)
        {
            var who = m.FullName.Length > 0 ? m.FullName : $"Member {m.Number}";
            var at = $"household[{m.Number - 1}]";
            if (!m.IsPresent && HasData(m.Data))
                Add(IssueSeverity.Warning, "Household", $"Member {m.Number}", $"Member {m.Number} has details but no name.", at + ".firstName");
            if (!string.IsNullOrWhiteSpace(m.Data.Ssn) && !Ssn.IsValid(m.Data.Ssn))
                Add(IssueSeverity.Error, "Household", $"{who}: SSN", $"{who}: SSN must be 9 digits (dashes optional).", at + ".ssn");
            if (m.Number > 1 && m.DateOfBirth > today)
                Add(IssueSeverity.Error, "Household", $"{who}: Date of birth", $"{who}: date of birth is in the future.", at + ".dateOfBirth");
            if (m.Data.IncomePerMonth < 0)
                Add(IssueSeverity.Error, "Household", $"{who}: Income", $"{who}: income can't be negative.", at + ".incomePerMonth");
            if (calc.AgeOf(m) is >= 14 && string.IsNullOrWhiteSpace(m.Data.Occupation) && m.IsPresent)
                Add(IssueSeverity.Info, "Household", $"{who}: Occupation", $"{who} is 14 or older: pick an occupation.", at + ".occupation");
        }

        // Income & eligibility
        if (!calc.HouseholdIncomeMatchesWorksheet)
            Add(IssueSeverity.Warning, "Income & Expenses", "Total income",
                $"Household income column ({Money.Format(calc.HouseholdIncomeColumnTotal)}) doesn't match the worksheet total ({Money.Format(calc.IncomeTotal)}).");
        if (calc.UnderIncomeLimit == false)
            Add(IssueSeverity.Warning, "Income & Expenses", "FPL",
                $"Household is at {Percent.Format(calc.FplPercent)} of FPL, over the program limit.");
        if (r.IncomeExpenses.IncomeChangeNextMonth == "Yes" && string.IsNullOrWhiteSpace(r.IncomeExpenses.IncomeChangeExplanation))
            Add(IssueSeverity.Warning, "Income & Expenses", "Income change", "Income changes next month: add an explanation.", "incomeExpenses.incomeChangeExplanation");

        // "Other" income needs saying what it is
        if (r.IncomeExpenses.Income.GetValueOrDefault("Other") is > 0 && string.IsNullOrWhiteSpace(r.IncomeExpenses.IncomeOtherDescription))
            Add(IssueSeverity.Warning, "Income & Expenses", "Other income", "Say what the \"Other\" income is.", "incomeExpenses.incomeOtherDescription");
        for (var n = 0; n < r.Household.Count; n++)
            if (r.Household[n].IncomeSource?.Trim() == "Other" && string.IsNullOrWhiteSpace(r.Household[n].IncomeSourceOther))
                Add(IssueSeverity.Warning, "Household", $"Member {n + 1} income source", "Income source is \"Other\": say what it is.", $"household[{n}].incomeSourceOther");

        // Application & orders
        if (string.IsNullOrWhiteSpace(r.Application.CrisisType))
            Add(IssueSeverity.Warning, "Application", "Crisis type", "Pick the crisis type (decides Fuel Order or Electric Pledge).", "application.crisisType");
        var fuelOrder = r.FuelElectric.FuelOrder;
        if (r.Application.CrisisType?.Trim() != CrisisTypes.DisconnectNotice
            && (fuelOrder.Authorized || fuelOrder.Fuel is not null || !string.IsNullOrWhiteSpace(r.FuelElectric.Vendor))
            && string.IsNullOrWhiteSpace(r.FuelElectric.FuelType))
            Add(IssueSeverity.Warning, "Fuel & Electric", "Fuel type", "Pick the fuel type (tab 5, Fuel Order): it prints on the Fuel Order and the ACCESS form.", "fuelElectric.fuelType");
        if (!string.IsNullOrWhiteSpace(r.FuelElectric.Vendor) && calc.FuelVendor is null)
            Add(IssueSeverity.Info, "Fuel & Electric", "Fuel vendor", "Fuel vendor isn't on the vendor list, so contact details won't fill in.", "fuelElectric.vendor");
        if (!string.IsNullOrWhiteSpace(r.FuelElectric.ElectricProvider) && calc.ElectricVendor is null)
            Add(IssueSeverity.Info, "Fuel & Electric", "Electric provider", "Electric provider isn't on the vendor list, so contact details won't fill in.", "fuelElectric.electricProvider");

        // Compliance standard (Warmth, Crisis Fuel…): its required fields
        if (Compliance.ComplianceRules.Effective(r, calc.Context.ComplianceOrEmpty) is { } standard)
            foreach (var m in Compliance.ComplianceRules.Missing(r, standard))
                Add(IssueSeverity.Error, m.Field.Tab, m.Member is int n ? $"Member {n} {m.Field.Label}" : m.Field.Label,
                    $"Required for {standard.Name}: {(m.Field.IsCheck ? "tick" : "fill in")} {m.Field.Group} › {m.Field.Label}" +
                    (m.Member is int k ? $" for member {k}." : "."),
                    m.Member is int i ? m.Field.Key.Replace("household[*]", $"household[{i - 1}]") : m.Field.Key);
        // …and its rule checks (Crisis Fuel's income limit, delivery limit…)
        if (Compliance.ComplianceRules.Effective(r, calc.Context.ComplianceOrEmpty) is { } withChecks)
            foreach (var id in withChecks.Checks.Distinct())
                if (Compliance.ComplianceChecks.Find(id) is { } check)
                    foreach (var x in check.Run(r, calc, today))
                        issues.Add(new(x.Severity, x.Section, x.Field,
                            $"{withChecks.Name} rule {check.Rule?.Cite ?? check.RuleId}: {x.Message}", x.Path, check.RuleId));
        if (!string.IsNullOrWhiteSpace(r.Application.ComplianceStandard) && Compliance.ComplianceRules.Effective(r, calc.Context.ComplianceOrEmpty) is null)
            Add(IssueSeverity.Warning, "Review", "Compliance standard", $"\"{r.Application.ComplianceStandard}\" isn't a compliance standard any more; pick another.", "application.complianceStandard");

        // Grant/funding sources (only checked once the agency has a list)
        if (r.FuelElectric.FuelOrder.Authorized || r.FuelElectric.FuelOrder.Fuel is not null)
            CheckGrant("Fuel Order", r.FuelElectric.FuelOrder.FundingSource, calc.FuelOrderGrant, calc.FuelOrderTotal, GrantUse.Fuel, "fuelElectric.fuelOrder.fundingSource");
        if (r.FuelElectric.Invoice.Received)
            CheckGrant("Invoice", r.FuelElectric.Invoice.FundingSource, calc.InvoiceGrant, calc.InvoiceTotal, GrantUse.Fuel, "fuelElectric.invoice.fundingSource");
        if (r.FuelElectric.ElectricPledge.Authorized || r.FuelElectric.ElectricPledge.AmountRequired is not null)
            CheckGrant("Electric Pledge", r.FuelElectric.ElectricPledge.FundingSource, calc.PledgeGrant, calc.PledgeTotal, GrantUse.Electric, "fuelElectric.electricPledge.fundingSource");

        return issues;

        void CheckGrant(string what, string? entered, GrantSource? grant, decimal? total, GrantUse use, string path)
        {
            var field = $"{what} funding source";
            if (string.IsNullOrWhiteSpace(entered))
            {
                if (GrantSources.For(calc.Settings.GrantSources, use).Count > 0)
                    Add(IssueSeverity.Warning, "Fuel & Electric", field, $"Pick the grant/funding source for the {what}.", path);
                return;
            }
            if (calc.Settings.GrantSources.Count == 0) return;
            if (grant is null)
                Add(IssueSeverity.Info, "Fuel & Electric", field, $"\"{entered.Trim()}\" isn't on the grant source list.", path);
            else if (!grant.Active)
                Add(IssueSeverity.Warning, "Fuel & Electric", field, $"\"{grant.Name}\" is marked inactive in the grant source list.", path);
            else if (use == GrantUse.Fuel ? !grant.ForFuel : !grant.ForElectric)
                Add(IssueSeverity.Warning, "Fuel & Electric", field, $"\"{grant.Name}\" isn't set up for {(use == GrantUse.Fuel ? "fuel" : "electric")} assistance.", path);
            else if (grant.MaxPerAssist is { } max && total > max)
                Add(IssueSeverity.Warning, "Fuel & Electric", field,
                    $"{what} total {Money.Format(total)} is over the {grant.Name} limit of {Money.Format(max)}.", path);
        }

        void CheckPhone(string? value, string section, string field, string path)
        {
            if (!string.IsNullOrWhiteSpace(value) && !Phone.IsValid(value))
                Add(IssueSeverity.Error, section, field, $"{field}: enter a 10-digit phone number, e.g. 802-555-0123.", path);
        }
    }

    private static bool HasData(HouseholdMember m) =>
        m.DateOfBirth is not null || m.IncomePerMonth is not null || !string.IsNullOrWhiteSpace(m.Ssn)
        || !string.IsNullOrWhiteSpace(m.Occupation) || !string.IsNullOrWhiteSpace(m.IncomeSource);
}
