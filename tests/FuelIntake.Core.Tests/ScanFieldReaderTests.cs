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
