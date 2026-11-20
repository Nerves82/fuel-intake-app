using System.IO;
using System.Text.Json;
using FuelIntake.Core.Storage;

namespace FuelIntake.Desktop.Hosting;

/// <summary>Recently opened clients for this Windows user: %LOCALAPPDATA%\FuelIntake\recent-clients.json.</summary>
public sealed class LocalRecentClients(string file) : IRecentClients
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<IReadOnlyList<RecentClient>> GetAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(file)) return [];
            return JsonSerializer.Deserialize<List<RecentClient>>(await File.ReadAllTextAsync(file, ct), Json) ?? [];
        }
        catch (Exception ex)
        {
            StartupLog.Write("Recent clients list couldn't be read; starting fresh", ex);
            return [];
        }
    }

    public async Task TouchAsync(ClientFolderInfo client, DateTimeOffset at, CancellationToken ct = default)
    {
        var list = RecentClientList.Touch(await GetAsync(ct), client, at);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(list, Json), ct);
    }
}
