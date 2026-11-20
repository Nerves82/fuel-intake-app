namespace FuelIntake.Core.Hosting;

/// <summary>
/// Facts about the running app, shown on the "It works" check page and in error reports.
/// Contains no client data.
/// </summary>
/// <param name="AppVersion">Build version, e.g. "0.1.42 (a1b2c3d)".</param>
/// <param name="HostKind">"Desktop" or "Web".</param>
/// <param name="MachineName">Computer name.</param>
/// <param name="RunningFrom">Folder the app is running from.</param>
/// <param name="Runtime">.NET runtime description.</param>
/// <param name="BrowserEngine">Browser component in use, e.g. "WebView2 131.0.2903.70".</param>
public sealed record HostInfo(
    string AppVersion,
    string HostKind,
    string MachineName,
    string RunningFrom,
    string Runtime,
    string BrowserEngine);
