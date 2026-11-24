using FuelIntake.Core.Import;

namespace FuelIntake.Core.Tests;

/// <summary>The app's own forms on a scan: corner squares, form code, and reading box by box.</summary>
public class ScanFormTests
{
    /// <summary>
    /// A fake scan of a page: black rectangles (page inches) drawn at <paramref name="dpi"/>, turned by
    /// <paramref name="degrees"/>, shrunk by <paramref name="scale"/> (printed "fit to page"), and offset in the scanner.
    /// <paramref name="quarterTurns"/> turns the whole page (fed sideways or upside down).
    /// </summary>
    private static GrayImage Scan(ScanFormDef form, IEnumerable<(double X, double Y, double W, double H)> ink,
        double dpi = 150, double degrees = 1.2, double scale = 0.97, int quarterTurns = 0)
    {
        var rects = Marks(form).Concat(ink).ToList();
        double pw = form.PageWidth, ph = form.PageHeight;
        var turnedW = quarterTurns % 2 == 0 ? pw : ph; var turnedH = quarterTurns % 2 == 0 ? ph : pw;
        int w = (int)(turnedW * dpi), h = (int)(turnedH * dpi);
        var px = new byte[w * h];
        var a = degrees * Math.PI / 180; var cos = Math.Cos(a); var sin = Math.Sin(a);
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            // image → turned page inches → undo skew/scale about the centre → page inches
            double u = x / dpi - turnedW / 2, v = y / dpi - turnedH / 2;
            (u, v) = ((u * cos + v * sin) / scale, (-u * sin + v * cos) / scale);
            for (var t = 0; t < quarterTurns; t++) (u, v) = (v, -u);
            u += pw / 2; v += ph / 2;
            var dark = rects.Any(r => u >= r.X && u <= r.X + r.W && v >= r.Y && v <= r.Y + r.H);
            px[y * w + x] = dark ? (byte)20 : (byte)235;
        }
        return new GrayImage(w, h, px);
    }

    private static IEnumerable<(double X, double Y, double W, double H)> Marks(ScanFormDef f)
    {
        foreach (var (x, y) in ScanMarks.Corners(f.PageWidth, f.PageHeight))
            yield return (x - ScanMarks.Size / 2, y - ScanMarks.Size / 2, ScanMarks.Size, ScanMarks.Size);
        var bits = ScanMarks.Encode(f.Code, f.Version);
        var cells = ScanMarks.CodeCellsAt(f.PageHeight).ToList();
        for (var i = 0; i < bits.Length; i++)
            if (bits[i]) yield return (cells[i].X, cells[i].Y, ScanMarks.CellSize, ScanMarks.CellSize);
    }

    private static ScanZone Zone(ScanFormDef f, string path, string? value = null) =>
        f.Zones.First(z => z.Path == path && (value is null || z.Value == value));

    /// <summary>
    /// Pen strokes in a box, like letters (thin uprights): on the writing line of a blank, inside a cell; an X on a
    /// tick blank.
    /// </summary>
    private static IEnumerable<(double X, double Y, double W, double H)> Writing(ScanZone z)
    {
        if (z.Kind == ZoneKind.Tick) { yield return (z.X + z.W * 0.3, z.Y + z.H * 0.2, z.W * 0.4, z.H * 0.7); yield break; }
        var (x, y, w, h) = z.Kind == ZoneKind.Cell
            ? (z.X + 0.08, z.Y + z.H * 0.25, Math.Min(0.6, z.W - 0.16), z.H * 0.5)
            : (z.X + 0.1, z.Y + z.H - 0.2, Math.Min(1.0, z.W - 0.2), 0.15);
        for (var u = x; u < x + w; u += 0.05) yield return (u, y, 0.02, h);
    }

    private static ScanFormDef Form(string id) => ScanForms.Find(id) ?? throw new InvalidOperationException($"{id} isn't in scan-forms.json");

    [Fact]
    public void Code_RoundTrips_AndRejectsMisreads()
    {
        for (var form = 1; form <= 15; form++)
        for (var v = 0; v <= 3; v++)
            Assert.Equal((form, v), ScanMarks.Decode(ScanMarks.Encode(form, v)));
        var bits = ScanMarks.Encode(3, 1);
        Assert.Null(ScanMarks.Decode(bits.Reverse().ToArray().Select((b, i) => i is 0 or 9 || !b).ToArray()));
        bits[4] = !bits[4];
        Assert.Null(ScanMarks.Decode(bits));
    }

    [Fact]
    public void TheFormsAreInTheLayout()
    {
        Assert.Equal("IntakePage1,IntakePage2,IncomeExpense", string.Join(",", ScanForms.All.Select(f => f.Id)));
        Assert.Equal(10 * 13 + 6, Form("IntakePage1").Zones.Count);
        Assert.All(ScanForms.All, f => Assert.Equal(ScanForms.Codes[f.Id], f.Code));
        Assert.Contains(Form("IntakePage2").Zones, z => z.Kind == ZoneKind.Tick && z.Path == "client.housingType" && z.Value == "Rent");
        Assert.Contains(Form("IncomeExpense").Zones, z => z.Kind == ZoneKind.Cell && z.Path == "incomeExpenses.income.SSA");
    }

    [Theory]
    [InlineData("IntakePage1", 0)]
    [InlineData("IntakePage1", 2)]   // upside down
    [InlineData("IntakePage1", 1)]   // landscape page scanned sideways
    [InlineData("IncomeExpense", 0)]
    [InlineData("IncomeExpense", 3)]
    public void FindsTheFormHoweverItWasFed(string id, int quarterTurns)
    {
        var form = Form(id);
        var located = ScanFormReader.Locate(Scan(form, [], quarterTurns: quarterTurns));
        Assert.NotNull(located);
        Assert.Equal(id, located!.Form.Id);
        Assert.Equal(quarterTurns == 2, located.UpsideDown);
    }

    [Fact]
    public void APageWithoutMarks_IsNotAForm() =>
        Assert.Null(ScanFormReader.Locate(new GrayImage(1100, 850, Enumerable.Repeat((byte)235, 1100 * 850).ToArray())));

    [Fact]
    public void ReadsWritingAndRowsBoxByBox()
    {
        var form = Form("IntakePage1");
        var written = new[] { "client.phone", "client.lastName", "household[2].firstName", "household[2].ssn", ScanFieldReader.CityStateZipPath };
        var image = Scan(form, written.SelectMany(p => Writing(Zone(form, p))), degrees: -0.8, scale: 0.96);
        var located = ScanFormReader.Locate(image)!;
        // the fake text reader answers in the order boxes are read (only boxes with ink are read)
        var answers = new Queue<string>(new[] { "St. Johnsbury, VT 05819", "(802) 555-0142", "Sampleton", "Kiddo", "000-12-3404" });
        var order = form.Zones.Where(z => written.Contains(z.Path)).Select(z => z.Path).ToList();
        Assert.Equal(string.Join(",", ScanFieldReader.CityStateZipPath, "client.phone", "client.lastName", "household[2].firstName", "household[2].ssn"),
            string.Join(",", order));
        var reading = ScanFormReader.Read(image, located, _ => answers.Dequeue(), "scan.pdf", 1);
        Assert.Empty(answers);
        Assert.True(reading.Values.Single(v => v.Zone.Path == "household[5].ssn").Text == "");

        var import = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        ScanFieldReader.Fill(import, [new OcrPage("scan.pdf", 1, []) { Form = reading }]);
        string? V(string p) => import.Fields.Single(f => f.Path == p).Value;
        Assert.Equal(3, import.MemberCount);
        Assert.Equal("(802) 555-0142", V("client.phone"));
        Assert.Equal("Sampleton", V("client.lastName"));
        Assert.Equal("St. Johnsbury", V("client.city"));
        Assert.Equal("VT", V("client.state"));
        Assert.Equal("05819", V("client.zip"));
        Assert.Equal("Kiddo", V("household[2].firstName"));
        Assert.Equal("000-12-3404", V("household[2].ssn"));
        Assert.Null(V("household[1].firstName"));
        var spot = import.Fields.Single(f => f.Path == "household[2].ssn").Spot!;
        Assert.InRange(spot.X, 0.7, 1.0);
    }

    [Fact]
    public void ReadsTicks()
    {
        var form = Form("IntakePage2");
        var image = Scan(form, [.. Writing(Zone(form, "client.housingType", "Rent")), .. Writing(Zone(form, "client.benefits.wic")),
            .. Writing(Zone(form, "client.householdType", "Single Parent Female"))]);
        var located = ScanFormReader.Locate(image)!;
        var reading = ScanFormReader.Read(image, located, _ => "", "scan.pdf", 2);
        var import = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        ScanFieldReader.Fill(import, [new OcrPage("scan.pdf", 2, []) { Form = reading }]);
        string? V(string p) => import.Fields.Single(f => f.Path == p).Value;
        Assert.Equal("Rent", V("client.housingType"));
        Assert.Equal("Single Parent Female", V("client.householdType"));
        Assert.Equal(PaperImportRules.Checked, V("client.benefits.wic"));
        Assert.Null(V("client.benefits.liheap"));
        Assert.Equal(0.8, import.Fields.Single(f => f.Path == "client.housingType").Confidence);
    }

    [Fact]
    public void StaffWorkIsKept_WhenReadAgain()
    {
        var form = Form("IntakePage2");
        var image = Scan(form, [.. Writing(Zone(form, "client.housingType", "Own"))]);
        var reading = ScanFormReader.Read(image, ScanFormReader.Locate(image)!, _ => "", "scan.pdf", 1);
        var import = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        PaperImportRules.SetValue(import.Fields.Single(f => f.Path == "client.housingType"), "Rent");
        ScanFieldReader.Fill(import, [new OcrPage("scan.pdf", 1, []) { Form = reading }]);
        Assert.Equal("Rent", import.Fields.Single(f => f.Path == "client.housingType").Value);
    }

    [Fact]
    public void NumberBoxes_FixLookAlikeLetters_ButFlagThem()
    {
        var import = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        var form = Form("IntakePage1");
        ScanZone Z(string p) => form.Zones.First(z => z.Path == p);
        var spot = new ScanSpot("scan.pdf", 1, 0, 0, 0.1, 0.1);
        var reading = new FormReading("IntakePage1", 1, false,
        [
            new(Z("household[0].ssn"), "000.-12-34o1", 0.1, spot), new(Z("client.dateOfBirth"), "2/.3/197o", 0.1, spot),
            new(Z("household[0].incomePerMonth"), "1200", 0.1, spot), new(Z(ScanFieldReader.CityStateZipPath), "Barton VT o5822", 0.1, spot),
        ]);
        ScanFieldReader.Fill(import, [new OcrPage("scan.pdf", 1, []) { Form = reading }]);
        ImportField F(string p) => import.Fields.Single(f => f.Path == p);
        Assert.Equal("000-12-3401", F("household[0].ssn").Value);
        Assert.Equal(0.35, F("household[0].ssn").Confidence);
        Assert.Equal("02/03/1970", F("client.dateOfBirth").Value);
        Assert.Equal("1200.00", F("household[0].incomePerMonth").Value);  // not "120"
        Assert.Equal("05822", F("client.zip").Value);
        Assert.Equal("Barton", F("client.city").Value);
    }

    [Theory]
    [InlineData("St. Johnsbury, VT 05819", "St. Johnsbury", "VT", "05819")]
    [InlineData("Lyndonville Vermont 05851", "Lyndonville", "VT", "05851")]
    [InlineData("Newport VT", "Newport", "VT", "")]
    [InlineData("Barton 05822", "Barton", "", "05822")]
    public void SplitsTownStateZip(string raw, string city, string state, string zip) =>
        Assert.Equal((city, state, zip), ScanFieldReader.SplitCityStateZip(raw));
}
