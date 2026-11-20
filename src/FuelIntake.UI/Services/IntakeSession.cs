using FuelIntake.Core.Activity;
using FuelIntake.Core.Contacts;
using FuelIntake.Core.Hosting;
using FuelIntake.Core.Model;
using FuelIntake.Core.Storage;
using FuelIntake.Core.Validation;

namespace FuelIntake.UI.Services;

/// <summary>Which screen is showing.</summary>
public enum Screen { Clients, Client, Household, Application, IncomeExpenses, FuelElectric, Review, Print, Check, Settings }

/// <summary>
/// The open client and everything the screens share: the record, calculated values, lists,
/// validation issues, and saving. One per window (desktop) or per browser connection (web).
/// Screens call <see cref="Changed"/> after every edit; the session recalculates and autosaves.
/// </summary>
public sealed class IntakeSession(IClientStore store, IListSource lists, ICurrentUser user, TimeProvider clock, IRecentClients recent) : IDisposable
{
    private CancellationTokenSource? saveDelay;

    public event Action? StateChanged;

    public Screen Screen { get; private set; } = Screen.Clients;
    public ClientFolderInfo? Folder { get; private set; }
    public IntakeRecord? Record { get; private set; }
    public IntakeCalculations? Calc { get; private set; }
    public IReadOnlyList<ValidationIssue> Issues { get; private set; } = [];

    public AgencySettings Settings { get; private set; } = new();
    public StaffDirectory Staff { get; private set; } = StaffDirectory.Empty;
    public VendorDirectory Vendors { get; private set; } = VendorDirectory.Empty;

    /// <summary>A one-off message for the user (e.g. a staff member was added to the Staff list).</summary>
    public string? Notice { get; private set; }
    public void DismissNotice() { Notice = null; Notify(); }

    /// <summary>Another computer saved this client after we opened it; we stop saving to avoid overwriting their work.</summary>
    public bool SaveConflict { get; private set; }

    private string baseline = "";           // record as last saved, for the activity log
    private DateTimeOffset? loadedVersion;  // intake.json time when we last read or wrote it

    public bool HasUnsavedChanges { get; private set; }
    public DateTimeOffset? LastSaved { get; private set; }
    public string? SaveError { get; private set; }

    public DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
    public ICurrentUser User => user;

    /// <summary>Loads staff, vendors and settings. Call once at start-up.</summary>
    public async Task InitializeAsync()
    {
        Settings = await lists.GetSettingsAsync();
        Staff = await lists.GetStaffAsync();
        Vendors = await lists.GetVendorsAsync();
    }

    /// <summary>Which sub-tab the settings page opens on (0 staff, 1 vendors, 2 agency, 3 data folder).</summary>
    public int SettingsTab { get; set; }

    /// <summary>Opens the settings page on the data folder tab.</summary>
    public void GoToDataFolder()
    {
        SettingsTab = 3;
        Go(Screen.Settings);
    }

    /// <summary>
    /// Switches the client files folder: saves and closes the open client first (it belongs to the old
    /// folder), runs <paramref name="change"/>, then reloads the lists from the new folder.
    /// </summary>
    public async Task SwitchDataFolderAsync(Func<Task> change)
    {
        var screen = Screen;
        if (Record is not null) await CloseAsync();
        await change();
        await InitializeAsync();
        Screen = screen == Screen.Settings ? Screen.Settings : Screen.Clients;
        Notify();
    }

    public async Task SaveStaffAsync(IEnumerable<StaffMember> staff)
    {
        await lists.SaveStaffAsync(staff);
        await ReloadListsAsync();
    }

    public async Task SaveVendorsAsync(IEnumerable<Vendor> vendors)
    {
        await lists.SaveVendorsAsync(vendors);
        await ReloadListsAsync();
    }

    public async Task SaveSettingsAsync(AgencySettings settings)
    {
        await lists.SaveSettingsAsync(settings);
        await ReloadListsAsync();
    }

    private async Task ReloadListsAsync()
    {
        await InitializeAsync();
        Recalculate();
        Notify();
    }

    /// <summary>The right-click menu on a field, if open.</summary>
    public (NoteFieldRef Ref, double X, double Y)? FieldMenu { get; private set; }

    /// <summary>The field the next note will be about (set by right-click → Add note).</summary>
    public NoteFieldRef? PendingNoteRef { get; private set; }

    /// <summary>A field to scroll to and highlight after the next render.</summary>
    public NoteFieldRef? PendingReveal { get; set; }

    /// <summary>Put the cursor in the note box after the next render.</summary>
    public bool FocusNoteBox { get; set; }

    public void ShowFieldMenu(NoteFieldRef r, double x, double y) { FieldMenu = (r, x, y); Notify(); }

    public void CloseFieldMenu() { if (FieldMenu is null) return; FieldMenu = null; Notify(); }

    public void StartNoteAbout(NoteFieldRef r)
    {
        FieldMenu = null;
        PendingNoteRef = r;
        NotesOpen = true;
        FocusNoteBox = true;
        Notify();
    }

    public void ClearPendingNoteRef() { PendingNoteRef = null; Notify(); }

    /// <summary>Go to the field a note is about: switch tab, then scroll to it and highlight it.</summary>
    public void RevealField(NoteFieldRef r)
    {
        if (Enum.TryParse<Screen>(r.Screen, out var s)) Screen = s;
        PendingReveal = r;
        Notify();
    }

    /// <summary>Notes to highlight in the notes panel after the next render (set by a field's note marker).</summary>
    public IReadOnlyList<string>? HighlightNoteIds { get; set; }

    /// <summary>Opens the notes panel and highlights the notes about one field.</summary>
    public void ShowNotesAbout(NoteFieldRef r)
    {
        if (Record is null) return;
        var ids = ActivityLog.NotesAbout(Record, r.Screen, r.Key).Select(n => n.Id).ToList();
        if (ids.Count == 0) return;
        FieldMenu = null;
        NotesOpen = true;
        HighlightNoteIds = ids;
        Notify();
    }

    /// <summary>The notes panel stays open (or closed) while moving between tabs.</summary>
    public bool NotesOpen { get; private set; }

    public void ToggleNotes() { NotesOpen = !NotesOpen; Notify(); }

    public async Task AddNoteAsync(string text)
    {
        if (Record is null || string.IsNullOrWhiteSpace(text)) return;
        ActivityLog.AddNote(Record, Me, clock.GetLocalNow(), text, PendingNoteRef);
        PendingNoteRef = null;
        HasUnsavedChanges = true;
        Notify();
        await SaveAsync();
    }

    public async Task DeleteNoteAsync(string noteId)
    {
        if (Record is null || !ActivityLog.DeleteNote(Record, noteId, Me, clock.GetLocalNow())) return;
        HasUnsavedChanges = true;
        Notify();
        await SaveAsync();
    }

    public void Go(Screen screen)
    {
        Screen = screen;
        Notify();
    }

    public Task<IReadOnlyList<ClientFolderInfo>> ListClientsAsync() => store.ListClientsAsync();

    public Task<IReadOnlyList<RecentClient>> RecentClientsAsync() => recent.GetAsync();

    private async Task RememberAsync(ClientFolderInfo folder)
    {
        try { await recent.TouchAsync(folder, clock.GetLocalNow()); }
        catch { /* the recent list is a convenience; never block opening a client */ }
    }

    public async Task OpenAsync(ClientFolderInfo folder)
    {
        await CloseAsync();
        var loaded = await store.LoadAsync(folder.Id);
        var record = loaded ?? NewRecordFor(folder.DisplayName);
        Folder = folder;
        Record = record;
        HasUnsavedChanges = false;
        SaveError = null;
        SaveConflict = false;
        LastSaved = folder.LastModified;
        loadedVersion = await store.GetLastSavedAsync(folder.Id);
        baseline = ActivityLog.Snapshot(record);
        if (loaded is null) ActivityLog.RecordCreated(record, Me, clock.GetLocalNow());
        await AddSnapshotStaffAsync(record);
        await RememberAsync(folder);
        Recalculate();
        Screen = Screen.Client;
        Notify();
    }

    /// <summary>Creates (or reuses) the client's folder and opens it with a fresh record.</summary>
    public async Task CreateAsync(string first, string last, Func<string, string, DateOnly, IntakeRecord>? sample = null)
    {
        await CloseAsync();
        var isTest = sample is not null;
        var lastForFolder = isTest ? ClientFolders.TestPrefix + last.Trim() : last.Trim();
        var folder = await store.CreateClientAsync(first.Trim(), lastForFolder);
        var existing = await store.LoadAsync(folder.Id);
        if (existing is not null && existing.IsTestData != isTest)
            throw new InvalidOperationException("That folder already holds a different kind of record (test vs real).");

        var record = existing ?? (isTest ? sample!(first.Trim(), last.Trim(), Today) : NewRecord(first.Trim(), last.Trim()));
        if (existing is null) StampStaff(record);
        Folder = folder;
        Record = record;
        SaveConflict = false;
        SaveError = null;
        loadedVersion = await store.GetLastSavedAsync(folder.Id);
        baseline = ActivityLog.Snapshot(record);
        if (existing is null) ActivityLog.RecordCreated(record, Me, clock.GetLocalNow());
        await RememberAsync(folder);
        Recalculate();
        Screen = Screen.Client;
        await SaveAsync();
    }

    public async Task CloseAsync()
    {
        if (Record is not null && HasUnsavedChanges && !SaveConflict) await SaveAsync();
        saveDelay?.Cancel();
        Folder = null; Record = null; Calc = null; Issues = [];
        SaveConflict = false; SaveError = null; Notice = null;
        Screen = Screen.Clients;
        Notify();
    }

    /// <summary>Call after any edit: recalculates, refreshes the screens and saves after a short pause.</summary>
    public void Changed()
    {
        if (Record is null) return;
        HasUnsavedChanges = true;
        Recalculate();
        Notify();
        ScheduleSave();
    }

    public async Task SaveAsync()
    {
        if (Record is null || Folder is null) return;
        saveDelay?.Cancel();
        await saveLock.WaitAsync();
        try
        {
            var onDisk = await store.GetLastSavedAsync(Folder.Id);
            if (onDisk is not null && loadedVersion is not null && onDisk > loadedVersion.Value.AddSeconds(1))
            {
                SaveConflict = true;
                SaveError = "Someone else saved this client after you opened it. Your latest changes were NOT saved. " +
                            "Note them down, close the client and open it again to see their changes.";
                Notify();
                return;
            }
            UpdateStaffSnapshot(Record);
            var now = clock.GetLocalNow();
            ActivityLog.Record(Record, Me, now, ActivityLog.Diff(baseline, ActivityLog.Snapshot(Record)));
            await store.SaveAsync(Folder.Id, Record);
            baseline = ActivityLog.Snapshot(Record);
            loadedVersion = await store.GetLastSavedAsync(Folder.Id);
            HasUnsavedChanges = false;
            SaveError = null;
            LastSaved = now;
        }
        catch (Exception ex)
        {
            SaveError = ex.Message;
        }
        finally
        {
            saveLock.Release();
        }
        Notify();
    }

    private readonly SemaphoreSlim saveLock = new(1, 1);

    /// <summary>The signed-in person, named as on the Staff list when they're on it.</summary>
    public StaffStamp Me =>
        Staff.FindByEmail(user.Email) is { } m ? new StaffStamp(m.Name, user.Email) : new StaffStamp(user.DisplayName, user.Email);

    /// <summary>Keeps a copy of the assigned staff member's details in the client file.</summary>
    private void UpdateStaffSnapshot(IntakeRecord r)
    {
        if (Staff.FindByName(r.Staff.StaffName) is { } m)
            r.Staff.StaffSnapshot = new StaffContact(m.Name, m.Initials, m.Phone, m.Email);
        else if (r.Staff.StaffSnapshot is { } s && !string.Equals(s.Name, r.Staff.StaffName?.Trim(), StringComparison.OrdinalIgnoreCase))
            r.Staff.StaffSnapshot = null; // staff changed to someone not on the list
    }

    /// <summary>When a client was filled out by someone not on this computer's Staff list, add them.</summary>
    private async Task AddSnapshotStaffAsync(IntakeRecord r)
    {
        if (r.Staff.StaffSnapshot is not { } s || string.IsNullOrWhiteSpace(s.Name) || Staff.FindByName(s.Name) is not null) return;
        try
        {
            await lists.SaveStaffAsync(Staff.Members.Append(new StaffMember(s.Name, s.Initials, s.Phone, s.Email)));
            Staff = await lists.GetStaffAsync();
            Notice = $"Added {s.Name} to your Staff list (from this client's file).";
        }
        catch (Exception ex)
        {
            Notice = $"This client was filled out by {s.Name}, who isn't on your Staff list (couldn't add them: {ex.Message}).";
        }
    }

    private void ScheduleSave()
    {
        if (SaveConflict) return;
        saveDelay?.Cancel();
        var cts = saveDelay = new CancellationTokenSource();
        _ = Task.Delay(TimeSpan.FromSeconds(2), cts.Token).ContinueWith(async t =>
        {
            if (!t.IsCanceled) await SaveAsync();
        }, TaskScheduler.Default);
    }

    private void Recalculate()
    {
        if (Record is null) return;
        Calc = new IntakeCalculations(Record, new CalculationContext(Settings, Staff, Vendors, Today));
        Issues = IntakeValidator.Validate(Record, Calc, Today);
    }

    private IntakeRecord NewRecordFor(string folderName)
    {
        // "Last, First" → names; anything else → last name only
        var isTest = folderName.StartsWith(ClientFolders.TestPrefix, StringComparison.Ordinal);
        var name = isTest ? folderName[ClientFolders.TestPrefix.Length..] : folderName;
        var parts = name.Split(Settings.FolderNameSeparator, 2);
        var r = parts.Length == 2 ? NewRecord(parts[1], parts[0]) : NewRecord("", name);
        r.IsTestData = isTest;
        StampStaff(r);
        return r;
    }

    private IntakeRecord NewRecord(string first, string last)
    {
        var r = new IntakeRecord();
        r.Client.FirstName = first;
        r.Client.LastName = last;
        r.Staff.AppDate = Today;
        r.Household.Add(new HouseholdMember());
        return r;
    }

    /// <summary>Fills the staff name from the signed-in user, if they're on the Staff list.</summary>
    private void StampStaff(IntakeRecord r)
    {
        if (!string.IsNullOrWhiteSpace(r.Staff.StaffName)) return;
        var me = Staff.FindByEmail(user.Email) ?? Staff.FindByName(user.DisplayName);
        if (me is not null) r.Staff.StaffName = me.Name;
    }

    private void Notify() => StateChanged?.Invoke();

    public void Dispose() => saveDelay?.Cancel();
}
