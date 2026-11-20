using System.IO;
using FuelIntake.Core.TestData;

namespace FuelIntake.Desktop.Hosting;

/// <summary>Saved test data sets: one JSON file each in "_Fuel Intake Settings\test-data", shared by everyone using that folder.</summary>
public sealed class LocalTestDataSets(LocalDataFolder data) : ITestDataSets
{
    private string folder => data.TestDataFolder;

    public async Task<IReadOnlyList<TestDataSet>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(folder)) return [];
        var sets = new List<TestDataSet>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                var set = TestDataSetRules.FromJson(await File.ReadAllTextAsync(file, ct));
                if (set is null) { StartupLog.Write($"Skipped test data set {Path.GetFileName(file)}: not a test data set"); continue; }
                set.Id = Path.GetFileNameWithoutExtension(file);
                sets.Add(set);
            }
            catch (IOException ex) { StartupLog.Write($"Couldn't read test data set {Path.GetFileName(file)}", ex); }
        }
        return sets.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task SaveAsync(TestDataSet set, CancellationToken ct = default)
    {
        data.EnsureAvailable();
        Directory.CreateDirectory(folder);
        var path = PathFor(set.Id);
        var temp = path + ".saving";
        await File.WriteAllTextAsync(temp, TestDataSetRules.ToJson(set), ct);
        File.Move(temp, path, overwrite: true);
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var path = PathFor(id);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>The id is the file name; anything with a path in it is refused so it can't point outside the folder.</summary>
    private string PathFor(string id)
    {
        if (id.Trim().Length == 0 || id != Path.GetFileName(id) || id.Contains(".."))
            throw new ArgumentException("Bad test data set id.", nameof(id));
        return Path.Combine(folder, id + ".json");
    }
}
