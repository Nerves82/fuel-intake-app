using System.IO;
using FuelIntake.Core.Model;
using FuelIntake.Core.Storage;

namespace FuelIntake.Desktop.Hosting;

/// <summary>
/// Phase 1 storage: one folder per client under the clients root (a OneDrive-synced SharePoint folder),
/// holding intake.json plus generated PDFs. Folder ids are full paths.
/// </summary>
public sealed class LocalFolderClientStore(LocalDataFolder folder, IListSource lists) : IClientStore
{
    public string ClientsRoot => folder.Root;

    public Task<IReadOnlyList<ClientFolderInfo>> ListClientsAsync(CancellationToken ct = default)
    {
        folder.EnsureAvailable();
        IReadOnlyList<ClientFolderInfo> list = new DirectoryInfo(ClientsRoot).GetDirectories()
            .Where(d => !d.Name.StartsWith(ClientFolders.HiddenPrefix, StringComparison.Ordinal)
                        || d.Name.StartsWith(ClientFolders.TestPrefix, StringComparison.Ordinal))
            .Select(Info)
            .OrderBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return Task.FromResult(list);
    }

    public async Task<ClientFolderInfo> CreateClientAsync(string firstName, string lastName, CancellationToken ct = default)
    {
        folder.EnsureAvailable();
        var settings = await lists.GetSettingsAsync(ct);
        var path = Path.Combine(ClientsRoot, ClientFolders.FolderName(firstName, lastName, settings.FolderNameSeparator));
        Directory.CreateDirectory(path);
        if (!lastName.StartsWith(ClientFolders.TestPrefix, StringComparison.Ordinal))
            CopyTemplates(path, settings, firstName, lastName);
        return Info(new DirectoryInfo(path));
    }

    public async Task<IntakeRecord?> LoadAsync(string clientId, CancellationToken ct = default)
    {
        var file = Path.Combine(clientId, ClientFolders.IntakeFileName);
        if (!File.Exists(file)) return null;
        return IntakeJson.Deserialize(await File.ReadAllTextAsync(file, ct));
    }

    public async Task SaveAsync(string clientId, IntakeRecord record, CancellationToken ct = default)
    {
        Directory.CreateDirectory(clientId);
        var file = Path.Combine(clientId, ClientFolders.IntakeFileName);
        var temp = file + ".saving";
        // write to a temp file first so a crash never leaves a half-written intake.json
        await File.WriteAllTextAsync(temp, IntakeJson.Serialize(record), ct);
        File.Move(temp, file, overwrite: true);
    }

    public Task<DateTimeOffset?> GetLastSavedAsync(string clientId, CancellationToken ct = default)
    {
        var file = new FileInfo(Path.Combine(clientId, ClientFolders.IntakeFileName));
        return Task.FromResult<DateTimeOffset?>(file.Exists ? new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero) : null);
    }

    /// <summary>Copies the forms the app doesn't draw (consent forms) into a new client folder. Never overwrites.</summary>
    private static void CopyTemplates(string clientFolder, AgencySettings settings, string first, string last)
    {
        var templateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), settings.TemplateFolder);
        if (!Directory.Exists(templateFolder)) { StartupLog.Write($"Template folder not found: {templateFolder}"); return; }
        var today = DateOnly.FromDateTime(DateTime.Now);
        foreach (var name in settings.TemplateFiles)
        {
            var source = Path.Combine(templateFolder, name);
            var dest = Path.Combine(clientFolder, ClientFolders.TemplateCopyName(name, first, last, today, settings.FileDateFormat));
            try
            {
                if (File.Exists(source) && !File.Exists(dest)) File.Copy(source, dest);
                else if (!File.Exists(source)) StartupLog.Write($"Template not found: {name}");
            }
            catch (Exception ex) { StartupLog.Write($"Couldn't copy template {name}", ex); }
        }
    }

    private static ClientFolderInfo Info(DirectoryInfo d)
    {
        var intake = new FileInfo(Path.Combine(d.FullName, ClientFolders.IntakeFileName));
        var when = intake.Exists ? intake.LastWriteTime : d.LastWriteTime;
        return new ClientFolderInfo(d.FullName, d.Name, new DateTimeOffset(when), intake.Exists);
    }
}
