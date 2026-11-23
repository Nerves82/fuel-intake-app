using FuelIntake.Core.Import;

namespace FuelIntake.Core.Tests;

public class ScanFieldReaderTests
{
    /// <summary>A fake page: each row is a line of words at a given height; x advances word by word.</summary>
    private static OcrPage Page(params (double Y, string Text)[] rows) => Page(rows.Select(r => (r.Y, 0.05, r.Text)).ToArray());

    private static OcrPage Page((double Y, double X, string Text)[] rows) =>
        new("scan.pdf", 1, rows.Select(r =>
        {
            var x = r.X;
            var words = new List<OcrWord>();
            foreach (var w in r.Text.Split(' '))
            {
                words.Add(new OcrWord(w, x, r.Y, 0.012 * w.Length, 0.015));
                x += 0.012 * w.Length + 0.01;
            }
            return new OcrLine(words);
        }).ToList());

    private static PaperImport Import() => new() { Fields = ImportFieldCatalog.NewImport(1) };
    private static string? V(PaperImport i, string path) => i.Fields.Single(f => f.Path == path).Value;

    [Fact]
    public void ReadsValuesNextToTheirLabels()
    {
        var page = Page(
            (0.10, "Last Name: Sampleton First Name: Testy"),
            (0.14, "Date of Birth 4/9/1951 SSN 000-12-3401"),
            (0.18, "Mailing Address: PO Box 0 Phone (802) 555-0142"),
            (0.22, "Physical Address: 12 Pretend Pond Rd"),
            (0.26, "Town St. Johnsbury Zip 05819"),
            (0.30, "Social Security Retirement (SSA) $1,480.00"));
        var i = Import();
        var n = ScanFieldReader.Fill(i, [page]);

        Assert.Equal("Sampleton", V(i, "client.lastName"));
        Assert.Equal("Testy", V(i, "client.firstName"));
        Assert.Equal("04/09/1951", V(i, "client.dateOfBirth"));
        Assert.Equal("000-12-3401", V(i, "household[0].ssn"));
        Assert.Equal("PO Box 0", V(i, "client.addressMailing"));
        Assert.Equal("12 Pretend Pond Rd", V(i, "client.addressPhysical")); // not the mailing one
        Assert.Equal("(802) 555-0142", V(i, "client.phone"));
        Assert.Equal("05819", V(i, "client.zip"));
        Assert.Equal("1480.00", V(i, "incomeExpenses.income.SSA"));
        Assert.True(n >= 9);
        var ssn = i.Fields.Single(f => f.Path == "household[0].ssn");
        Assert.Equal(ssn.Value, ssn.ReadValue);
        Assert.NotNull(ssn.Spot);
        Assert.Equal(0.6, ssn.Confidence);
    }

    [Fact]
    public void ReadsWritingAboveOrBelowALabel()
    {
        // value written on the line, label printed underneath
        var page = Page([(0.10, 0.10, "Sampleton"), (0.13, 0.10, "Last Name"), (0.20, 0.10, "Date of Birth"), (0.23, 0.10, "02/03/1960")]);
        var i = Import();
        ScanFieldReader.Fill(i, [page]);
        Assert.Equal("Sampleton", V(i, "client.lastName"));
        Assert.Equal("02/03/1960", V(i, "client.dateOfBirth"));
    }

    [Fact]
    public void NeverOverwritesStaffWork_AndFlagsShakyReads()
    {
        var page = Page((0.10, "Last Name: Sampletin"), (0.14, "Date of Birth: sometime"));
        var i = Import();
        var last = i.Fields.Single(f => f.Path == "client.lastName");
        last.Value = "Sampleton";
        ScanFieldReader.Fill(i, [page]);
        Assert.Equal("Sampleton", last.Value);
        var dob = i.Fields.Single(f => f.Path == "client.dateOfBirth");
        Assert.Equal("sometime", dob.Value);
        Assert.Equal(0.3, dob.Confidence); // doesn't look like a date: shown as unsure
    }
}

public class ScanFieldReaderLayoutTests
{
    private static OcrLine L(double y, double x, string text)
    {
        var words = new List<OcrWord>();
        foreach (var w in text.Split(' ')) { words.Add(new OcrWord(w, x, y, 0.012 * w.Length, 0.015)); x += 0.012 * w.Length + 0.01; }
        return new OcrLine(words);
    }

    private static string? V(PaperImport i, string path) => i.Fields.Single(f => f.Path == path).Value;

    [Fact]
    public void ReadsTablesWhereLabelAndValueAreSeparatePieces()
    {
        // a printed profile: labels in one column, values far to the right (the reader returns separate lines)
        var page = new OcrPage("profile.pdf", 1,
        [
            L(0.100, 0.05, "Last Name"), L(0.101, 0.45, "Sampleton"),
            L(0.130, 0.05, "Birth Date"), L(0.131, 0.45, "04/09/1951"),
            L(0.160, 0.05, "Primary Phone"), L(0.159, 0.45, "802-555-0142"),
            L(0.190, 0.05, "Address 1"), L(0.190, 0.45, "12 Pretend Pond Rd"),
            L(0.220, 0.05, "Last grade completed"), L(0.220, 0.45, "Some college (but no degree)"),
        ]);
        var i = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        ScanFieldReader.Fill(i, [page]);
        Assert.Equal("Sampleton", V(i, "client.lastName"));
        Assert.Equal("04/09/1951", V(i, "client.dateOfBirth"));
        Assert.Equal("(802) 555-0142", V(i, "client.phone"));
        Assert.Equal("12 Pretend Pond Rd", V(i, "client.addressPhysical"));
        Assert.Equal("Some college (but no degree)", V(i, "household[0].education"));
    }

    [Fact]
    public void WholeName_IsSplit_AndRereadingReplacesOnlyUneditedReadings()
    {
        var page = new OcrPage("p.pdf", 1, [L(0.1, 0.05, "Name: Sampleton, Testy"), L(0.14, 0.05, "Staff Name: Pat Example")]);
        var i = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        ScanFieldReader.Fill(i, [page]);
        Assert.Equal("Sampleton", V(i, "client.lastName"));
        Assert.Equal("Testy", V(i, "client.firstName"));

        var better = new OcrPage("p.pdf", 1, [L(0.1, 0.05, "Last Name Sampletown"), L(0.14, 0.05, "First Name Testy")]);
        i.Fields.Single(f => f.Path == "client.firstName").Value = "Tester"; // staff edit stays
        ScanFieldReader.Fill(i, [better]);
        Assert.Equal("Sampletown", V(i, "client.lastName"));
        Assert.Equal("Tester", V(i, "client.firstName"));
    }

    [Fact]
    public void MaskedLayout_KeepsLabels_HidesDetails()
    {
        var page = new OcrPage("Sampleton scan.pdf", 1, [L(0.1, 0.05, "Last Name: Sampleton SSN 000-12-3401")]);
        var text = ScanFieldReader.MaskedLayout([page]);
        Assert.Contains("Last Name: xxxxxxxxx SSN 999-99-9999", text);
        Assert.DoesNotContain("Sampleton", text);
        Assert.DoesNotContain("3401", text);
    }
}

public class ScanFieldReaderProfileTests
{
    private static OcrLine L(double y, double x, string text)
    {
        var words = new List<OcrWord>();
        foreach (var w in text.Split(' ')) { words.Add(new OcrWord(w, x, y, 0.012 * w.Length, 0.012)); x += 0.012 * w.Length + 0.01; }
        return new OcrLine(words);
    }

    private static string? V(PaperImport i, string path) => i.Fields.Single(f => f.Path == path).Value;

    [Fact]
    public void ReadsAGoEngageStyleProfile()
    {
        // rows as the reader returns them from a printed intake profile (made-up details)
        var page = new OcrPage("profile.pdf", 1,
        [
            L(0.184, 0.037, "First Name: Testy Middle Name: Last Name: Sampleton"),
            L(0.203, 0.037, "Suffix: Preferred name:"),
            L(0.215, 0.037, "DOB: 4/9/1951 Gender: Female Email: testy@example.org"),
            L(0.232, 0.037, "Mobile #:802-555-0142 Mobile Provider: None"),
            L(0.248, 0.037, "Race: White Ethnicity: Hispanic or Latino: No"),
            L(0.264, 0.037, "Disabled: No Military Status: Never Served SNAP: Yes"),
            L(0.278, 0.647, "Highest Education: Some college (but no degree)"),
            L(0.310, 0.037, "Insurance: Medicaid"),
            L(0.329, 0.037, "SSN: 000-12-3401 Marital Status: Single"),
            L(0.379, 0.495, "Street Address: 12 Pretend Pond Rd"),
            L(0.409, 0.496, "State: Vermont Zip Code: 05819"),
        ]);
        var i = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        ScanFieldReader.Fill(i, [page]);
        Assert.Equal("Testy", V(i, "client.firstName"));
        Assert.Equal("Sampleton", V(i, "client.lastName"));
        Assert.Equal("04/09/1951", V(i, "client.dateOfBirth"));
        Assert.Equal("Female", V(i, "household[0].gender"));
        Assert.Equal("testy@example.org", V(i, "client.email"));
        Assert.Equal("(802) 555-0142", V(i, "client.phone"));
        Assert.Equal("White", V(i, "household[0].race"));
        Assert.Equal("No", V(i, "household[0].hispanic"));
        Assert.Equal("N", V(i, "household[0].disabled"));
        Assert.Equal("Never", V(i, "household[0].militaryStatus"));
        Assert.Equal("Yes", V(i, "client.benefits.snap"));
        Assert.Equal("Some college (but no degree)", V(i, "household[0].education"));
        Assert.Equal("Medicaid", V(i, "household[0].healthInsurance"));
        Assert.Equal("000-12-3401", V(i, "household[0].ssn"));
        Assert.Equal("12 Pretend Pond Rd", V(i, "client.addressPhysical"));
        Assert.Equal("Vermont", V(i, "client.state"));
        Assert.Equal("05819", V(i, "client.zip"));
    }
}

public class ScanFieldReaderChoiceTests
{
    [Fact]
    public void MisreadChoices_SnapToTheClosestOption_OrStayBlank()
    {
        Assert.Equal("High School plus some college",
            ScanFieldReader.ClosestChoice("High School phis some ccNege", FuelIntake.Core.Lists.ChoiceLists.Education));
        Assert.Null(ScanFieldReader.ClosestChoice("Banana bread", FuelIntake.Core.Lists.ChoiceLists.Education));

        OcrLine L(double y, string text)
        {
            double x = 0.05; var words = new List<OcrWord>();
            foreach (var w in text.Split(' ')) { words.Add(new OcrWord(w, x, y, 0.012 * w.Length, 0.012)); x += 0.012 * w.Length + 0.01; }
            return new OcrLine(words);
        }
        var page = new OcrPage("p.pdf", 1, [L(0.1, "Highest Education: High School phis some ccNege"), L(0.2, "Race: Banana bread")]);
        var i = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        ScanFieldReader.Fill(i, [page]);

        var edu = i.Fields.Single(f => f.Path == "household[0].education");
        Assert.Equal("High School plus some college", edu.Value);
        Assert.True(edu.Confidence < 0.5); // marked unsure

        var race = i.Fields.Single(f => f.Path == "household[0].race");
        Assert.Null(race.Value);                 // no nonsense in the box…
        Assert.Equal("Banana bread", race.ReadValue); // …but what the scan said is shown
    }
}
