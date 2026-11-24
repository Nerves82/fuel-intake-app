using FuelIntake.Core.Import;
using FuelIntake.Scanning;
using SkiaSharp;

namespace FuelIntake.Scanning.Tests;

/// <summary>PaddleOCR through RapidOcrNet, on pages drawn here (printed-style lettering, made-up data).</summary>
public sealed class PaddleTextTests : IClassFixture<PaddleTextTests.Engine>
{
    public sealed class Engine : IDisposable
    {
        public PaddleText Paddle { get; } = new();
        public void Dispose() => Paddle.Dispose();
    }

    private const float Dpi = 200;
    private readonly PaddleText paddle;

    public PaddleTextTests(Engine e) => paddle = e.Paddle;

    private static SKTypeface Face => SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;
    private static readonly SKPaint Ink = new() { Color = SKColors.Black, IsAntialias = true };

    private static GrayImage Draw(float wIn, float hIn, float degrees, Action<SKCanvas> draw)
    {
        using var bitmap = new SKBitmap((int)(wIn * Dpi), (int)(hIn * Dpi), SKColorType.Rgba8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(240, 240, 240));
            canvas.RotateDegrees(degrees, bitmap.Width / 2f, bitmap.Height / 2f);
            draw(canvas);
        }
        return PaddleText.ToGray(bitmap);
    }

    /// <summary>Lettering at a page position in inches.</summary>
    private static void Text(SKCanvas c, string text, float x, float baseline, float sizeIn = 0.16f)
    {
        using var font = new SKFont(Face, sizeIn * Dpi);
        c.DrawText(text, x * Dpi, baseline * Dpi, font, Ink);
    }

    /// <summary>A rectangle in inches, filled or outlined.</summary>
    private static void Box(SKCanvas c, double x, double y, double w, double h, SKPaint paint) =>
        c.DrawRect((float)(x * Dpi), (float)(y * Dpi), (float)(w * Dpi), (float)(h * Dpi), paint);

    [Fact]
    public void ReadsALine()
    {
        var image = Draw(3, 0.4f, 0, c => Text(c, "802-555-0142", 0.1f, 0.28f, 0.2f));
        var crop = image.TrimToInk(6)!;
        Assert.Equal("8025550142", new string(paddle.ReadLine(crop).Where(char.IsDigit).ToArray()));
    }

    [Fact]
    public void ReadsTheAppsFormBoxByBox()
    {
        var form = ScanForms.Find("IntakePage1")!;
        ScanZone Z(string p) => form.Zones.First(z => z.Path == p);
        var answers = new Dictionary<string, string>
        {
            ["client.phone"] = "802-555-0142", ["client.lastName"] = "Sampleton", ["client.firstName"] = "Testy",
            ["household[1].firstName"] = "Kiddo", ["household[1].ssn"] = "000-12-3402",
        };
        var image = Draw(11, 8.5f, 0.8f, c =>
        {
            foreach (var (x, y) in ScanMarks.Corners(11, 8.5))
                Box(c, x - ScanMarks.Size / 2, y - ScanMarks.Size / 2, ScanMarks.Size, ScanMarks.Size, Ink);
            var bits = ScanMarks.Encode(form.Code, form.Version);
            foreach (var (cell, on) in ScanMarks.CodeCellsAt(8.5).Zip(bits))
                if (on) Box(c, cell.X, cell.Y, ScanMarks.CellSize, ScanMarks.CellSize, Ink);
            using var thin = new SKPaint { Color = SKColors.Black, StrokeWidth = 2, IsStroke = true };
            foreach (var (path, text) in answers)
            {
                var z = Z(path);
                if (z.Kind == ZoneKind.Cell)
                {
                    Box(c, z.X, z.Y, z.W, z.H, thin);   // the printed cell
                    Text(c, text, (float)z.X + 0.05f, (float)(z.Y + z.H) - 0.07f, 0.13f);
                }
                else
                {
                    c.DrawLine((float)(z.X * Dpi), (float)((z.Y + z.H) * Dpi), (float)((z.X + z.W) * Dpi), (float)((z.Y + z.H) * Dpi), thin);   // the blank's line
                    Text(c, text, (float)z.X + 0.1f, (float)(z.Y + z.H) - 0.03f);
                }
            }
        });
        var page = paddle.Read(image, "scan.png", 1);
        Assert.NotNull(page.Form);
        var import = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        ScanFieldReader.Fill(import, [page]);
        string? V(string p) => import.Fields.Single(f => f.Path == p).Value;
        Assert.Equal(2, import.MemberCount);
        Assert.Equal("(802) 555-0142", V("client.phone"));
        Assert.Equal("Sampleton", V("client.lastName"));
        Assert.Equal("Testy", V("client.firstName"));
        Assert.Equal("Kiddo", V("household[1].firstName"));
        Assert.Equal("000-12-3402", V("household[1].ssn"));
    }

    [Fact]
    public void ReadsAPageThatIsntAnAppForm()
    {
        var image = Draw(8.5f, 11, -0.5f, c =>
        {
            Text(c, "Last Name: Sampleton", 1, 1.5f);
            Text(c, "First Name: Testy", 1, 2f);
            Text(c, "Phone: 802-555-0142", 1, 2.5f);
        });
        var page = paddle.Read(image, "scan.png", 1);
        Assert.Null(page.Form);
        Assert.NotEmpty(page.Lines);
        var import = new PaperImport { Fields = ImportFieldCatalog.NewImport(1) };
        ScanFieldReader.Fill(import, [page]);
        Assert.Equal("Sampleton", import.Fields.Single(f => f.Path == "client.lastName").Value);
        Assert.Equal("(802) 555-0142", import.Fields.Single(f => f.Path == "client.phone").Value);
    }
}
