using FuelIntake.Core.Import;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Tests;

public class PaperImportTests
{
    private static readonly StaffStamp Pat = new("Pat Example", "pexample@example.org");
    private static readonly DateTimeOffset At = new(2026, 11, 23, 9, 0, 0, TimeSpan.FromHours(-5));

    private static PaperImport NewImport(int members = 2) =>
        new() { CreatedAt = At, MemberCount = members, Fields = ImportFieldCatalog.NewImport(members) };

    private static void Fill(PaperImport i, string path, string? value) =>
        PaperImportRules.SetValue(i.Fields.Single(f => f.Path == path), value);

    private static void ConfirmAll(PaperImport i)
    {
        foreach (var f in i.Fields) PaperImportRules.SetConfirmed(f, true, Pat, At);
    }

    [Fact]
    public void Build_PutsEveryConfirmedValueInItsPlace()
    {
        var i = NewImport();
        Fill(i, "client.lastName", "Sampleton");
        Fill(i, "client.firstName", "Testy");
        Fill(i, "client.dateOfBirth", "4/9/1951");
        Fill(i, "client.phone", "802 555 0142");
        Fill(i, "client.benefits.snap", "Yes");
        Fill(i, "client.housingType", "rent");
        Fill(i, "household[0].ssn", "000123401");
        Fill(i, "household[0].incomePerMonth", "$1,480");
        Fill(i, "household[1].firstName", "Partner");
        Fill(i, "household[1].lastName", "Sampleton");
        Fill(i, "household[1].dateOfBirth", "02/03/2010");
        Fill(i, "incomeExpenses.income.SSA", "1480");
        ConfirmAll(i);

        var r = PaperImportRules.Build(i, FakeIntake.Today);
        Assert.Equal("Testy", r.Client.FirstName);
        Assert.Equal(new DateOnly(1951, 4, 9), r.Client.DateOfBirth);
        Assert.Equal("(802) 555-0142", r.Client.Phone);
        Assert.True(r.Client.Benefits.Snap);
        Assert.False(r.Client.Benefits.Wic);
        Assert.Equal("Rent", r.Client.HousingType); // matched to the list's spelling
        Assert.Equal(2, r.Household.Count);
        Assert.Equal("000-12-3401", r.Household[0].Ssn);
        Assert.Equal(1480m, r.Household[0].IncomePerMonth);
        Assert.Equal("Partner", r.Household[1].FirstName);
        Assert.Equal(new DateOnly(2010, 2, 3), r.Household[1].DateOfBirth);
        Assert.Equal(1480m, r.IncomeExpenses.Income["SSA"]);
        Assert.Equal(FakeIntake.Today, r.Staff.AppDate);
        Assert.False(r.IsTestData);
    }

    [Fact]
    public void Build_RefusesUntilEveryFieldIsConfirmed_AndValuesWork()
    {
        var i = NewImport(1);
        Fill(i, "client.lastName", "Sampleton");
        Fill(i, "client.firstName", "Testy");
        ConfirmAll(i);
        PaperImportRules.SetConfirmed(i.Fields[5], false, Pat, At);
        Assert.Throws<InvalidOperationException>(() => PaperImportRules.Build(i, FakeIntake.Today));

        ConfirmAll(i);
        Fill(i, "client.zip", "5819"); // editing un-confirms it…
        Assert.Contains(PaperImportRules.Blockers(i, FakeIntake.Today), b => b.Contains("still to check"));
        ConfirmAll(i);                  // …and a bad value still blocks
        Assert.Contains(PaperImportRules.Blockers(i, FakeIntake.Today), b => b.Contains("to fix"));
        Fill(i, "client.zip", "05819");
        ConfirmAll(i);
        Assert.Empty(PaperImportRules.Blockers(i, FakeIntake.Today));
    }

    [Fact]
    public void Problems_ByKind()
    {
        ImportField F(ImportFieldKind k, string v, List<string>? o = null) => new() { Kind = k, Value = v, Options = o };
        var today = FakeIntake.Today;
        Assert.NotNull(PaperImportRules.Problem(F(ImportFieldKind.Date, "13/40/2020"), today));
        Assert.NotNull(PaperImportRules.Problem(F(ImportFieldKind.Date, today.AddDays(1).ToString("MM/dd/yyyy")), today));
        Assert.Null(PaperImportRules.Problem(F(ImportFieldKind.Money, "$1,200.50"), today));
        Assert.NotNull(PaperImportRules.Problem(F(ImportFieldKind.Money, "about 50"), today));
        Assert.NotNull(PaperImportRules.Problem(F(ImportFieldKind.Ssn, "12-345"), today));
        Assert.NotNull(PaperImportRules.Problem(F(ImportFieldKind.Choice, "Maybe", ["Y", "N"]), today));
        Assert.Null(PaperImportRules.Problem(F(ImportFieldKind.Choice, "y", ["Y", "N"]), today));
    }

    [Fact]
    public void Members_AddAndRemove_KeepOrder_AndNamesShow()
    {
        var i = NewImport(1);
        Assert.Equal("Paper application from Nov 23", PaperImportRules.DisplayName(i));
        PaperImportRules.AddMember(i);
        Assert.Equal(2, i.MemberCount);
        var firstIncome = i.Fields.FindIndex(f => f.Path.StartsWith("incomeExpenses", StringComparison.Ordinal));
        var lastMember2 = i.Fields.FindLastIndex(f => f.Path.StartsWith("household[1]", StringComparison.Ordinal));
        Assert.True(lastMember2 < firstIncome);
        PaperImportRules.RemoveLastMember(i);
        PaperImportRules.RemoveLastMember(i); // member 1 stays
        Assert.Equal(1, i.MemberCount);
        Assert.DoesNotContain(i.Fields, f => f.Path.StartsWith("household[1]", StringComparison.Ordinal));
        Fill(i, "client.lastName", "Sampleton");
        Assert.Equal("Sampleton", PaperImportRules.DisplayName(i));
    }
}
