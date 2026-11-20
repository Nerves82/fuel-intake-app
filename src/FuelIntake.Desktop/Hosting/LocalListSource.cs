using System.IO;
using System.Text.Json;
using FuelIntake.Core.Contacts;
using FuelIntake.Core.Model;
using FuelIntake.Core.Storage;

namespace FuelIntake.Desktop.Hosting;

/// <summary>
/// Phase 1 lists: staff.json, vendors.json and settings.json in an app-data folder.
/// Missing files are created (empty lists, default settings) so staff can see where they go.
/// Phase 2 reads Microsoft Lists instead.
/// </summary>
public sealed class LocalListSource(string dataFolder) : IListSource
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public string DataFolder { get; } = dataFolder;

    public async Task<StaffDirectory> GetStaffAsync(CancellationToken ct = default) =>
        new(await ReadAsync<List<StaffMember>>("staff.json", [], ct));

    public async Task<VendorDirectory> GetVendorsAsync(CancellationToken ct = default) =>
        new(await ReadAsync<List<Vendor>>("vendors.json", [], ct));

    public Task<AgencySettings> GetSettingsAsync(CancellationToken ct = default) =>
        ReadAsync("settings.json", new AgencySettings(), ct);

    public Task SaveStaffAsync(IEnumerable<StaffMember> staff, CancellationToken ct = default) =>
        WriteAsync("staff.json", staff.ToList(), ct);

    public Task SaveVendorsAsync(IEnumerable<Vendor> vendors, CancellationToken ct = default) =>
        WriteAsync("vendors.json", vendors.ToList(), ct);

    public Task SaveSettingsAsync(AgencySettings settings, CancellationToken ct = default) =>
        WriteAsync("settings.json", settings, ct);

    private async Task WriteAsync<T>(string name, T value, CancellationToken ct)
    {
        Directory.CreateDirectory(DataFolder);
        var path = Path.Combine(DataFolder, name);
        var temp = path + ".saving";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(value, Json), ct);
        File.Move(temp, path, overwrite: true);
    }

    private async Task<T> ReadAsync<T>(string name, T fallback, CancellationToken ct)
    {
        var path = Path.Combine(DataFolder, name);
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(DataFolder);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(fallback, Json), ct);
            return fallback;
        }
        try
        {
            return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path, ct), Json) ?? fallback;
        }
        catch (JsonException ex)
        {
            throw new IntakeFormatException($"{name} in {DataFolder} couldn't be read ({ex.Message}).", ex);
        }
    }
}
