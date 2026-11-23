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
        // the chosen folder lives in LocalAppData, so new versions open the same one; Documents until one is chosen
        dataFolder = new LocalDataFolder(Path.Combine(StartupLog.AppDataFolder, "data-folder.json"),
            Path.Combine(documents, new AgencySettings().ClientsRootFolderName), Dispatcher, () => this);
        var lists = new LocalListSource(dataFolder);
        services.AddSingleton<IDataFolder>(dataFolder);
        services.AddSingleton<IListSource>(lists);
        services.AddSingleton<IClientStore>(new LocalFolderClientStore(dataFolder, lists));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRecentClients>(new LocalRecentClients(Path.Combine(StartupLog.AppDataFolder, "recent-clients.json")));
        services.AddSingleton<FuelIntake.Core.TestData.ITestDataSets>(new LocalTestDataSets(dataFolder));
        services.AddSingleton<FuelIntake.Core.Import.IPendingImports>(new LocalPendingImports(dataFolder));
        services.AddSingleton<FuelIntake.Core.Import.IFilePicker>(new LocalFilePicker(Dispatcher, () => this));
        services.AddSingleton<FuelIntake.Core.Import.IScanReader, WindowsScanReader>();
        services.AddSingleton<IntakeSession>();
        services.AddSingleton<FuelIntake.UI.Printing.PacketRenderer>();
        // PDFs reuse the main window's WebView2 environment (a second, different environment on the same data folder is refused)
        services.AddSingleton<IPdfPrinter>(new WebView2PdfPrinter(Dispatcher, () => Blazor.WebView?.CoreWebView2?.Environment,
            Path.Combine(StartupLog.AppDataFolder, "WebView2-print")));
        services.AddSingleton<IFileOutput, LocalFileOutput>();
        services.AddSingleton<IEmailComposer>(new EmailComposer(Dispatcher));
        services.AddSingleton<ICurrentUser, WindowsCurrentUser>();
        services.AddSingleton(BuildHostInfo());
        foreach (var target in CheckTargets(dataFolder.Root))
            services.AddSingleton(target);

        provider = services.BuildServiceProvider();
        Resources.Add("services", provider);
        InitializeComponent();
        // scans of paper applications are shown from the client files folder under a fixed name
        Blazor.BlazorWebViewInitialized += (_, e) => MapScanFolder(e.WebView.CoreWebView2);
        dataFolder.Changed += () => Dispatcher.Invoke(() => MapScanFolder(Blazor.WebView?.CoreWebView2));
        Title = $"NEKCA Fuel Assistance Intake — v{provider.GetRequiredService<HostInfo>().AppVersion.Split(' ', '+')[0]}";
        Closing += (_, _) => SavePending();
    }

    private void MapScanFolder(Microsoft.Web.WebView2.Core.CoreWebView2? web)
    {
        if (web is null) return;
        try
        {
            try { web.ClearVirtualHostNameToFolderMapping(LocalPendingImports.ViewHost); } catch (ArgumentException) { /* not mapped yet */ }
            if (Directory.Exists(dataFolder.Root))
                web.SetVirtualHostNameToFolderMapping(LocalPendingImports.ViewHost, dataFolder.Root,
                    Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
        }
        catch (Exception ex)
        {
            StartupLog.Write("Couldn't set up showing scans", ex);
        }
    }

    private readonly ServiceProvider provider;
    private readonly LocalDataFolder dataFolder;

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
    private static IEnumerable<CheckTarget> CheckTargets(string clientsRoot)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        yield return new CheckTarget("Documents", documents);
        yield return new CheckTarget("Client files folder", clientsRoot);

        var oneDrive = Environment.GetEnvironmentVariable("OneDriveCommercial")
                       ?? Environment.GetEnvironmentVariable("OneDrive");
        if (!string.IsNullOrEmpty(oneDrive))
            yield return new CheckTarget("OneDrive (work)", oneDrive);

        yield return new CheckTarget("Crisis Fuel team folder (synced)",
            Path.Combine(profile, "Northeast Kingdom Community Action, Inc", "3025 Crisis Fuel (07 31) - General"));
    }
}
