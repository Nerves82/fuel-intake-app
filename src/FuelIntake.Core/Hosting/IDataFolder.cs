namespace FuelIntake.Core.Hosting;

/// <summary>Where client folders and the shared lists are kept.</summary>
/// <param name="Path">The folder that holds the client folders.</param>
/// <param name="DefaultPath">Used until staff choose another folder.</param>
/// <param name="IsDefault">True when no folder has been chosen.</param>
/// <param name="Available">False when a chosen folder can't be reached (not synced, renamed, offline).</param>
/// <param name="SyncedFrom">"OneDrive / SharePoint" when the folder is kept in sync by OneDrive, otherwise null.</param>
public sealed record DataFolderInfo(string Path, string DefaultPath, bool IsDefault, bool Available, string? SyncedFrom);

/// <summary>
/// The client files folder. Phase 1: a folder on this computer, which can be a SharePoint library synced
/// by OneDrive. The choice is kept per Windows user outside the app folder, so new versions keep it.
/// Phase 2 replaces this with a SharePoint site and library.
/// </summary>
public interface IDataFolder
{
    DataFolderInfo Current { get; }

    /// <summary>False where there's no folder dialog (the browser preview).</summary>
    bool CanPick { get; }

    /// <summary>Opens a folder dialog. Null if cancelled.</summary>
    Task<string?> PickAsync(CancellationToken ct = default);

    /// <summary>Why the folder can't be used (missing, read-only…), or null if it's fine.</summary>
    string? Problem(string path);

    /// <summary>A note about a folder that works but looks like the wrong level (a client folder, the settings folder).</summary>
    string? Warning(string path);

    /// <summary>
    /// Switches to <paramref name="path"/> (null = back to the default). With <paramref name="copyLists"/>,
    /// staff, vendors, settings and saved test data sets are copied into the new folder when it doesn't have them yet.
    /// Client folders are never moved.
    /// </summary>
    Task UseAsync(string? path, bool copyLists, CancellationToken ct = default);
}

/// <summary>Rules for the client files folder that don't depend on the host.</summary>
public static class DataFolderRules
{
    public const string SettingsFolderName = "_Fuel Intake Settings";
    public const string TestDataFolderName = "test-data";

    /// <summary>The files copied with "bring my lists along".</summary>
    public static readonly IReadOnlyList<string> ListFiles = ["staff.json", "vendors.json", "settings.json", "compliance.json"];

    public static string SettingsFolder(string root) => System.IO.Path.Combine(root, SettingsFolderName);
    public static string TestDataFolder(string root) => System.IO.Path.Combine(SettingsFolder(root), TestDataFolderName);

    /// <summary>The saved choice if there is one, otherwise the default.</summary>
    public static string Resolve(string? saved, string defaultPath) =>
        string.IsNullOrWhiteSpace(saved) ? defaultPath : Clean(saved);

    /// <summary>What to store for a choice: null when it is the default (so a changed default is followed).</summary>
    public static string? ToSaved(string? chosen, string defaultPath) =>
        string.IsNullOrWhiteSpace(chosen) || SamePath(chosen, defaultPath) ? null : Clean(chosen);

    /// <summary>Windows paths: case doesn't matter, trailing slashes don't count.</summary>
    public static bool SamePath(string a, string b) => string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);

    public static bool IsInside(string path, string folder)
    {
        var p = Clean(path); var f = Clean(folder);
        return SamePath(p, f) || p.StartsWith(f + '\\', StringComparison.OrdinalIgnoreCase)
                              || p.StartsWith(f + '/', StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Spots the two usual mis-picks: the settings folder itself, or one client's folder
    /// (<paramref name="hasIntakeFile"/>: it holds an intake.json).
    /// </summary>
    public static string? Warning(string path, bool hasIntakeFile)
    {
        var name = LastPart(path);
        if (string.Equals(name, SettingsFolderName, StringComparison.OrdinalIgnoreCase))
            return $"That's the {SettingsFolderName} folder. Pick the folder above it, the one that holds the client folders.";
        if (hasIntakeFile)
            return "That looks like one client's folder. Pick the folder above it, the one that holds all the client folders.";
        return null;
    }

    private static string Clean(string p) => p.Trim().TrimEnd('\\', '/');

    private static string LastPart(string p)
    {
        var c = Clean(p);
        var i = c.LastIndexOfAny(['\\', '/']);
        return i < 0 ? c : c[(i + 1)..];
    }
}
