using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using FuelIntake.Desktop.Hosting;

namespace FuelIntake.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch every kind of unhandled error so the app never just disappears.
        DispatcherUnhandledException += OnDispatcherError;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            ReportFatal("Unhandled error", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            StartupLog.Write("Background task error", args.Exception);
            args.SetObserved();
        };

        StartupLog.Write($"Starting from {AppContext.BaseDirectory}");

        try
        {
            // Keep WebView2's working files on the local disk, not next to the app
            // (the app folder may be a OneDrive/SharePoint-synced folder).
            var webViewData = Path.Combine(StartupLog.AppDataFolder, "WebView2");
            Directory.CreateDirectory(webViewData);
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewData);
            StartupLog.Write($"WebView2 data folder: {webViewData}");

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            StartupLog.Write("Main window shown");
        }
        catch (Exception ex)
        {
            ReportFatal("The app could not start", ex);
        }
    }

    private void OnDispatcherError(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ReportFatal("Unexpected error", e.Exception);
    }

    private void ReportFatal(string title, Exception? ex)
    {
        StartupLog.Write(title, ex);
        MessageBox.Show(
            $"{title}.\n\n{ex?.GetType().Name}: {ex?.Message}\n\nDetails were saved to:\n{StartupLog.LogFile}",
            "Fuel Assistance Intake", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(1);
    }
}
