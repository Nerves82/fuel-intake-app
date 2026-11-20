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
