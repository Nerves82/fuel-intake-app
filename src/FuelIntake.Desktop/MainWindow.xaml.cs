using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using FuelIntake.Core.Hosting;
using FuelIntake.Core.Model;
using FuelIntake.Core.Storage;
using FuelIntake.Desktop.Hosting;
using FuelIntake.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace FuelIntake.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
        // Phase 1 implementations of the Core interfaces (phase 2 swaps these for Microsoft 365 versions).
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var clientsRoot = Path.Combine(documents, new AgencySettings().ClientsRootFolderName);
        var lists = new LocalListSource(Path.Combine(clientsRoot, "_Fuel Intake Settings"));
        services.AddSingleton<IListSource>(lists);
        services.AddSingleton<IClientStore>(new LocalFolderClientStore(clientsRoot, lists));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IntakeSession>();
        services.AddSingleton<FuelIntake.UI.Printing.PacketRenderer>();
        // PDFs reuse the main window's WebView2 environment (a second, different environment on the same data folder is refused)
        services.AddSingleton<IPdfPrinter>(new WebView2PdfPrinter(Dispatcher, () => Blazor.WebView?.CoreWebView2?.Environment));
        services.AddSingleton<IFileOutput, LocalFileOutput>();
        services.AddSingleton<ICurrentUser, WindowsCurrentUser>();
        services.AddSingleton(BuildHostInfo());
        foreach (var target in CheckTargets())
            services.AddSingleton(target);

        provider = services.BuildServiceProvider();
        Resources.Add("services", provider);
        InitializeComponent();
        Closing += (_, _) => SavePending();
    }

    private readonly ServiceProvider provider;

    /// <summary>Saves any unsaved edits before the window closes (runs off the UI thread to avoid a deadlock).</summary>
    private void SavePending()
    {
        try
        {
            var session = provider.GetRequiredService<IntakeSession>();
            if (session.HasUnsavedChanges)
                Task.Run(session.SaveAsync).Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            StartupLog.Write("Save on close failed", ex);
        }
    }

    private static HostInfo BuildHostInfo()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        string browser;
        try { browser = "WebView2 " + CoreWebView2Environment.GetAvailableBrowserVersionString(); }
        catch (Exception ex) { browser = "WebView2 not found: " + ex.Message; }

        return new HostInfo(
            AppVersion: version,
            HostKind: "Desktop",
            MachineName: Environment.MachineName,
            RunningFrom: AppContext.BaseDirectory,
            Runtime: RuntimeInformation.FrameworkDescription,
            BrowserEngine: browser);
    }

    /// <summary>Folders the "It works" page tests for write access.</summary>
    private static IEnumerable<CheckTarget> CheckTargets()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        yield return new CheckTarget("Documents", documents);
        yield return new CheckTarget("Client files folder", Path.Combine(documents, "Client Files - Fuel 26-27"));

        var oneDrive = Environment.GetEnvironmentVariable("OneDriveCommercial")
                       ?? Environment.GetEnvironmentVariable("OneDrive");
        if (!string.IsNullOrEmpty(oneDrive))
            yield return new CheckTarget("OneDrive (work)", oneDrive);

        yield return new CheckTarget("Crisis Fuel team folder (synced)",
            Path.Combine(profile, "Northeast Kingdom Community Action, Inc", "3025 Crisis Fuel (07 31) - General"));
    }
}
