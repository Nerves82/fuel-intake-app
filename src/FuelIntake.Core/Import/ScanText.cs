namespace FuelIntake.Core.Import;

/// <summary>A word the scan reader found; position and size are fractions of the page (0–1).</summary>
public sealed record OcrWord(string Text, double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

/// <summary>A line of words, left to right.</summary>
public sealed record OcrLine(IReadOnlyList<OcrWord> Words)
{
    public string Text => string.Join(' ', Words.Select(w => w.Text));
    public double X => Words.Min(w => w.X);
    public double Y => Words.Min(w => w.Y);
    public double Right => Words.Max(w => w.Right);
    public double Bottom => Words.Max(w => w.Bottom);
    public double Height => Bottom - Y;
}

/// <summary>The text found on one page of a scan.</summary>
public sealed record OcrPage(string File, int Page, IReadOnlyList<OcrLine> Lines);

/// <summary>
/// Reads the text on scanned pages, on this computer only (nothing is sent anywhere).
/// Desktop: Windows' built-in text recognition (Windows.Media.Ocr) and PDF rendering (Windows.Data.Pdf).
/// </summary>
public interface IScanReader
{
    /// <summary>Why reading isn't possible here (e.g. no OCR language installed), or null.</summary>
    string? Unavailable { get; }

    /// <summary>Reads every page of each scan. <paramref name="progress"/> gets "page 2 of 5"-style updates.</summary>
    Task<IReadOnlyList<OcrPage>> ReadAsync(IReadOnlyList<(string File, string Location)> scans, IProgress<string>? progress = null, CancellationToken ct = default);
}
