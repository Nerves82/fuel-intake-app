namespace FuelIntake.Core.Hosting;

/// <summary>
/// Prints a self-contained HTML document to PDF, honouring CSS page sizes (portrait and landscape pages
/// in one file). Phase 1: the desktop app's WebView2. Phase 2: a headless browser on the server.
/// </summary>
public interface IPdfPrinter
{
    Task<byte[]> PrintAsync(string html, CancellationToken ct = default);
}
