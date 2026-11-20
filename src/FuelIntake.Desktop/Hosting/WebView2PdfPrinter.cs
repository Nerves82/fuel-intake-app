using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using FuelIntake.Core.Hosting;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace FuelIntake.Desktop.Hosting;

/// <summary>
/// Prints HTML to PDF with WebView2 (the same Chromium engine the app already uses), via the
/// DevTools "Page.printToPDF" command so CSS page sizes (portrait and landscape) are honoured.
/// Uses a hidden off-screen window; nothing is shown to the user and nothing is written to disk.
/// </summary>
public sealed class WebView2PdfPrinter(Dispatcher dispatcher, Func<CoreWebView2Environment?> sharedEnvironment) : IPdfPrinter
{
    private const string PrintOptions =
        """{"printBackground":true,"preferCSSPageSize":true,"marginTop":0,"marginBottom":0,"marginLeft":0,"marginRight":0}""";

    public async Task<byte[]> PrintAsync(string html, CancellationToken ct = default)
    {
        try
        {
            return await dispatcher.InvokeAsync(() => PrintOnUiThreadAsync(html)).Task.Unwrap();
        }
        catch (Exception ex)
        {
            StartupLog.Write("PDF printing failed", ex);
            throw;
        }
    }

    private async Task<byte[]> PrintOnUiThreadAsync(string html)
    {
        var web = new WebView2();
        var window = new Window
        {
            Width = 900, Height = 1100, Left = -32000, Top = -32000,
            ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None, Content = web,
        };
        window.Show();
        try
        {
            // Must use the same environment as the main window: WebView2 refuses a second environment
            // with different options on the same user-data folder ("not in the correct state").
            await web.EnsureCoreWebView2Async(sharedEnvironment()
                ?? throw new InvalidOperationException("The app's browser isn't ready yet. Try again in a moment."));
            var loaded = new TaskCompletionSource<bool>();
            web.CoreWebView2.NavigationCompleted += (_, e) => loaded.TrySetResult(e.IsSuccess);
            web.CoreWebView2.NavigateToString(html);
            if (!await loaded.Task) throw new InvalidOperationException("The forms couldn't be laid out for printing.");

            var result = await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.printToPDF", PrintOptions);
            using var doc = JsonDocument.Parse(result);
            return Convert.FromBase64String(doc.RootElement.GetProperty("data").GetString()!);
        }
        finally
        {
            window.Close();
            web.Dispose();
        }
    }
}
