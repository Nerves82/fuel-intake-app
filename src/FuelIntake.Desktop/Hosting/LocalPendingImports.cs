using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FuelIntake.Core.Import;
using FuelIntake.Core.Storage;

namespace FuelIntake.Desktop.Hosting;

/// <summary>
/// Paper applications waiting to be checked: "_PENDING yyyyMMdd-HHmm xxxx" folders in the client files
/// folder, each with the scans and import.json. The "_" keeps them out of the client list.
/// </summary>
public sealed class LocalPendingImports(LocalDataFolder folder) : IPendingImports
{
    public const string ImportFileName = "import.json";
    /// <summary>The built-in browser shows scans from the client files folder under this name (see MainWindow).</summary>
    public const string ViewHost = "clientfiles.fuelintake";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyList<PendingImportInfo>> ListAsync(CancellationToken ct = default)
    {
        folder.EnsureAvailable();
        var list = new List<PendingImportInfo>();
        foreach (var dir in new DirectoryInfo(folder.Root).EnumerateDirectories(PaperImportRules.HoldingPrefix + "*"))
        {
            var file = Path.Combine(dir.FullName, ImportFileName);
            if (!File.Exists(file)) continue;
            try
            {
                var i = Read(await File.ReadAllTextAsync(file, ct));
                list.Add(new PendingImportInfo(dir.Name, PaperImportRules.DisplayName(i), i.CreatedBy, i.CreatedAt,
                    PaperImportRules.ConfirmedCount(i), i.Fields.Count));
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
            {
                StartupLog.Write("A pending paper application couldn't be read", ex);
            }
        }
        return list.OrderByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<string> CreateAsync(PaperImport import, IReadOnlyList<string> scanLocations, CancellationToken ct = default)
    {
        folder.EnsureAvailable();
        var id = $"{PaperImportRules.HoldingPrefix}{import.CreatedAt.ToLocalTime():yyyyMMdd-HHmm} {Guid.NewGuid().ToString("N")[..4]}";
        var dir = Directory.CreateDirectory(Path.Combine(folder.Root, id));
        import.ScanFiles = [];
        foreach (var source in scanLocations)
        {
            var name = UniqueName(dir.FullName, ClientFolders.SafeName(Path.GetFileName(source)));
            File.Copy(source, Path.Combine(dir.FullName, name));
            import.ScanFiles.Add(name);
        }
        await SaveAsync(id, import, ct);
        return id;
    }

    public async Task<PaperImport> GetAsync(string id, CancellationToken ct = default) =>
        Read(await File.ReadAllTextAsync(Path.Combine(Dir(id), ImportFileName), ct));

    public async Task SaveAsync(string id, PaperImport import, CancellationToken ct = default)
    {
        var path = Path.Combine(Dir(id), ImportFileName);
        var temp = path + ".saving";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(import, Json), ct);
        File.Move(temp, path, overwrite: true);
    }

    public string? ScanLocation(string id, string scanFile) => Path.Combine(Dir(id), Path.GetFileName(scanFile));

    public string? ViewUrl(string id, string scanFile) =>
        $"https://{ViewHost}/{Uri.EscapeDataString(Path.GetFileName(Dir(id)))}/{Uri.EscapeDataString(Path.GetFileName(scanFile))}";

    public Task FinishAsync(string id, string clientId, CancellationToken ct = default)
    {
        var dir = Dir(id);
        foreach (var file in Directory.EnumerateFiles(dir))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith(ImportFileName, StringComparison.OrdinalIgnoreCase)) continue;
            File.Move(file, Path.Combine(clientId, UniqueName(clientId, name)));
        }
        Directory.Delete(dir, recursive: true); // only import.json is left; its values are now in intake.json
        StartupLog.Write("A paper application was turned into a client");
        return Task.CompletedTask;
    }

    public Task DiscardAsync(string id, CancellationToken ct = default)
    {
        RecycleBin.Send(Dir(id));
        StartupLog.Write("A pending paper application was discarded (Recycle Bin)");
        return Task.CompletedTask;
    }

    private string Dir(string id)
    {
        if (!id.StartsWith(PaperImportRules.HoldingPrefix, StringComparison.Ordinal) || id != Path.GetFileName(id) || id.Contains(".."))
            throw new InvalidOperationException("That isn't a pending paper application.");
        var dir = Path.Combine(folder.Root, id);
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException("This paper application isn't there any more (someone may have finished or discarded it).");
        return dir;
    }

    private static PaperImport Read(string json)
    {
        var i = JsonSerializer.Deserialize<PaperImport>(json, Json) ?? throw new InvalidDataException("empty import file");
        if (i.FormatVersion > PaperImport.CurrentFormatVersion) throw new InvalidDataException("made by a newer version of the app");
        return i;
    }

    private static string UniqueName(string dir, string name)
    {
        var candidate = name;
        for (var n = 2; File.Exists(Path.Combine(dir, candidate)); n++)
            candidate = $"{Path.GetFileNameWithoutExtension(name)} ({n}){Path.GetExtension(name)}";
        return candidate;
    }
}

/// <summary>The Windows Open dialog, for choosing scanned pages.</summary>
public sealed class LocalFilePicker(System.Windows.Threading.Dispatcher dispatcher, Func<System.Windows.Window?> owner) : IFilePicker
{
    public bool CanPick => true;

    public Task<IReadOnlyList<string>> PickScansAsync(CancellationToken ct = default) =>
        dispatcher.InvokeAsync<IReadOnlyList<string>>(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose the scanned pages of the paper application",
                Filter = "Scans (PDF, JPG, PNG)|*.pdf;*.jpg;*.jpeg;*.png|All files|*.*",
                Multiselect = true,
            };
            var w = owner();
            var ok = w is null ? dialog.ShowDialog() : dialog.ShowDialog(w);
            return ok == true ? dialog.FileNames : [];
        }).Task;
}
