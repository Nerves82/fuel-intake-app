using FuelIntake.Core.Model;

namespace FuelIntake.Core.Activity;

/// <summary>
/// One person's uninterrupted run of activity on one day, shown as a single item.
/// <see cref="FieldChanges"/> is the net effect (first "from", last "to"; fields changed back drop out).
/// </summary>
public sealed record ActivityGroup(
    StaffStamp By,
    DateTimeOffset From,
    DateTimeOffset To,
    bool Created,
    IReadOnlyList<FieldChange> FieldChanges,
    IReadOnlyList<ActivityEntry> NoteEntries,
    IReadOnlyList<ActivityEntry> Entries)
{
    /// <summary>"created the intake, changed 3 fields, added 2 notes".</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (Created) parts.Add("created the intake");
            if (FieldChanges.Count > 0) parts.Add($"changed {FieldChanges.Count} field{(FieldChanges.Count == 1 ? "" : "s")}");
            var added = NoteEntries.Count(e => e.Action == "Added a note");
            var deleted = NoteEntries.Count(e => e.Action == "Deleted a note");
            if (added > 0) parts.Add(added == 1 ? "added a note" : $"added {added} notes");
            if (deleted > 0) parts.Add(deleted == 1 ? "deleted a note" : $"deleted {deleted} notes");
            var other = Entries.Count(e => e.Action is not ("Created" or "Edited" or "Added a note" or "Deleted a note"));
            if (other > 0) parts.Add(string.Join(", ", Entries.Where(e => e.Action is not ("Created" or "Edited" or "Added a note" or "Deleted a note"))
                .Select(e => e.Action.ToLowerInvariant()).Distinct()));
            if (parts.Count == 0) parts.Add("made changes that cancelled out");
            return string.Join(", ", parts);
        }
    }
}

public static partial class ActivityLog
{
    /// <summary>
    /// Groups the log for display: consecutive entries by the same person on the same day (in
    /// <paramref name="zone"/>) become one group. Oldest first, like <see cref="IntakeRecord.Activity"/>.
    /// </summary>
    public static IReadOnlyList<ActivityGroup> Group(IEnumerable<ActivityEntry> entries, TimeZoneInfo zone)
    {
        var groups = new List<List<ActivityEntry>>();
        foreach (var e in entries.OrderBy(e => e.At))
        {
            var current = groups.LastOrDefault();
            if (current is not null && SamePerson(current[^1].By, e.By) && Day(current[^1].At, zone) == Day(e.At, zone))
                current.Add(e);
            else
                groups.Add([e]);
        }
        return groups.Select(Build).ToList();
    }

    private static DateOnly Day(DateTimeOffset at, TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    private static ActivityGroup Build(List<ActivityEntry> run)
    {
        var net = new List<FieldChange>();
        foreach (var c in run.Where(e => e.Action == "Edited").SelectMany(e => e.Changes))
        {
            var i = net.FindIndex(x => x.Field == c.Field);
            if (i < 0) net.Add(c);
            else net[i] = net[i] with { To = c.To };
        }
        net.RemoveAll(c => Equivalent(c.From, c.To));
        return new ActivityGroup(
            run[^1].By, run[0].At, run[^1].At,
            run.Any(e => e.Action == "Created"),
            net,
            run.Where(e => e.Action is "Added a note" or "Deleted a note").ToList(),
            run);
    }
}
