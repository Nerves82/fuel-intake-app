using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using FuelIntake.Core.Import;
using RapidOcrNet;
using SkiaSharp;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace FuelIntake.Desktop.Hosting;

/// <summary>
/// Reads scanned pages on this computer with PaddleOCR (PP-OCRv5, Latin script) through RapidOcrNet and ONNX Runtime.
/// The models ship in the app folder (models\v5); nothing is installed and nothing leaves the laptop.
/// <para>
/// Pages printed by the app (corner squares and form code) are read box by box: ticks by ink, each answer cut out,
/// straightened and read on its own. Any other page is read whole and matched by its printed labels.
/// If the models can't be loaded, Windows' built-in text recognition is used instead.
/// </para>
/// </summary>
public sealed class PaddleScanReader(IScanReader fallback) : IScanReader, IDisposable
{
    /// <summary>Scans are worked on at about this resolution: enough for handwriting, small enough to be quick.</summary>
    private const double Dpi = 200;

    private readonly Lazy<Engines?> engines = new(Load);
    private static string? loadProblem;

    private sealed record Engines(RapidOcr Page, TextRecognizer Line) : IDisposable
    {
        public void Dispose() { Page.Dispose(); Line.Dispose(); }
    }

    public string? Unavailable => engines.Value is null ? fallback.Unavailable : null;

    private static Engines? Load()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, RapidOcr.ModelsFolderName, RapidOcr.ModelsVersion);
            string det = Path.Combine(dir, RapidOcr.DefaultDetModelPath), cls = Path.Combine(dir, RapidOcr.DefaultClsModelPath);
            string rec = Path.Combine(dir, RapidOcr.DefaultRecModelPath), keys = Path.Combine(dir, RapidOcr.DefaultKeysFilePath);
            var page = new RapidOcr();
            page.InitModels(det, cls, rec, keys);
            var line = new TextRecognizer();
            line.InitModel(rec, keys, 0);
            return new Engines(page, line);
        }
        catch (Exception ex)
        {
            loadProblem = ex.Message;
            StartupLog.Write("PaddleOCR couldn't load, using Windows text recognition: " + ex.Message);
            return null;
        }
    }

    public async Task<IReadOnlyList<OcrPage>> ReadAsync(IReadOnlyList<(string File, string Location)> scans, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (engines.Value is not { } e) return await fallback.ReadAsync(scans, progress, ct);
        var pages = new List<OcrPage>();
        foreach (var (file, location) in scans)
        {
            ct.ThrowIfCancellationRequested();
            var storage = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(location));
            if (string.Equals(Path.GetExtension(location), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var pdf = await PdfDocument.LoadFromFileAsync(storage);
                for (uint n = 0; n < pdf.PageCount; n++)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report($"Reading {file}, page {n + 1} of {pdf.PageCount}…");
                    using var page = pdf.GetPage(n);
                    using var stream = new InMemoryRandomAccessStream();
                    // page.Size is in 1/96 inch
                    await page.RenderToStreamAsync(stream, new PdfPageRenderOptions
                    {
                        DestinationWidth = (uint)Math.Round(page.Size.Width / 96 * Dpi),
                        DestinationHeight = (uint)Math.Round(page.Size.Height / 96 * Dpi),
                    });
                    pages.Add(Read(e, await GrayAsync(stream, null), file, (int)n + 1));
                }
            }
            else
            {
                progress?.Report($"Reading {file}…");
                using var stream = await storage.OpenReadAsync();
                pages.Add(Read(e, await GrayAsync(stream, 3400), file, 1));
            }
        }
        return pages;
    }

    private static OcrPage Read(Engines e, GrayImage image, string file, int number)
    {
        if (ScanFormReader.Locate(image) is { } located)
            return new OcrPage(file, number, []) { Form = ScanFormReader.Read(image, located, crop => ReadLine(e, crop), file, number) };
        return ReadWholePage(e, image, file, number);
    }

    /// <summary>One answer box, already cut down to its line of writing: recognition only, no text-finding step.</summary>
    private static string ReadLine(Engines e, GrayImage crop)
    {
        using var bitmap = ToBitmap(crop);
        var line = e.Line.GetTextLine(bitmap);
        return line.Chars is null ? "" : string.Concat(line.Chars).Trim();
    }

    /// <summary>A page that isn't one of the app's forms: every line of text, with where it is, for the label reader.</summary>
    private static OcrPage ReadWholePage(Engines e, GrayImage image, string file, int number)
    {
        using var bitmap = ToBitmap(image);
        var options = RapidOcrOptions.Default with { ImgResize = 0, MaxSideLen = 2400, TextScore = 0.3f, ReturnWordBox = true };
        var result = e.Page.Detect(bitmap, options);
        double w = image.Width, h = image.Height;
        var lines = new List<OcrLine>();
        foreach (var block in result.TextBlocks)
        {
            var words = block.WordResults is { Length: > 0 } wr
                ? wr.Select(x => Word(x.Text, x.BoxPoints, w, h)).ToList()
                : SplitWords(block.Text, block.BoxPoints, w, h);
            words = words.Where(x => x.Text.Length > 0).ToList();
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
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var total = Math.Max(1, text.Length);
        var list = new List<OcrWord>();
        var at = 0;
        foreach (var part in parts)
        {
            var start = text.IndexOf(part, at, StringComparison.Ordinal);
            at = start + part.Length;
            var px = x0 + (x1 - x0) * start / total;
            var pw = (x1 - x0) * part.Length / total;
            list.Add(new OcrWord(part, px / w, y0 / h, pw / w, (y1 - y0) / h));
        }
        return list;
    }

    private static SKBitmap ToBitmap(GrayImage g)
    {
        var bitmap = new SKBitmap(new SKImageInfo(g.Width, g.Height, SKColorType.Gray8, SKAlphaType.Opaque));
        var ptr = bitmap.GetPixels();
        for (var y = 0; y < g.Height; y++)
            Marshal.Copy(g.Pixels, y * g.Width, ptr + y * bitmap.RowBytes, g.Width);
        return bitmap;
    }

    /// <summary>Decodes a picture (turned the way its camera/scanner says) to grey levels, no bigger than <paramref name="maxSide"/>.</summary>
    private static async Task<GrayImage> GrayAsync(IRandomAccessStream stream, int? maxSide)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var scale = maxSide is int m ? Math.Min(1.0, (double)m / Math.Max(decoder.OrientedPixelWidth, decoder.OrientedPixelHeight)) : 1.0;
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Max(1, Math.Floor(decoder.PixelWidth * scale)),
            ScaledHeight = (uint)Math.Max(1, Math.Floor(decoder.PixelHeight * scale)),
            InterpolationMode = BitmapInterpolationMode.Fant,
        };
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
            transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
        int w = bitmap.PixelWidth, h = bitmap.PixelHeight;
        var buffer = new Windows.Storage.Streams.Buffer((uint)(w * h * 4));
        bitmap.CopyToBuffer(buffer);
        var bgra = buffer.ToArray();
        var gray = new byte[w * h];
        for (var i = 0; i < gray.Length; i++)
            gray[i] = (byte)((bgra[i * 4] * 29 + bgra[i * 4 + 1] * 150 + bgra[i * 4 + 2] * 77) >> 8);
        return new GrayImage(w, h, gray);
    }

    public void Dispose()
    {
        if (engines.IsValueCreated) engines.Value?.Dispose();
    }

    /// <summary>Why PaddleOCR didn't load (for the log), if it didn't.</summary>
    public static string? LoadProblem => loadProblem;
}
