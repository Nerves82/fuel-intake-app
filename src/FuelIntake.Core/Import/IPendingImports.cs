namespace FuelIntake.Core.Import;

/// <summary>A paper application waiting to be checked, for the client list.</summary>
public sealed record PendingImportInfo(string Id, string DisplayName, Model.StaffStamp? CreatedBy, DateTimeOffset CreatedAt, int Confirmed, int Total);

/// <summary>
/// Holding folders for paper applications ("_PENDING …" in the client files folder): the scans and
/// import.json with every field and who confirmed it. Shared like client folders, so any staff member
/// can carry on. Phase 2: a SharePoint library folder.
/// </summary>
public interface IPendingImports
{
    Task<IReadOnlyList<PendingImportInfo>> ListAsync(CancellationToken ct = default);

    /// <summary>Makes a holding folder, copies the scans into it (from where staff picked them) and saves the import.</summary>
    Task<string> CreateAsync(PaperImport import, IReadOnlyList<string> scanLocations, CancellationToken ct = default);

    Task<PaperImport> GetAsync(string id, CancellationToken ct = default);
    Task SaveAsync(string id, PaperImport import, CancellationToken ct = default);

    /// <summary>Where a scan is on this computer (for reading it), or null if it can't be read from here.</summary>
    string? ScanLocation(string id, string scanFile);

    /// <summary>A link the app can show a scan with (PDFs and images open in the built-in viewer).</summary>
    string? ViewUrl(string id, string scanFile);

    /// <summary>Moves the scans into the new client's folder and removes the holding folder.</summary>
    Task FinishAsync(string id, string clientId, CancellationToken ct = default);

    /// <summary>
    /// Throws the application away: its checking work and the holding folder with the app's copies of the scans
    /// (Recycle Bin). The original files staff picked are never touched. Returns null (kept for a future location).
    /// </summary>
    Task<string?> DiscardAsync(string id, CancellationToken ct = default);
}

/// <summary>Lets staff pick files (the scans). Desktop: the Windows Open dialog.</summary>
public interface IFilePicker
{
    bool CanPick { get; }
    Task<IReadOnlyList<string>> PickScansAsync(CancellationToken ct = default);
}
