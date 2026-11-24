using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using FuelIntake.Core.Import;
using FuelIntake.Scanning;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace FuelIntake.Desktop.Hosting;

/// <summary>
/// Reads scanned pages with PaddleOCR (<see cref="PaddleText"/>): Windows turns PDF pages and pictures into grey
/// levels, PaddleOCR reads them. Pages the app printed are read box by box. If the models can't be loaded, Windows'
/// built-in text recognition is used instead, as before.
/// </summary>
public sealed class PaddleScanReader(IScanReader fallback) : IScanReader, IDisposable
{
    /// <summary>Scans are worked on at about this resolution: enough for handwriting, small enough to be quick.</summary>
    private const double Dpi = 200;

    private readonly Lazy<PaddleText?> paddle = new(Load);

    public string? Unavailable => paddle.Value is null ? fallback.Unavailable : null;

    private static PaddleText? Load()
    {
        try { return new PaddleText(); }
        catch (Exception ex)
        {
            StartupLog.Write("PaddleOCR couldn't load, using Windows text recognition instead.", ex);
            return null;
        }
    }

    public async Task<IReadOnlyList<OcrPage>> ReadAsync(IReadOnlyList<(string File, string Location)> scans, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (paddle.Value is not { } p) return await fallback.ReadAsync(scans, progress, ct);
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
                    pages.Add(p.Read(await GrayAsync(stream, null), file, (int)n + 1));
                }
            }
            else
            {
                progress?.Report($"Reading {file}…");
                using var stream = await storage.OpenReadAsync();
                pages.Add(p.Read(await GrayAsync(stream, 3400), file, 1));
            }
        }
        return pages;
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
        if (paddle.IsValueCreated) paddle.Value?.Dispose();
    }
}
