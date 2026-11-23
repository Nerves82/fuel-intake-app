using System.IO;
using FuelIntake.Core.Import;
using Windows.Data.Pdf;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using OcrEngine = Windows.Media.Ocr.OcrEngine;
using Windows.Storage;
using Windows.Storage.Streams;

namespace FuelIntake.Desktop.Hosting;

/// <summary>
/// Reads scanned pages with what's built into Windows 10/11, on this computer only:
/// Windows.Data.Pdf turns PDF pages into pictures and Windows.Media.Ocr reads the text.
/// Nothing to install and nothing leaves the laptop. Good with printed text and neat block
/// capitals; handwriting is hit and miss, which is why staff check every field.
/// </summary>
public sealed class WindowsScanReader : IScanReader
{
    private readonly OcrEngine? engine =
        OcrEngine.TryCreateFromUserProfileLanguages() ?? TryLanguage("en-US");

    public string? Unavailable => engine is null
        ? "Windows text recognition isn't set up for English on this computer (Settings → Time & language → Language → English → Optional features)."
        : null;

    public async Task<IReadOnlyList<OcrPage>> ReadAsync(IReadOnlyList<(string File, string Location)> scans, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (engine is null) throw new InvalidOperationException(Unavailable);
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
                    // as large as the reader takes (better for handwriting), keeping the page's shape
                    var scale = OcrEngine.MaxImageDimension / Math.Max(page.Size.Width, page.Size.Height);
                    await page.RenderToStreamAsync(stream, new PdfPageRenderOptions
                    {
                        DestinationWidth = (uint)Math.Floor(page.Size.Width * scale),
                        DestinationHeight = (uint)Math.Floor(page.Size.Height * scale),
                    });
                    pages.Add(await ReadImageAsync(stream, file, (int)n + 1));
                }
            }
            else
            {
                progress?.Report($"Reading {file}…");
                using var stream = await storage.OpenReadAsync();
                pages.Add(await ReadImageAsync(stream, file, 1));
            }
        }
        return pages;
    }

    private async Task<OcrPage> ReadImageAsync(IRandomAccessStream stream, string file, int pageNumber)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var max = OcrEngine.MaxImageDimension;
        var scale = Math.Min(1.0, (double)max / Math.Max(decoder.OrientedPixelWidth, decoder.OrientedPixelHeight));
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Floor(decoder.PixelWidth * scale),
            ScaledHeight = (uint)Math.Floor(decoder.PixelHeight * scale),
            InterpolationMode = BitmapInterpolationMode.Fant,
        };
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
        var result = await engine!.RecognizeAsync(bitmap);
        double w = bitmap.PixelWidth, h = bitmap.PixelHeight;
        var lines = result.Lines
            .Select(l => new OcrLine(l.Words.Select(word => new OcrWord(word.Text,
                word.BoundingRect.X / w, word.BoundingRect.Y / h, word.BoundingRect.Width / w, word.BoundingRect.Height / h)).ToList()))
            .Where(l => l.Words.Count > 0)
            .ToList();
        return new OcrPage(file, pageNumber, lines);
    }

    private static OcrEngine? TryLanguage(string tag)
    {
        try { return OcrEngine.TryCreateFromLanguage(new Language(tag)); }
        catch { return null; }
    }
}
