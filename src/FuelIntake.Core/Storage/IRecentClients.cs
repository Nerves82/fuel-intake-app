namespace FuelIntake.Core.Storage;

/// <summary>A client this staff member opened recently.</summary>
public sealed record RecentClient(string Id, string DisplayName, DateTimeOffset OpenedAt);

/// <summary>
/// Each staff member's own "recently opened" list (newest first). Phase 1: a file in the user's local
/// app data. Phase 2: per-user storage in Microsoft 365. Holds client folder names only, no other data.
/// </summary>
public interface IRecentClients
{
    Task<IReadOnlyList<RecentClient>> GetAsync(CancellationToken ct = default);

    /// <summary>Moves (or adds) the client to the top of the list.</summary>
    Task TouchAsync(ClientFolderInfo client, DateTimeOffset at, CancellationToken ct = default);
}

/// <summary>Keeps the list newest-first, de-duplicated, at most <see cref="Max"/> long.</summary>
public static class RecentClientList
{
    public const int Max = 10;

    public static List<RecentClient> Touch(IEnumerable<RecentClient> list, ClientFolderInfo client, DateTimeOffset at) =>
        list.Where(r => !string.Equals(r.Id, client.Id, StringComparison.OrdinalIgnoreCase))
            .Prepend(new RecentClient(client.Id, client.DisplayName, at))
            .OrderByDescending(r => r.OpenedAt)
            .Take(Max)
            .ToList();
}
