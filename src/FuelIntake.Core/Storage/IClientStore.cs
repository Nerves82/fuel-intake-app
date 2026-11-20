using FuelIntake.Core.Model;

namespace FuelIntake.Core.Storage;

/// <summary>A client folder as listed by the store.</summary>
/// <param name="Id">Store-specific id: a folder path (phase 1) or a SharePoint item id (phase 2).</param>
public sealed record ClientFolderInfo(string Id, string DisplayName, DateTimeOffset? LastModified, bool HasIntake);

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

    /// <summary>When intake.json was last saved by anyone (to spot two people editing at once), or null if never.</summary>
    Task<DateTimeOffset?> GetLastSavedAsync(string clientId, CancellationToken ct = default);
}

/// <summary>Saves generated files (PDF packets) to a client folder and optionally opens them.</summary>
public interface IFileOutput
{
    /// <summary>Saves the file and returns where it went (a path or a URL).</summary>
    Task<string> SaveAsync(string clientId, string fileName, byte[] content, CancellationToken ct = default);

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
}
