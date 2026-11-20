using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using FuelIntake.Core.Hosting;
using FuelIntake.Desktop.Hosting;
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
        services.AddSingleton<ICurrentUser, WindowsCurrentUser>();
        services.AddSingleton(BuildHostInfo());
        foreach (var target in CheckTargets())
            services.AddSingleton(target);

        Resources.Add("services", services.BuildServiceProvider());
        InitializeComponent();
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
