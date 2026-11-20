using FuelIntake.Core.Activity;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Tests;

public class ActivityTests
{
    private static readonly StaffStamp Pat = new("Pat Example", "pexample@example.org");
    private static readonly StaffStamp Sam = new("Sam Placeholder", "splaceholder@example.org");
    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void Diff_FindsChangedFields_WithReadableNames()
    {
        var r = FakeIntake.Family();
        var before = ActivityLog.Snapshot(r);
        r.Client.Phone = "802-555-0177";
        r.Household[1].IncomePerMonth = 700m;
        r.IncomeExpenses.Expenses["Rent"].Bill = 950m;
        var changes = ActivityLog.Diff(before, ActivityLog.Snapshot(r));

        Assert.Contains(changes, c => c.Field == "Client › Phone" && c.From == "802-555-0199" && c.To == "802-555-0177");
        Assert.Contains(changes, c => c.Field == "Household member 2 › Income per month" && c.To == "700");
        Assert.Contains(changes, c => c.Field == "Income & Expenses › Expenses › Mortgage/Rent › Bill");
        Assert.Equal(3, changes.Count);
    }

    [Fact]
    public void Diff_MasksSsns()
    {
        var r = FakeIntake.Family();
        var before = ActivityLog.Snapshot(r);
        r.Household[0].Ssn = "000-55-9999";
        var c = Assert.Single(ActivityLog.Diff(before, ActivityLog.Snapshot(r)));
        Assert.Equal("***-**-9999", c.To);
        Assert.DoesNotContain("000-55", c.To);
    }

    [Fact]
    public void Diff_IgnoresHistoryAndStaffSnapshot()
    {
        var r = FakeIntake.Family();
        var before = ActivityLog.Snapshot(r);
        ActivityLog.RecordCreated(r, Pat, T0);
        r.Staff.StaffSnapshot = new StaffContact("Pat Example", "PE", null, null);
        Assert.Empty(ActivityLog.Diff(before, ActivityLog.Snapshot(r)));
    }

    [Fact]
    public void SamePersonSoon_Merges_OtherPerson_Adds()
    {
        var r = new IntakeRecord();
        ActivityLog.RecordCreated(r, Pat, T0);
        ActivityLog.Record(r, Pat, T0.AddMinutes(1), [new("Client › Phone", null, "1")]);
        ActivityLog.Record(r, Pat, T0.AddMinutes(2), [new("Client › Phone", "1", "2"), new("Client › Zip", null, "05819")]);
        ActivityLog.Record(r, Sam, T0.AddMinutes(3), [new("Client › Zip", "05819", "05851")]);

        Assert.Equal(3, r.Activity.Count);                      // created, Pat's edits, Sam's edit
        var pat = r.Activity[1];
        Assert.Equal(2, pat.Changes.Count);
        Assert.Equal("2", pat.Changes.Single(c => c.Field == "Client › Phone").To);
        Assert.Equal(Sam, r.Activity[2].By);
    }

    [Fact]
    public void ChangedBack_LeavesNoEntry()
    {
        var r = new IntakeRecord();
        ActivityLog.Record(r, Pat, T0, [new("Client › Phone", "1", "2")]);
        ActivityLog.Record(r, Pat, T0.AddMinutes(1), [new("Client › Phone", "2", "1")]);
        Assert.Empty(r.Activity);
    }

    [Fact]
    public void Snapshot_PrintsStaffOnAnotherComputer()
    {
        var r = FakeIntake.Family();
        r.Staff.StaffName = "Away Colleague";
        r.Staff.StaffSnapshot = new StaffContact("Away Colleague", "AC", "8025550188", "ac@example.org");
        var calc = new IntakeCalculations(r, FakeIntake.Context()); // their Staff list doesn't have this person
        Assert.Equal("AC", calc.StaffInitials);
        Assert.Equal("ac@example.org", calc.InvoiceEmail);
    }
}

public class NotesTests
{
    private static readonly StaffStamp Pat = new("Pat Example", "pexample@example.org");
    private static readonly StaffStamp Sam = new("Sam Placeholder", "splaceholder@example.org");
    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void AddNote_IsStampedAndLogged()
    {
        var r = new IntakeRecord();
        var n = ActivityLog.AddNote(r, Pat, T0, "  Called client, left voicemail  ");
        Assert.Equal("Called client, left voicemail", n.Text);
        Assert.Equal(Pat, n.By);
        Assert.Equal(T0, n.At);
        Assert.Equal("Added a note", Assert.Single(r.Activity).Action);
    }

    [Fact]
    public void DeleteNote_RemovesIt_ButTheLogKeepsWhoAndWhat()
    {
        var r = new IntakeRecord();
        var n = ActivityLog.AddNote(r, Pat, T0, "Vendor said Tuesday delivery");
        Assert.True(ActivityLog.DeleteNote(r, n.Id, Sam, T0.AddHours(1)));
        Assert.Empty(r.Notes);
        var log = r.Activity.Last();
        Assert.Equal("Deleted a note", log.Action);
        Assert.Equal(Sam, log.By);
        Assert.Contains("Pat Example", log.Changes[0].Field);
        Assert.Equal("Vendor said Tuesday delivery", log.Changes[0].From);
        Assert.False(ActivityLog.DeleteNote(r, n.Id, Sam, T0));
    }

    [Fact]
    public void Notes_AreNotDoubleLoggedByTheFieldDiff()
    {
        var r = FakeIntake.Family();
        var before = ActivityLog.Snapshot(r);
        ActivityLog.AddNote(r, Pat, T0, "hello");
        Assert.Empty(ActivityLog.Diff(before, ActivityLog.Snapshot(r)));
    }
}

public class FieldNoteTests
{
    [Fact]
    public void NoteAboutAField_KeepsTheReference_AndRoundTrips()
    {
        var r = new IntakeRecord();
        var about = new NoteFieldRef("Household", "household|member-2|ssn", "Household › Member 2 › SSN");
        ActivityLog.AddNote(r, new StaffStamp("Pat Example", null), DateTimeOffset.Now, "Card shows a different last digit", about);
        Assert.Equal("Note about Household › Member 2 › SSN", r.Activity.Last().Changes[0].Field);
        var back = FuelIntake.Core.Storage.IntakeJson.Deserialize(FuelIntake.Core.Storage.IntakeJson.Serialize(r));
        Assert.Equal(about, back.Notes[0].About);
    }
}
