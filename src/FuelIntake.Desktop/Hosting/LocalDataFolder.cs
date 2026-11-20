using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using FuelIntake.Core.Hosting;
using FuelIntake.Core.Storage;
using Microsoft.Win32;

namespace FuelIntake.Desktop.Hosting;

/// <summary>
/// The client files folder for this Windows user. The choice is saved in
/// %LOCALAPPDATA%\FuelIntake\data-folder.json, outside the versioned app folder, so every new
/// version opens the same folder without asking. With no choice saved, Documents\Client Files - Fuel 26-27.
/// </summary>
public sealed class LocalDataFolder : IDataFolder
{
    private sealed record Saved(string? ClientsFolder);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string choiceFile;
    private readonly Dispatcher dispatcher;
    private readonly Func<Window?> owner;

    public LocalDataFolder(string choiceFile, string defaultPath, Dispatcher dispatcher, Func<Window?> owner)
    {
        this.choiceFile = choiceFile;
        this.dispatcher = dispatcher;
        this.owner = owner;
        DefaultPath = defaultPath;
        Root = DataFolderRules.Resolve(ReadChoice(), defaultPath);
    }

    public string DefaultPath { get; }

    /// <summary>The folder holding the client folders.</summary>
    public string Root { get; private set; }

    public bool IsDefault => DataFolderRules.SamePath(Root, DefaultPath);

    public string SettingsFolder => DataFolderRules.SettingsFolder(Root);
    public string TestDataFolder => DataFolderRules.TestDataFolder(Root);

    public DataFolderInfo Current => new(Root, DefaultPath, IsDefault, IsDefault || Directory.Exists(Root), SyncedFrom(Root));

    public bool CanPick => true;

    /// <summary>
    /// Call before reading or writing. The default folder is created when missing; a chosen folder never is,
    /// because creating a missing SharePoint folder would quietly keep client files on this laptop only.
    /// </summary>
    public void EnsureAvailable()
    {
        if (IsDefault) { Directory.CreateDirectory(Root); return; }
        if (!Directory.Exists(Root))
            throw new DataFolderMissingException(
                $"The client files folder isn't available: {Root}. If it's a SharePoint folder, check that OneDrive is " +
                "running and the library is synced on this computer, or choose another folder under Staff, vendors & settings → Data folder.");
    }

    public Task<string?> PickAsync(CancellationToken ct = default) =>
        dispatcher.InvokeAsync(() =>
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Choose the folder that holds the client folders",
                InitialDirectory = Directory.Exists(Root) ? Root : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            var w = owner();
            var ok = w is null ? dialog.ShowDialog() : dialog.ShowDialog(w);
            return ok == true ? dialog.FolderName : null;
        }).Task;

    public string? Problem(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return "Choose a full folder path, like C:\\Users\\you\\…";
        if (!Directory.Exists(path)) return "That folder doesn't exist on this computer.";
        var probe = Path.Combine(path, $".fuelintake-write-check-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probe, "write check");
            File.Delete(probe);
            return null;
        }
        catch (Exception ex)
        {
            return "The app can't save files in that folder (" + ex.Message + ").";
        }
    }

    public string? Warning(string path) =>
        DataFolderRules.Warning(path, File.Exists(Path.Combine(path, ClientFolders.IntakeFileName)));

    public Task UseAsync(string? path, bool copyLists, CancellationToken ct = default)
    {
        var next = DataFolderRules.Resolve(path, DefaultPath);
        if (!DataFolderRules.SamePath(next, DefaultPath) && Problem(next) is { } problem)
            throw new InvalidOperationException(problem);

        var oldSettings = SettingsFolder;
        if (copyLists && !DataFolderRules.SamePath(next, Root))
            CopyLists(oldSettings, DataFolderRules.SettingsFolder(next));

        Directory.CreateDirectory(Path.GetDirectoryName(choiceFile)!);
        File.WriteAllText(choiceFile, JsonSerializer.Serialize(new Saved(DataFolderRules.ToSaved(next, DefaultPath)), Json));
        StartupLog.Write($"Client files folder changed to {next}");
        Root = next;
        return Task.CompletedTask;
    }

    /// <summary>Copies the lists and saved test sets, never overwriting what the new folder already has.</summary>
    private static void CopyLists(string from, string to)
    {
        if (!Directory.Exists(from)) return;
        Directory.CreateDirectory(to);
        foreach (var name in DataFolderRules.ListFiles)
        {
            var src = Path.Combine(from, name);
            var dst = Path.Combine(to, name);
            if (File.Exists(src) && !File.Exists(dst)) File.Copy(src, dst);
        }
        var testFrom = Path.Combine(from, DataFolderRules.TestDataFolderName);
        if (!Directory.Exists(testFrom)) return;
        var testTo = Path.Combine(to, DataFolderRules.TestDataFolderName);
        Directory.CreateDirectory(testTo);
        foreach (var src in Directory.EnumerateFiles(testFrom, "*.json"))
        {
            var dst = Path.Combine(testTo, Path.GetFileName(src));
            if (!File.Exists(dst)) File.Copy(src, dst);
        }
    }

    private string? ReadChoice()
    {
        try
        {
            return File.Exists(choiceFile) ? JsonSerializer.Deserialize<Saved>(File.ReadAllText(choiceFile), Json)?.ClientsFolder : null;
        }
        catch (Exception ex)
        {
            StartupLog.Write("Data folder choice couldn't be read; using the default", ex);
            return null;
        }
    }

    /// <summary>
    /// Folders OneDrive keeps in sync, from the current user's OneDrive settings (no admin rights needed):
    /// the work OneDrive and every SharePoint library synced or added as a shortcut.
    /// </summary>
    private static string? SyncedFrom(string path)
    {
        try
        {
            var roots = new List<string>();
            if (Environment.GetEnvironmentVariable("OneDriveCommercial") is { Length: > 0 } od) roots.Add(od);
            using var accounts = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts");
            foreach (var name in accounts?.GetSubKeyNames() ?? [])
            {
                using var account = accounts!.OpenSubKey(name);
                if (account?.GetValue("UserFolder") is string userFolder) roots.Add(userFolder);
                using var mounts = account?.OpenSubKey("ScopeIdToMountPointPathCache");
                foreach (var v in mounts?.GetValueNames() ?? [])
                    if (mounts!.GetValue(v) is string mount) roots.Add(mount);
            }
            return roots.Any(r => DataFolderRules.IsInside(path, r)) ? "OneDrive / SharePoint" : null;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class DataFolderMissingException(string message) : Exception(message);
