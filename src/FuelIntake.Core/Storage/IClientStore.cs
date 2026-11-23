using FuelIntake.Core.Model;

namespace FuelIntake.Core.Storage;

/// <summary>A client folder as listed by the store.</summary>
/// <param name="Id">Store-specific id: a folder path (phase 1) or a SharePoint item id (phase 2).</param>
public sealed record ClientFolderInfo(string Id, string DisplayName, DateTimeOffset? LastModified, bool HasIntake);

/// <summary>The folder after a rename, the files that were renamed, and any that couldn't be.</summary>
public sealed record ClientRenameResult(ClientFolderInfo Folder, IReadOnlyList<FileRename> Renamed, IReadOnlyList<string> Problems);

/// <summary>
/// Where client folders and intake.json live. Phase 1: OneDrive-synced folders on disk.
/// Phase 2: SharePoint through Microsoft Graph, as the signed-in user.
/// </summary>
public interface IClientStore
{
    Task<IReadOnlyList<ClientFolderInfo>> ListClientsAsync(CancellationToken ct = default);

    /// <summary>Creates "Last, First" (or returns the existing folder) and copies any template files.</summary>
    Task<ClientFolderInfo> CreateClientAsync(string firstName, string lastName, CancellationToken ct = default);

    /// <summary>Loads intake.json, or returns null when the folder has none yet.</summary>
    Task<IntakeRecord?> LoadAsync(string clientId, CancellationToken ct = default);

    Task SaveAsync(string clientId, IntakeRecord record, CancellationToken ct = default);

    /// <summary>Names of the files in the client's folder (subfolders end in "\"), shown before deleting.</summary>
    Task<IReadOnlyList<string>> ListFilesAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// Deletes the client's folder and everything in it. Phase 1 moves it to the Windows Recycle Bin
    /// (and, for a synced SharePoint folder, SharePoint's recycle bin), so it can be restored.
    /// </summary>
    Task DeleteClientAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// Renames the client's folder to <paramref name="newFolderName"/> and the listed files inside it
    /// (used to correct a misspelled name). The folder is renamed first; if that fails nothing changes.
    /// A file that can't be renamed (open, already exists) is skipped and reported.
    /// </summary>
    Task<ClientRenameResult> RenameClientAsync(string clientId, string newFolderName, IReadOnlyList<FileRename> files, CancellationToken ct = default);

    /// <summary>When intake.json was last saved by anyone (to spot two people editing at once), or null if never.</summary>
    Task<DateTimeOffset?> GetLastSavedAsync(string clientId, CancellationToken ct = default);
}

/// <summary>Saves generated files (PDF packets) to a client folder and optionally opens them.</summary>
public interface IFileOutput
{
    /// <summary>Saves the file and returns where it went (a path or a URL).</summary>
    Task<string> SaveAsync(string clientId, string fileName, byte[] content, CancellationToken ct = default);

    /// <summary>Where a file in the client's folder is (a path or a URL), for opening or attaching it.</summary>
    string LocationOf(string clientId, string fileName);

    /// <summary>Opens a saved file in its usual app, or a client folder in File Explorer.</summary>
    Task OpenAsync(string location, CancellationToken ct = default);
}

/// <summary>The Staff list, vendor list and agency settings.</summary>
public interface IListSource
{
    Task<Contacts.StaffDirectory> GetStaffAsync(CancellationToken ct = default);
    Task<Contacts.VendorDirectory> GetVendorsAsync(CancellationToken ct = default);
    Task<AgencySettings> GetSettingsAsync(CancellationToken ct = default);

    Task SaveStaffAsync(IEnumerable<Contacts.StaffMember> staff, CancellationToken ct = default);
    Task SaveVendorsAsync(IEnumerable<Contacts.Vendor> vendors, CancellationToken ct = default);
    Task SaveSettingsAsync(AgencySettings settings, CancellationToken ct = default);

    /// <summary>Compliance standards (compliance.json); the defaults (Warmth, Crisis Fuel) until saved.</summary>
    Task<Compliance.ComplianceSettings> GetComplianceAsync(CancellationToken ct = default);
    Task SaveComplianceAsync(Compliance.ComplianceSettings compliance, CancellationToken ct = default);
}
