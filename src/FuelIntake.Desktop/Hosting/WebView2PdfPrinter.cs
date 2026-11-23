using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using FuelIntake.Core.Hosting;
using Microsoft.Web.WebView2.Core;

namespace FuelIntake.Desktop.Hosting;

/// <summary>
/// Prints HTML to PDF with WebView2 (the Chromium engine the app already uses), via the DevTools
/// "Page.printToPDF" command so CSS page sizes (portrait and landscape) are honoured.
/// <para>
/// Printing runs in its own WebView2 browser with its own working folder
/// (%LOCALAPPDATA%\FuelIntake\WebView2-print). Sharing the main window's browser failed on some
/// laptops with "The group or resource is not in the correct state to perform the requested operation".
/// If the separate browser can't start, the main window's browser is tried as a fallback.
/// </para>
/// The forms are handed to the browser from memory (never written to disk) and nothing is shown on screen.
/// </summary>
public sealed class WebView2PdfPrinter(Dispatcher dispatcher, Func<CoreWebView2Environment?> sharedEnvironment, string printDataFolder) : IPdfPrinter
{
    private const string PrintOptions =
        """{"printBackground":true,"preferCSSPageSize":true,"marginTop":0,"marginBottom":0,"marginLeft":0,"marginRight":0}""";
    private const string PageUrl = "https://fuelintake-print.local/forms.html";

    private CoreWebView2Environment? printEnvironment;

    public async Task<byte[]> PrintAsync(string html, CancellationToken ct = default)
    {
        try
        {
            return await dispatcher.InvokeAsync(() => PrintWithFallbacksAsync(html)).Task.Unwrap();
        }
        catch (Exception ex)
        {
            StartupLog.Write("PDF printing failed", ex);
            throw new InvalidOperationException(
                "The PDF couldn't be made (" + ex.Message + "). Try again; if it keeps happening, close and reopen the app.", ex);
        }
    }

    private async Task<byte[]> PrintWithFallbacksAsync(string html)
    {
        // 1. the app's own print browser (made once, reused)
        try
        {
            return await PrintWithAsync(await PrintEnvironmentAsync(), html);
        }
        catch (Exception first)
        {
            StartupLog.Write($"PDF: print browser failed (0x{first.HResult:X8}); starting it again", first);
            printEnvironment = null;
        }
        // 2. a fresh print browser (the old one may have closed)
        try
        {
            return await PrintWithAsync(await PrintEnvironmentAsync(), html);
        }
        catch (Exception second)
        {
            StartupLog.Write($"PDF: fresh print browser failed (0x{second.HResult:X8}); trying the main window's browser", second);
            printEnvironment = null;
        }
        // 3. the main window's browser
        var shared = sharedEnvironment() ?? throw new InvalidOperationException("the app's browser isn't ready yet");
        return await PrintWithAsync(shared, html);
    }

    private async Task<CoreWebView2Environment> PrintEnvironmentAsync()
    {
        if (printEnvironment is not null) return printEnvironment;
        Directory.CreateDirectory(printDataFolder);
        // WEBVIEW2_USER_DATA_FOLDER (set at start-up for the main window) would override the folder given
        // here and put the print browser back in the main browser's folder, so clear it while creating.
        var saved = Environment.GetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER");
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", null);
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, printDataFolder);
            env.BrowserProcessExited += (_, _) => { if (ReferenceEquals(printEnvironment, env)) printEnvironment = null; };
            StartupLog.Write("PDF: print browser started");
            return printEnvironment = env;
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", saved);
        }
    }

    private static async Task<byte[]> PrintWithAsync(CoreWebView2Environment env, string html)
    {
        // an off-screen window just to host the browser; never visible
        var window = new Window
        {
            Width = 900, Height = 1100, Left = -32000, Top = -32000,
            ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None,
        };
        window.Show();
        CoreWebView2Controller? controller = null;
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            controller = await env.CreateCoreWebView2ControllerAsync(hwnd);
            controller.Bounds = new System.Drawing.Rectangle(0, 0, 900, 1100);
            var web = controller.CoreWebView2;

            // serve the forms from memory, so nothing with client details touches the disk
            var bytes = Encoding.UTF8.GetBytes(html);
            web.AddWebResourceRequestedFilter("https://fuelintake-print.local/*", CoreWebView2WebResourceContext.All);
            web.WebResourceRequested += (_, e) =>
            {
                if (e.Request.Uri == PageUrl)
                    e.Response = env.CreateWebResourceResponse(new MemoryStream(bytes), 200, "OK", "Content-Type: text/html; charset=utf-8");
            };

            var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            web.NavigationCompleted += (_, e) => loaded.TrySetResult(e.IsSuccess);
            web.Navigate(PageUrl);
            var done = await Task.WhenAny(loaded.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            if (done != loaded.Task || !loaded.Task.Result)
                throw new InvalidOperationException("the forms couldn't be laid out for printing");

            var result = await web.CallDevToolsProtocolMethodAsync("Page.printToPDF", PrintOptions);
            using var doc = JsonDocument.Parse(result);
            return Convert.FromBase64String(doc.RootElement.GetProperty("data").GetString()!);
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException($"{ex.Message.Trim()} (0x{ex.HResult:X8})", ex);
        }
        finally
        {
            controller?.Close();
            window.Close();
        }
    }
}
