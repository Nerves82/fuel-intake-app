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
        // never re-create a folder someone deleted (or that OneDrive removed) while this client was open
        if (!Directory.Exists(clientId))
            throw new DirectoryNotFoundException("This client's folder no longer exists (it may have been deleted). Changes weren't saved.");
        var file = Path.Combine(clientId, ClientFolders.IntakeFileName);
        var temp = file + ".saving";
        // write to a temp file first so a crash never leaves a half-written intake.json
        await File.WriteAllTextAsync(temp, IntakeJson.Serialize(record), ct);
        File.Move(temp, file, overwrite: true);
    }

    public Task<IReadOnlyList<string>> ListFilesAsync(string clientId, CancellationToken ct = default)
    {
        var dir = new DirectoryInfo(CheckClientFolder(clientId));
        IReadOnlyList<string> names = dir.Exists
            ? dir.GetDirectories().Select(d => d.Name + "\\").Concat(dir.GetFiles().Select(f => f.Name))
                 .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList()
            : [];
        return Task.FromResult(names);
    }

    public Task DeleteClientAsync(string clientId, CancellationToken ct = default)
    {
        var path = CheckClientFolder(clientId);
        if (!Directory.Exists(path)) return Task.CompletedTask;
        RecycleBin.Send(path);
        StartupLog.Write("A client folder was moved to the Recycle Bin"); // no client name in the log
        return Task.CompletedTask;
    }

    public Task<ClientRenameResult> RenameClientAsync(string clientId, string newFolderName, IReadOnlyList<FileRename> files, CancellationToken ct = default)
    {
        folder.EnsureAvailable();
        var from = CheckClientFolder(clientId);
        if (newFolderName.Length == 0 || ClientFolders.SafeName(newFolderName) != newFolderName
            || (newFolderName.StartsWith(ClientFolders.HiddenPrefix, StringComparison.Ordinal) && !newFolderName.StartsWith(ClientFolders.TestPrefix, StringComparison.Ordinal)))
            throw new InvalidOperationException("That isn't a usable folder name.");
        var to = Path.Combine(Path.GetDirectoryName(from)!, newFolderName);

        // 1. the folder (if this fails, nothing has changed)
        if (!string.Equals(from, to, StringComparison.Ordinal))
        {
            var caseOnly = string.Equals(from, to, StringComparison.OrdinalIgnoreCase);
            if (!caseOnly && Directory.Exists(to))
                throw new IOException($"There's already a folder named \"{newFolderName}\". Is this client already in the list under the corrected name?");
            try
            {
                if (caseOnly)
                {
                    // Windows can't rename "smith, jane" to "Smith, Jane" in one step
                    var temp = from + ".renaming-" + Guid.NewGuid().ToString("N")[..6];
                    Directory.Move(from, temp);
                    Directory.Move(temp, to);
                }
                else Directory.Move(from, to);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException("Windows couldn't rename the folder. Close any files from this client's folder " +
                                      "(PDFs, File Explorer windows) and try again. (" + ex.Message + ")", ex);
            }
        }

        // 2. the files inside it
        var renamed = new List<FileRename>();
        var problems = new List<string>();
        foreach (var f in files)
        {
            if (f.From != Path.GetFileName(f.From) || f.To != Path.GetFileName(f.To) || f.To.Length == 0 || ClientFolders.SafeName(f.To) != f.To)
            {
                problems.Add($"{f.From}: not a plain file name");
                continue;
            }
            var src = Path.Combine(to, f.From);
            var dst = Path.Combine(to, f.To);
            try
            {
                if (!File.Exists(src)) { problems.Add($"{f.From}: not found"); continue; }
                if (File.Exists(dst) && !string.Equals(src, dst, StringComparison.OrdinalIgnoreCase)) { problems.Add($"{f.From}: {f.To} already exists"); continue; }
                File.Move(src, dst);
                renamed.Add(f);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problems.Add($"{f.From}: {ex.Message}");
            }
        }
        StartupLog.Write($"A client folder was renamed ({renamed.Count} file(s) renamed, {problems.Count} skipped)"); // no names in the log
        return Task.FromResult(new ClientRenameResult(Info(new DirectoryInfo(to)), renamed, problems));
    }

    /// <summary>Only a folder directly inside the client files folder (not the settings folder) can be listed or deleted.</summary>
    private string CheckClientFolder(string clientId)
    {
        var full = Path.GetFullPath(clientId).TrimEnd('\\', '/');
        var parent = Path.GetDirectoryName(full);
        var name = Path.GetFileName(full);
        if (parent is null || !string.Equals(Path.GetFullPath(ClientsRoot).TrimEnd('\\', '/'), parent, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(name)
            || (name.StartsWith(ClientFolders.HiddenPrefix, StringComparison.Ordinal) && !name.StartsWith(ClientFolders.TestPrefix, StringComparison.Ordinal)))
            throw new InvalidOperationException("That isn't a client folder in the client files folder.");
        return full;
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
