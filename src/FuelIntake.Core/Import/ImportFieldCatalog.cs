using FuelIntake.Core.Lists;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Import;

/// <summary>
/// The fields a paper application fills, in the order the approval page lists them. Every one of them
/// has to be confirmed, blank ones included, so a field the scan reader missed can't slip through.
/// </summary>
public static class ImportFieldCatalog
{
    public const string ClientGroup = "Client";
    public const string BenefitsGroup = "Benefits received";
    public const string IncomeGroup = "Income (monthly)";
    public static string MemberGroup(int number) => number == 1 ? "Member 1 (the client)" : $"Member {number}";

    public static List<ImportField> ClientFields() =>
    [
        F("client.lastName", "Last name", ClientGroup),
        F("client.firstName", "First name", ClientGroup),
        F("client.dateOfBirth", "Date of birth", ClientGroup, ImportFieldKind.Date),
        F("client.addressPhysical", "Physical address", ClientGroup),
        F("client.city", "Town", ClientGroup),
        F("client.state", "State", ClientGroup),
        F("client.zip", "Zip", ClientGroup, ImportFieldKind.Zip),
        F("client.addressMailing", "Mailing address (if different)", ClientGroup),
        F("client.phone", "Phone", ClientGroup, ImportFieldKind.Phone),
        F("client.email", "Email", ClientGroup),
        F("client.county", "County", ClientGroup, ImportFieldKind.Choice, ChoiceLists.County),
        F("client.primaryLanguage", "Primary language", ClientGroup),
        F("client.housingType", "Housing type", ClientGroup, ImportFieldKind.Choice, ChoiceLists.Housing),
        F("client.householdType", "Household type", ClientGroup, ImportFieldKind.Choice, ChoiceLists.HouseholdType),
        F("client.benefits.snap", "SNAP (3SquaresVT)", BenefitsGroup, ImportFieldKind.Check),
        F("client.benefits.snapAmount", "SNAP amount", BenefitsGroup, ImportFieldKind.Money),
        F("client.benefits.wic", "WIC", BenefitsGroup, ImportFieldKind.Check),
        F("client.benefits.liheap", "LIHEAP / Seasonal Fuel", BenefitsGroup, ImportFieldKind.Check),
        F("client.benefits.housingChoiceVoucher", "Housing Choice Voucher", BenefitsGroup, ImportFieldKind.Check),
        F("client.benefits.publicHousing", "Public housing", BenefitsGroup, ImportFieldKind.Check),
        F("client.benefits.childcareVoucher", "Childcare voucher", BenefitsGroup, ImportFieldKind.Check),
        F("client.benefits.acaSubsidy", "ACA subsidy", BenefitsGroup, ImportFieldKind.Check),
    ];

    /// <summary>Fields for household member <paramref name="number"/> (1 = the client, whose name comes from the client fields).</summary>
    public static List<ImportField> MemberFields(int number)
    {
        var g = MemberGroup(number);
        var p = $"household[{number - 1}]";
        var list = new List<ImportField>();
        if (number > 1)
        {
            list.Add(F($"{p}.lastName", "Last name", g));
            list.Add(F($"{p}.firstName", "First name", g));
            list.Add(F($"{p}.dateOfBirth", "Date of birth", g, ImportFieldKind.Date));
        }
        list.AddRange(
        [
            F($"{p}.ssn", "SSN", g, ImportFieldKind.Ssn),
            F($"{p}.gender", "Gender identity", g, ImportFieldKind.Choice, ChoiceLists.Gender),
            F($"{p}.disabled", "Disabled (Y/N)", g, ImportFieldKind.Choice, ChoiceLists.YesNoLetter),
            F($"{p}.education", "Last grade completed", g, ImportFieldKind.Choice, ChoiceLists.Education),
            F($"{p}.race", "Race", g, ImportFieldKind.Choice, ChoiceLists.Race),
            F($"{p}.hispanic", "Hispanic/Latino?", g, ImportFieldKind.Choice, ChoiceLists.Hispanic),
            F($"{p}.healthInsurance", "Health insurance source", g, ImportFieldKind.Choice, ChoiceLists.HealthInsurance),
            F($"{p}.militaryStatus", "Military status", g, ImportFieldKind.Choice, ChoiceLists.Military),
            F($"{p}.occupation", "Occupation (14+)", g, ImportFieldKind.Choice, ChoiceLists.Occupation),
            F($"{p}.incomePerMonth", "Income per month", g, ImportFieldKind.Money),
            F($"{p}.incomeSource", "Income source", g, ImportFieldKind.Choice, ChoiceLists.IncomeSource),
        ]);
        return list;
    }

    public static List<ImportField> IncomeFields() =>
        IncomeExpenseCatalog.IncomeLines
            .Select(l => F($"incomeExpenses.income.{l.Key}", l.Label, IncomeGroup, ImportFieldKind.Money))
            .ToList();

    /// <summary>All fields for a new paper application with <paramref name="members"/> household members.</summary>
    public static List<ImportField> NewImport(int members = 1)
    {
        var list = ClientFields();
        for (var n = 1; n <= Math.Max(1, members); n++) list.AddRange(MemberFields(n));
        list.AddRange(IncomeFields());
        return list;
    }

    private static ImportField F(string path, string label, string group, ImportFieldKind kind = ImportFieldKind.Text, IReadOnlyList<string>? options = null) =>
        new() { Path = path, Label = label, Group = group, Kind = kind, Options = options?.ToList() };
}
