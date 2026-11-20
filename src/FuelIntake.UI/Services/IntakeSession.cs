using FuelIntake.Core.Contacts;
using FuelIntake.Core.Hosting;
using FuelIntake.Core.Model;
using FuelIntake.Core.Storage;
using FuelIntake.Core.Validation;

namespace FuelIntake.UI.Services;

/// <summary>Which screen is showing.</summary>
public enum Screen { Clients, Client, Household, Application, IncomeExpenses, FuelElectric, Review, Check }

/// <summary>
/// The open client and everything the screens share: the record, calculated values, lists,
/// validation issues, and saving. One per window (desktop) or per browser connection (web).
/// Screens call <see cref="Changed"/> after every edit; the session recalculates and autosaves.
/// </summary>
public sealed class IntakeSession(IClientStore store, IListSource lists, ICurrentUser user, TimeProvider clock) : IDisposable
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

    public void Go(Screen screen)
    {
        Screen = screen;
        Notify();
    }

    public Task<IReadOnlyList<ClientFolderInfo>> ListClientsAsync() => store.ListClientsAsync();

    public async Task OpenAsync(ClientFolderInfo folder)
    {
        await CloseAsync();
        var record = await store.LoadAsync(folder.Id) ?? NewRecordFor(folder.DisplayName);
        Folder = folder;
        Record = record;
        HasUnsavedChanges = false;
        SaveError = null;
        LastSaved = folder.LastModified;
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
        Recalculate();
        Screen = Screen.Client;
        await SaveAsync();
    }

    public async Task CloseAsync()
    {
        if (Record is not null && HasUnsavedChanges) await SaveAsync();
        saveDelay?.Cancel();
        Folder = null; Record = null; Calc = null; Issues = [];
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
        try
        {
            await store.SaveAsync(Folder.Id, Record);
            HasUnsavedChanges = false;
            SaveError = null;
            LastSaved = clock.GetLocalNow();
        }
        catch (Exception ex)
        {
            SaveError = ex.Message;
        }
        Notify();
    }

    private void ScheduleSave()
    {
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
