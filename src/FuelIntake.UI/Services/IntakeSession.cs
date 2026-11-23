using FuelIntake.Core.Activity;
using FuelIntake.Core.Contacts;
using FuelIntake.Core.Hosting;
using FuelIntake.Core.Model;
using FuelIntake.Core.Storage;
using FuelIntake.Core.Validation;

namespace FuelIntake.UI.Services;

/// <summary>Which screen is showing.</summary>
public enum Screen { Clients, Client, Household, Application, IncomeExpenses, FuelElectric, Review, Print, Check, Settings, Verify }

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
    public void ShowNotice(string text) { Notice = text; Notify(); }

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
        Compliance = await lists.GetComplianceAsync();
    }

    /// <summary>Agency-wide compliance standards (Warmth, Crisis Fuel…).</summary>
    public FuelIntake.Core.Compliance.ComplianceSettings Compliance { get; private set; } = FuelIntake.Core.Compliance.ComplianceSettings.Defaults();

    /// <summary>Unlocked with the passcode for this run of the app.</summary>
    public bool ComplianceUnlocked { get; set; }

    public async Task SaveComplianceAsync(FuelIntake.Core.Compliance.ComplianceSettings compliance)
    {
        await lists.SaveComplianceAsync(compliance);
        Compliance = compliance;
        Recalculate();
        Notify();
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

    /// <summary>The field the next note will be about (set by a field's "add note" button).</summary>
    public NoteFieldRef? PendingNoteRef { get; private set; }

    /// <summary>A field to scroll to and highlight after the next render.</summary>
    public NoteFieldRef? PendingReveal { get; set; }

    /// <summary>Put the cursor in the note box after the next render.</summary>
    public bool FocusNoteBox { get; set; }

    public void StartNoteAbout(NoteFieldRef r)
    {
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

    // ---- compliance badges in the clients list

    private readonly Dictionary<string, (DateTimeOffset? Modified, IntakeRecord? Record)> badgeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim badgeGate = new(4);

    /// <summary>
    /// The compliance badge for a client in the list. Reads the client's intake.json once and keeps it until
    /// the file changes; works out the badge from the current compliance settings each time. Null when no
    /// standard applies or the file can't be read.
    /// </summary>
    public async Task<FuelIntake.Core.Compliance.ComplianceBadge?> BadgeAsync(ClientFolderInfo c)
    {
        if (!c.HasIntake) return null;
        if (Folder is not null && Record is not null && string.Equals(Folder.Id, c.Id, StringComparison.OrdinalIgnoreCase))
            return FuelIntake.Core.Compliance.ComplianceBadge.For(Record, Compliance);
        (DateTimeOffset? Modified, IntakeRecord? Record) hit;
        lock (badgeCache)
            if (badgeCache.TryGetValue(c.Id, out hit) && hit.Modified == c.LastModified)
                return FuelIntake.Core.Compliance.ComplianceBadge.For(hit.Record, Compliance);
        IntakeRecord? r = null;
        await badgeGate.WaitAsync();
        try { r = await store.LoadAsync(c.Id); }
        catch { /* unreadable: no badge (opening it shows the problem) */ }
        finally { badgeGate.Release(); }
        lock (badgeCache) badgeCache[c.Id] = (c.LastModified, r);
        return FuelIntake.Core.Compliance.ComplianceBadge.For(r, Compliance);
    }

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
    /// <param name="complianceStandard">Starts the new record under this standard (blank = none, use the grant source's).
    /// An existing record's standard is only filled in when it has none.</param>
    public async Task CreateAsync(string first, string last, Func<string, string, DateOnly, IntakeRecord>? sample = null, string? complianceStandard = null)
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
        if (FuelIntake.Core.Compliance.ComplianceBadge.ForNewClient(complianceStandard, Compliance) is { } standard
            && string.IsNullOrWhiteSpace(record.Application.ComplianceStandard))
            record.Application.ComplianceStandard = standard;
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

    // ---- correcting a misspelled client name

    /// <summary>Hides the rename banner for the client that's open (until it's closed).</summary>
    public bool RenameDismissed { get; set; }

    /// <summary>
    /// The folder name the name on tab 1 calls for, when it differs from the folder's name (the name was
    /// corrected). Null while typing, when the names are blank, or when the folder isn't named "Last, First".
    /// </summary>
    public string? RenameSuggestion
    {
        get
        {
            if (Folder is null || Record is null || HasUnsavedChanges) return null;
            var first = Record.Client.FirstName?.Trim(); var last = Record.Client.LastName?.Trim();
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(last)) return null;
            var sep = Settings.FolderNameSeparator;
            if (ClientRename.ParseFolderName(Folder.DisplayName, sep) is not { } old) return null;
            var want = ClientRename.FolderNameFor(first, last, old.IsTest, sep);
            return want == Folder.DisplayName ? null : want;
        }
    }

    /// <summary>What renaming to the name on tab 1 would change (folder and files).</summary>
    public async Task<ClientRenamePlan?> PlanRenameAsync()
    {
        if (Folder is null || Record is null) return null;
        var files = await store.ListFilesAsync(Folder.Id);
        var plan = ClientRename.Plan(Folder.DisplayName, Record.Client.FirstName ?? "", Record.Client.LastName ?? "",
            files.Where(f => !f.EndsWith('\\')), Settings.FolderNameSeparator);
        if (plan is null || ClientRename.ParseFolderName(Folder.DisplayName, Settings.FolderNameSeparator) is not { } old) return plan;
        return plan with { Members = ClientRename.MatchingMembers(Record, old.Last, Record.Client.LastName ?? "") };
    }

    /// <summary>
    /// Renames the folder and the chosen files, logs it in the activity, and saves. Throws if the folder
    /// couldn't be renamed (nothing changed then).
    /// </summary>
    public async Task<ClientRenameResult> RenameClientAsync(ClientRenamePlan plan, IReadOnlyList<FileRename> files,
        IReadOnlyList<MemberRename>? members = null)
    {
        if (Folder is null || Record is null) throw new InvalidOperationException("No client is open.");
        if (HasUnsavedChanges) await SaveAsync();
        if (SaveConflict || SaveError is not null) throw new InvalidOperationException("Save the client first: " + SaveError);
        ClientRenameResult result;
        var membersRenamed = 0;
        saveDelay?.Cancel();
        await saveLock.WaitAsync();
        try
        {
            result = await store.RenameClientAsync(Folder.Id, plan.FolderTo, files);
            Folder = result.Folder;
            ActivityLog.RecordRename(Record, Me, clock.GetLocalNow(), plan.FolderFrom, plan.FolderTo, result.Renamed);
            // household members' last names: logged as ordinary edits when saved below
            membersRenamed = ClientRename.ApplyMembers(Record, members ?? []);
            HasUnsavedChanges = true;
        }
        finally
        {
            saveLock.Release();
        }
        Recalculate();
        await SaveAsync();
        await RememberAsync(Folder);
        var who = membersRenamed > 0 ? $" Updated the last name of {membersRenamed} household member{(membersRenamed == 1 ? "" : "s")}." : "";
        Notice = (result.Problems.Count == 0
            ? $"Renamed the folder to {plan.FolderTo}" + (result.Renamed.Count > 0 ? $" and {result.Renamed.Count} file{(result.Renamed.Count == 1 ? "" : "s")}." : ".")
            : $"Renamed the folder to {plan.FolderTo}. {result.Problems.Count} file{(result.Problems.Count == 1 ? "" : "s")} couldn't be renamed: " +
              string.Join("; ", result.Problems)) + who;
        Notify();
        return result;
    }

    // ---- paper applications

    /// <summary>The pending paper application on the approval page (Screen.Verify).</summary>
    public string? PendingImportId { get; private set; }

    public void OpenPendingImport(string id)
    {
        PendingImportId = id;
        Screen = Screen.Verify;
        Notify();
    }

    public void LeavePendingImport()
    {
        PendingImportId = null;
        Screen = Screen.Clients;
        Notify();
    }

    public const string FromPaperAction = "Created from a paper application";

    /// <summary>
    /// Makes the client from a fully checked paper application: a new folder under the confirmed name
    /// (never an existing client's), the intake from the confirmed values, and an activity entry saying
    /// who confirmed how many fields. Opens the new client on the Review tab.
    /// </summary>
    public async Task CreateFromPaperAsync(FuelIntake.Core.Import.PaperImport import)
    {
        var record = FuelIntake.Core.Import.PaperImportRules.Build(import, Today); // throws unless everything is checked
        var first = record.Client.FirstName!.Trim();
        var last = record.Client.LastName!.Trim();
        var folderName = ClientFolders.FolderName(first, last, Settings.FolderNameSeparator);
        var existing = await store.ListClientsAsync();
        if (existing.Any(c => string.Equals(c.DisplayName, folderName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"There's already a client folder named \"{folderName}\". If it's the same person, open that client instead; otherwise correct the name.");

        await CloseAsync();
        var folder = await store.CreateClientAsync(first, last);
        if (await store.LoadAsync(folder.Id) is not null)
            throw new InvalidOperationException($"\"{folder.DisplayName}\" already has an intake. Open that client instead.");
        StampStaff(record);
        Folder = folder;
        Record = record;
        SaveConflict = false; SaveError = null; PendingImportId = null;
        loadedVersion = await store.GetLastSavedAsync(folder.Id);
        baseline = ActivityLog.Snapshot(record);
        var now = clock.GetLocalNow();
        ActivityLog.RecordCreated(record, Me, now);
        record.Activity.Add(new ActivityEntry
        {
            At = now, By = Me, Action = FromPaperAction,
            Changes = import.Fields.Where(f => f.ConfirmedBy is not null).GroupBy(f => f.ConfirmedBy!.Name)
                .Select(g => new FieldChange("Fields checked", null, $"{g.Key}: {g.Count()}")).ToList(),
        });
        HasUnsavedChanges = true;
        await RememberAsync(folder);
        Recalculate();
        Screen = Screen.Review;
        await SaveAsync();
    }

    public Task<IReadOnlyList<string>> ClientFilesAsync() =>
        Folder is null ? Task.FromResult<IReadOnlyList<string>>([]) : store.ListFilesAsync(Folder.Id);

    /// <summary>
    /// Deletes the open client's folder (to the Recycle Bin on the desktop) without saving first,
    /// then goes back to the client list with a notice.
    /// </summary>
    public async Task DeleteCurrentClientAsync()
    {
        if (Folder is null) return;
        var folder = Folder;
        saveDelay?.Cancel();
        await saveLock.WaitAsync(); // let a save in progress finish, and stop new ones
        try
        {
            await store.DeleteClientAsync(folder.Id);
            Folder = null; Record = null; Calc = null; Issues = [];
            HasUnsavedChanges = false; SaveConflict = false; SaveError = null;
        }
        finally
        {
            saveLock.Release();
        }
        Notice = $"Deleted {folder.DisplayName}. The folder is in the Recycle Bin if you need it back.";
        Screen = Screen.Clients;
        Notify();
    }

    public async Task CloseAsync()
    {
        if (Record is not null && HasUnsavedChanges && !SaveConflict) await SaveAsync();
        saveDelay?.Cancel();
        Folder = null; Record = null; Calc = null; Issues = [];
        SaveConflict = false; SaveError = null; Notice = null; RenameDismissed = false;
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
        Calc = new IntakeCalculations(Record, new CalculationContext(Settings, Staff, Vendors, Today, Compliance));
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
