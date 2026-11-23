using System.Diagnostics;
using System.IO;
using FuelIntake.Core.Storage;

namespace FuelIntake.Desktop.Hosting;

/// <summary>Saves generated PDFs into the client's folder and opens them in the default PDF viewer.</summary>
public sealed class LocalFileOutput : IFileOutput
{
    public async Task<string> SaveAsync(string clientId, string fileName, byte[] content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(clientId);
        var path = Path.Combine(clientId, fileName);
        try
        {
            await File.WriteAllBytesAsync(path, content, ct);   // re-printing replaces the earlier copy
            return path;
        }
        catch (IOException)
        {
            // usually the old copy is open in a PDF viewer: save beside it instead
            var stem = Path.GetFileNameWithoutExtension(fileName);
            for (var n = 2; ; n++)
            {
                var alt = Path.Combine(clientId, $"{stem} ({n}){Path.GetExtension(fileName)}");
                if (File.Exists(alt)) continue;
                await File.WriteAllBytesAsync(alt, content, ct);
                return alt;
            }
        }
    }

    public string LocationOf(string clientId, string fileName) => Path.Combine(clientId, Path.GetFileName(fileName));

    public Task OpenAsync(string location, CancellationToken ct = default)
    {
        // a folder opens in File Explorer; a file opens in its usual app
        if (Directory.Exists(location))
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { location }, UseShellExecute = false });
        else
            Process.Start(new ProcessStartInfo(location) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
