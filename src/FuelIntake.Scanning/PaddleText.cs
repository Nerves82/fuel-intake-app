using System.Runtime.InteropServices;
using FuelIntake.Core.Import;
using RapidOcrNet;
using SkiaSharp;

namespace FuelIntake.Scanning;

/// <summary>
/// PaddleOCR (PP-OCRv5, Latin script) on this computer, through RapidOcrNet and ONNX Runtime. The models are files in
/// the app folder (models\v5, copied there by the RapidOcrNet package); nothing is installed and nothing leaves the laptop.
/// <para>
/// A page the app printed (corner squares and form code) is read box by box: ticks by ink, each answer cut out,
/// straightened and read on its own. Any other page is read whole, for the label and table readers.
/// </para>
/// </summary>
public sealed class PaddleText : IDisposable
{
    private readonly RapidOcr page = new();
    private readonly TextRecognizer line = new();

    /// <param name="modelsFolder">Where the .onnx files are; the app folder's models\v5 when null.</param>
    public PaddleText(string? modelsFolder = null)
    {
        var dir = modelsFolder ?? Path.Combine(AppContext.BaseDirectory, RapidOcr.ModelsFolderName, RapidOcr.ModelsVersion);
        string det = Path.Combine(dir, RapidOcr.DefaultDetModelPath), cls = Path.Combine(dir, RapidOcr.DefaultClsModelPath);
        string rec = Path.Combine(dir, RapidOcr.DefaultRecModelPath), keys = Path.Combine(dir, RapidOcr.DefaultKeysFilePath);
        page.InitModels(det, cls, rec, keys);
        line.InitModel(rec, keys, 0);
    }

    /// <summary>The app's own form box by box when the page is one, else the whole page's text.</summary>
    public OcrPage Read(GrayImage image, string file, int number) =>
        ScanFormReader.Locate(image) is { } located
            ? new OcrPage(file, number, []) { Form = ScanFormReader.Read(image, located, ReadLine, file, number) }
            : ReadPage(image, file, number);

    /// <summary>One line of writing (an answer box already cut down to it): recognition only, no text-finding step.</summary>
    public string ReadLine(GrayImage crop)
    {
        using var bitmap = ToBitmap(crop);
        var result = line.GetTextLine(bitmap);
        return result.Chars is null ? "" : string.Concat(result.Chars).Trim();
    }

    /// <summary>Every line of text on a page, with where it is (fractions of the page), for the label reader.</summary>
    public OcrPage ReadPage(GrayImage image, string file, int number)
    {
        using var bitmap = ToBitmap(image);
        var options = RapidOcrOptions.Default with { ImgResize = 0, MaxSideLen = 2400, TextScore = 0.3f, ReturnWordBox = true };
        var result = page.Detect(bitmap, options);
        double w = image.Width, h = image.Height;
        var lines = new List<OcrLine>();
        foreach (var block in result.TextBlocks)
        {
            var words = (block.WordResults is { Length: > 0 } wr
                    ? wr.Select(x => Word(x.Text, x.BoxPoints, w, h)).ToList()
                    : SplitWords(block.Text, block.BoxPoints, w, h))
                .Where(x => x.Text.Length > 0).ToList();
            if (words.Count > 0) lines.Add(new OcrLine(words));
        }
        return new OcrPage(file, number, lines);
    }

    private static OcrWord Word(string text, SKPointI[] box, double w, double h)
    {
        double x0 = box.Min(p => p.X), y0 = box.Min(p => p.Y), x1 = box.Max(p => p.X), y1 = box.Max(p => p.Y);
        return new OcrWord(text.Trim(), x0 / w, y0 / h, (x1 - x0) / w, (y1 - y0) / h);
    }

    /// <summary>A line without word boxes: its width shared out by letter count.</summary>
    private static List<OcrWord> SplitWords(string text, SKPointI[] box, double w, double h)
    {
        double x0 = box.Min(p => p.X), y0 = box.Min(p => p.Y), x1 = box.Max(p => p.X), y1 = box.Max(p => p.Y);
        var total = Math.Max(1, text.Length);
        var list = new List<OcrWord>();
        var at = 0;
        foreach (var part in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var start = text.IndexOf(part, at, StringComparison.Ordinal);
            at = start + part.Length;
            list.Add(new OcrWord(part, (x0 + (x1 - x0) * start / total) / w, y0 / h, (x1 - x0) * part.Length / total / w, (y1 - y0) / h));
        }
        return list;
    }

    /// <summary>Grey levels as an 8-bit grey Skia bitmap (what the models are given).</summary>
    public static SKBitmap ToBitmap(GrayImage g)
    {
        var bitmap = new SKBitmap(new SKImageInfo(g.Width, g.Height, SKColorType.Gray8, SKAlphaType.Opaque));
        var ptr = bitmap.GetPixels();
        for (var y = 0; y < g.Height; y++)
            Marshal.Copy(g.Pixels, y * g.Width, ptr + y * bitmap.RowBytes, g.Width);
        return bitmap;
    }

    /// <summary>Any Skia bitmap as grey levels.</summary>
    public static GrayImage ToGray(SKBitmap b)
    {
        var px = new byte[b.Width * b.Height];
        for (var y = 0; y < b.Height; y++)
        for (var x = 0; x < b.Width; x++)
        {
            var c = b.GetPixel(x, y);
            px[y * b.Width + x] = (byte)((c.Red * 77 + c.Green * 150 + c.Blue * 29) >> 8);
        }
        return new GrayImage(b.Width, b.Height, px);
    }

    public void Dispose()
    {
        page.Dispose();
        line.Dispose();
    }
}
