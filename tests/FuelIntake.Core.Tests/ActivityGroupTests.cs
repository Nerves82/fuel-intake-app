using FuelIntake.Core.Activity;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Tests;

public class ActivityGroupTests
{
    private static readonly StaffStamp Pat = new("Pat Example", "pexample@example.org");
    private static readonly StaffStamp Sam = new("Sam Placeholder", "splaceholder@example.org");
    private static readonly TimeZoneInfo Vermont = TimeZoneInfo.CreateCustomTimeZone("Test-5", TimeSpan.FromHours(-5), "Test", "Test");
    private static readonly DateTimeOffset Morning = new(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(-5));

    private static ActivityEntry Edit(StaffStamp by, DateTimeOffset at, params FieldChange[] changes) =>
        new() { By = by, At = at, Action = "Edited", Changes = changes.ToList() };

    [Fact]
    public void SamePersonSameDay_IsOneGroup_WithNetChanges()
    {
        var r = new IntakeRecord();
        ActivityLog.RecordCreated(r, Pat, Morning);
        r.Activity.Add(Edit(Pat, Morning.AddHours(1), new("Phone", null, "802-555-0100"), new("Zip", "05819", "05820")));
        ActivityLog.AddNote(r, Pat, Morning.AddHours(2), "called client");
        r.Activity.Add(Edit(Pat, Morning.AddHours(5), new("Phone", "802-555-0100", "802-555-0142"), new("Zip", "05820", "05819")));

        var g = Assert.Single(ActivityLog.Group(r.Activity, Vermont));
        Assert.True(g.Created);
        Assert.Equal(Morning, g.From);
        Assert.Equal(Morning.AddHours(5), g.To);
        var phone = Assert.Single(g.FieldChanges); // zip went back to where it started
        Assert.Equal(new FieldChange("Phone", null, "802-555-0142"), phone);
        Assert.Single(g.NoteEntries);
        Assert.Equal("created the intake, changed 1 field, added a note", g.Summary);
    }

    [Fact]
    public void AnotherPersonInBetween_OrANewDay_StartsANewGroup()
    {
        var entries = new List<ActivityEntry>
        {
            Edit(Pat, Morning, new("A", null, "1")),
            Edit(Sam, Morning.AddHours(1), new("B", null, "2")),
            Edit(Pat, Morning.AddHours(2), new("C", null, "3")),
            Edit(Pat, Morning.AddHours(3), new("D", null, "4")),
            Edit(Pat, Morning.AddDays(1), new("E", null, "5")),
        };
        var groups = ActivityLog.Group(entries, Vermont);
        Assert.Equal(["Pat Example", "Sam Placeholder", "Pat Example", "Pat Example"], groups.Select(g => g.By.Name));
        Assert.Equal(2, groups[2].FieldChanges.Count);
    }

    [Fact]
    public void DayBoundary_UsesLocalTime()
    {
        // 11:30 pm and 12:30 am local are different days even though they're the same UTC day
        var late = new DateTimeOffset(2026, 11, 20, 23, 30, 0, TimeSpan.FromHours(-5));
        var groups = ActivityLog.Group([Edit(Pat, late, new("A", null, "1")), Edit(Pat, late.AddHours(1), new("B", null, "2"))], Vermont);
        Assert.Equal(2, groups.Count);
    }
}
