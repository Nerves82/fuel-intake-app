using FuelIntake.Core.Import;

namespace FuelIntake.Core.Tests;

/// <summary>Household tables on scanned applications: one row per person under column headings.</summary>
public class ScanHouseholdTests
{
    private const double Char = 0.0035, Gap = 0.003, LineH = 0.011, LineStep = 0.013;

    /// <summary>NEKCA Intake page 1's household columns: heading lines and relative widths.</summary>
    private static readonly (string[] Heading, double Width)[] Columns =
    [
        (["Last Name"], 11), (["First Name"], 10), (["Date of Birth"], 7.5), (["Gender", "Identity"], 5), (["Disabled", "(Y/N)"], 3.5),
        (["Last Grade", "Completed"], 7), (["Race"], 6), (["Hispanic or Not", "Hispanic"], 6),
        (["Health", "Insurance", "Source", "(Employer,", "Medicaid, Etc.)"], 7), (["Income per", "month"], 6.5), (["Source"], 7.5),
        (["Military Status", "(Veteran/ Active/", "None)"], 6.5), (["SS #"], 7.5),
    ];

    private static List<OcrWord> Words(string text, double x, double y)
    {
        var list = new List<OcrWord>();
        foreach (var w in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            list.Add(new OcrWord(w, x, y, Char * w.Length, LineH));
            x += Char * w.Length + Gap;
        }
        return list;
    }

    private static double Width(string text) => text.Length * Char;

    /// <summary>A landscape page: some labelled fields above, the household table (headings centred in tall cells, values
    /// left-aligned), then the certification line.</summary>
    private static OcrPage IntakePage1(params string[][] rows)
    {
        var total = Columns.Sum(c => c.Width);
        var lefts = new List<double>(); var x = 0.05;
        foreach (var c in Columns) { lefts.Add(x); x += c.Width / total * 0.9; }
        var lines = new List<OcrLine>
        {
            new(Words("Physical Address: 12 Pretend Pond Rd", 0.05, 0.10)),
            new(Words("Town: Faketown Zip: 05819", 0.05, 0.13)),
        };
        const double headTop = 0.20, headLines = 5;
        for (var c = 0; c < Columns.Length; c++)
        {
            var (heading, width) = Columns[c];
            var w = width / total * 0.9;
            var top = headTop + (headLines - heading.Length) / 2.0 * LineStep;
            for (var k = 0; k < heading.Length; k++)
                lines.Add(new OcrLine(Words(heading[k], lefts[c] + (w - Width(heading[k])) / 2, top + k * LineStep)));
        }
        var y = headTop + headLines * LineStep + 0.012;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
                if (row[c].Length > 0) lines.Add(new OcrLine(Words(row[c], lefts[c] + 0.003, y)));
            y += 0.03;
        }
        y += 0.03 * (10 - rows.Length) + 0.01; // the table's empty rows
        lines.Add(new OcrLine(Words("I certify that all information is correct to the best of my knowledge.", 0.05, y)));
        lines.Add(new OcrLine(Words("Participant Signature: Date:", 0.05, y + 0.03)));
        return new OcrPage("intake.pdf", 1, lines);
    }

    private static readonly string[][] FiveMembers =
    [
        ["Sampleton", "Testy", "4/9/1971", "Female", "N", "High School", "White", "Not Hispanic", "Medicaid", "$1,200.00", "Employment", "Never", "000-12-3401"],
        ["Sampleton", "Partner", "2/3/1970", "Male", "Y", "", "White", "Not Hispanic", "Medicare", "$980.00", "SSDI", "Veteran", "000-12-3402"],
        ["Sampleton", "Teen", "6/1/2010", "Male", "N", "9th grade", "White", "Not Hispanic", "Medicaid", "", "None", "", "000-12-3403"],
        ["Sampleton", "Kid", "8/15/2016", "Female", "N", "", "White", "Not Hispanic", "Medicaid", "", "None", "", "000-12-3404"],
        ["Example", "Cousin", "1/20/1999", "Non-binary", "N", "Bachelor's degree", "Asian", "Not Hispanic", "Employment Based", "$2,100.00", "Employment", "Never", "000-12-3405"],
    ];

    private static string? V(PaperImport i, string path) => i.Fields.SingleOrDefault(f => f.Path == path)?.Value;

    [Fact]
    public void EveryRowBecomesAMember()
    {
        var i = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        ScanFieldReader.Fill(i, [IntakePage1(FiveMembers)]);

        Assert.Equal(5, i.MemberCount);
        Assert.Equal("Sampleton", V(i, "client.lastName"));
        Assert.Equal("Testy", V(i, "client.firstName"));
        Assert.Equal("04/09/1971", V(i, "client.dateOfBirth"));
        Assert.Equal("000-12-3401", V(i, "household[0].ssn"));
        Assert.Equal("Partner", V(i, "household[1].firstName"));
        Assert.Equal("02/03/1970", V(i, "household[1].dateOfBirth"));
        Assert.Equal("Y", V(i, "household[1].disabled"));
        Assert.Equal("Veteran", V(i, "household[1].militaryStatus"));
        Assert.Equal("980.00", V(i, "household[1].incomePerMonth"));
        Assert.Equal("Medicare", V(i, "household[1].healthInsurance"));
        Assert.Equal("9th grade", V(i, "household[2].education"));
        Assert.Equal("Kid", V(i, "household[3].firstName"));
        Assert.Equal("Example", V(i, "household[4].lastName"));
        Assert.Equal("Non-binary", V(i, "household[4].gender"));
        Assert.Equal("Asian", V(i, "household[4].race"));
        Assert.Equal("000-12-3405", V(i, "household[4].ssn"));
        Assert.Equal("2100.00", V(i, "household[4].incomePerMonth"));
        Assert.Equal("Employment", V(i, "household[4].incomeSource"));
        Assert.NotNull(i.Fields.Single(f => f.Path == "household[4].ssn").Spot);
        // still the labelled fields above the table
        Assert.Equal("05819", V(i, "client.zip"));
    }

    [Fact]
    public void HeadingWordsAreNotReadAsAnswers()
    {
        // the client left Disabled and Military blank: "(Y/N)" and "(Veteran/ Active/" under the headings aren't answers
        var row = (string[])FiveMembers[0].Clone(); row[4] = ""; row[11] = "";
        var i = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        ScanFieldReader.Fill(i, [IntakePage1(row)]);
        Assert.Equal(1, i.MemberCount);
        Assert.Null(V(i, "household[0].disabled"));
        Assert.Null(V(i, "household[0].militaryStatus"));
    }

    [Fact]
    public void ReadingAgain_KeepsStaffWorkAndNeverRemovesMembers()
    {
        var i = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        ScanFieldReader.Fill(i, [IntakePage1(FiveMembers)]);
        var name = i.Fields.Single(f => f.Path == "household[2].firstName");
        PaperImportRules.SetValue(name, "Teenager");
        PaperImportRules.AddMember(i); // staff added a sixth by hand

        ScanFieldReader.Fill(i, [IntakePage1(FiveMembers)]);
        Assert.Equal(6, i.MemberCount);
        Assert.Equal("Teenager", V(i, "household[2].firstName"));
    }

    [Fact]
    public void GoEngageStyleTable_ShortHeadings()
    {
        var page = new OcrPage("goengage.pdf", 1,
        [
            new(Words("Last", 0.05, 0.20).Concat(Words("First", 0.15, 0.20)).Concat(Words("DOB", 0.25, 0.20)).Concat(Words("Age", 0.32, 0.20))
                .Concat(Words("SSN", 0.40, 0.20)).ToList()),
            new(Words("Sampleton", 0.05, 0.23).Concat(Words("Testy", 0.15, 0.23)).Concat(Words("04/09/1971", 0.25, 0.23)).Concat(Words("55", 0.32, 0.23))
                .Concat(Words("000-12-3401", 0.40, 0.23)).ToList()),
            new(Words("Sampleton", 0.05, 0.26).Concat(Words("Kid", 0.15, 0.26)).Concat(Words("08/15/2016", 0.25, 0.26)).Concat(Words("10", 0.32, 0.26))
                .Concat(Words("000-12-3404", 0.40, 0.26)).ToList()),
        ]);
        var i = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        ScanFieldReader.Fill(i, [page]);
        Assert.Equal(2, i.MemberCount);
        Assert.Equal("Kid", V(i, "household[1].firstName"));
        Assert.Equal("08/15/2016", V(i, "household[1].dateOfBirth"));
        Assert.Equal("000-12-3404", V(i, "household[1].ssn"));
    }
}
