using System.Text.Json;
using System.Text.Json.Serialization;
using FuelIntake.Core.Model;
using FuelIntake.Core.Storage;

namespace FuelIntake.Core.TestData;

/// <summary>A practice case staff saved from a test client, to start new test clients from.</summary>
public sealed class TestDataSet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public StaffStamp? SavedBy { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public IntakeRecord Record { get; set; } = new() { IsTestData = true };
}

/// <summary>
/// Saved test data sets, shared by the team. Phase 1: JSON files in the settings folder's
/// "test-data" subfolder. Phase 2: a SharePoint list or library.
/// </summary>
public interface ITestDataSets
{
    /// <summary>All saved sets, sorted by name.</summary>
    Task<IReadOnlyList<TestDataSet>> ListAsync(CancellationToken ct = default);

    /// <summary>Adds the set, or replaces the one with the same <see cref="TestDataSet.Id"/>.</summary>
    Task SaveAsync(TestDataSet set, CancellationToken ct = default);

    Task DeleteAsync(string id, CancellationToken ct = default);
}

public static class TestDataSetRules
{
    public const int MaxNameLength = 80;

    /// <summary>
    /// Makes a saved set from a test client's record. Only test records can be saved: a real client's
    /// SSNs, income and address must never become practice data.
    /// </summary>
    public static TestDataSet FromRecord(IntakeRecord record, string name, string? description, StaffStamp? by, DateTimeOffset at)
    {
        if (!record.IsTestData)
            throw new InvalidOperationException("Only test clients can be saved as test data sets.");
        name = name.Trim();
        if (name.Length == 0) throw new ArgumentException("Give the test data set a name.", nameof(name));
        if (name.Length > MaxNameLength) name = name[..MaxNameLength];

        return new TestDataSet
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            SavedBy = by,
            SavedAt = at,
            Record = Strip(Clone(record)),
        };
    }

    /// <summary>A fresh record for a new test client, using that client's own name and today's date.</summary>
    public static IntakeRecord Build(TestDataSet set, string first, string last, DateOnly today)
    {
        var r = Strip(Clone(set.Record));
        r.IsTestData = true;
        // household members who shared the saved client's last name take the new one
        var oldLast = r.Client.LastName?.Trim();
        if (!string.IsNullOrEmpty(oldLast))
            foreach (var m in r.Household.Skip(1).Where(m => string.Equals(m.LastName?.Trim(), oldLast, StringComparison.OrdinalIgnoreCase)))
                m.LastName = last;
        r.Client.FirstName = first;
        r.Client.LastName = last;
        r.Staff.AppDate = today;
        return r;
    }

    /// <summary>Turns a saved set into an entry for the "Practice with test data" list.</summary>
    public static SampleIntakes.Sample AsSample(TestDataSet set) =>
        new(set.Name, set.Description ?? "", (f, l, d) => Build(set, f, l, d), SavedId: set.Id);

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string ToJson(TestDataSet set) => JsonSerializer.Serialize(set, Json);

    /// <summary>Reads a saved set; null if the file isn't one or isn't marked as test data.</summary>
    public static TestDataSet? FromJson(string json)
    {
        try
        {
            var set = JsonSerializer.Deserialize<TestDataSet>(json, Json);
            return set is { Record.IsTestData: true } && set.Name.Length > 0 && set.Record.FormatVersion <= IntakeRecord.CurrentFormatVersion
                ? set : null;
        }
        catch (JsonException) { return null; }
    }

    private static IntakeRecord Clone(IntakeRecord r) => IntakeJson.Deserialize(IntakeJson.Serialize(r));

    /// <summary>Drops who-did-what: history, notes, creator and the staff on the saved client.</summary>
    private static IntakeRecord Strip(IntakeRecord r)
    {
        r.Activity = [];
        r.Notes = [];
        r.CreatedBy = null;
        r.CreatedAt = null;
        r.Staff.StaffName = null;
        r.Staff.InitialsOverride = null;
        r.Staff.PhoneOverride = null;
        r.Staff.EmailOverride = null;
        r.Staff.StaffSnapshot = null;
        r.Staff.CaseNumber = null;
        return r;
    }
}

/// <summary>Keeps sets in memory (browser preview and tests).</summary>
public sealed class MemoryTestDataSets : ITestDataSets
{
    private readonly List<TestDataSet> sets = [];

    public Task<IReadOnlyList<TestDataSet>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TestDataSet>>(sets.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList());

    public Task SaveAsync(TestDataSet set, CancellationToken ct = default)
    {
        sets.RemoveAll(s => s.Id == set.Id);
        sets.Add(set);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        sets.RemoveAll(s => s.Id == id);
        return Task.CompletedTask;
    }
}
