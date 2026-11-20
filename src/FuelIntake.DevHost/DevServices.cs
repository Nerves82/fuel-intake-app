using System.Collections.Concurrent;
using FuelIntake.Core.Contacts;
using FuelIntake.Core.Hosting;
using FuelIntake.Core.Model;
using FuelIntake.Core.Storage;
using FuelIntake.UI.Services;

namespace FuelIntake.DevHost;

/// <summary>Fake services for the browser preview. Data lives in memory only and is made up.</summary>
public static class DevServices
{
    public static void Add(IServiceCollection services, string contentRoot)
    {
        services.AddSingleton<IClientStore, MemoryClientStore>();
        services.AddSingleton<IListSource, FakeLists>();
        services.AddSingleton<ICurrentUser>(new FakeUser());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new HostInfo("dev preview", "Web (preview)", Environment.MachineName, contentRoot,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, "Browser"));
        services.AddSingleton<IEnumerable<CheckTarget>>([new CheckTarget("Temp folder", Path.GetTempPath())]);
        services.AddScoped<IntakeSession>();
    }

    private sealed class FakeUser : ICurrentUser
    {
        public string DisplayName => "pexample";
        public string? Email => "pexample@example.org";
    }

    private sealed class FakeLists : IListSource
    {
        public Task<StaffDirectory> GetStaffAsync(CancellationToken ct = default) => Task.FromResult(new StaffDirectory([
            new StaffMember("Pat Example", "PE", "8025550100", "pexample@example.org"),
            new StaffMember("Sam Placeholder", null, "8025550101", "splaceholder@example.org"),
        ]));

        public Task<VendorDirectory> GetVendorsAsync(CancellationToken ct = default) => Task.FromResult(new VendorDirectory([
            new Vendor("Example Oil", "St. Johnsbury", "orders@exampleoil.test", "802-555-0111", "Oil, Kerosene", "100 gal", "$150", "$85", "Call before noon"),
            new Vendor("Example Oil", "Lancaster NH", "nh@exampleoil.test", "603-555-0112", "Oil"),
            new Vendor("Sample Propane", "Lyndonville", "gas@samplepropane.test", "802-555-0113", "Propane", "150 gal"),
            new Vendor("Pretend Power", null, "billing@pretendpower.test\nhelp@pretendpower.test", "800-555-0114", "Electric"),
            new Vendor("Mock Municipal Electric", "Lyndonville", "mme@example.test", "802-555-0115", "Electric"),
        ]));

        public Task<AgencySettings> GetSettingsAsync(CancellationToken ct = default) =>
            Task.FromResult(new AgencySettings { DefaultInvoiceEmail = "fuel@example.org", DefaultInvoicePhone = "8025550000" });
    }

    private sealed class MemoryClientStore : IClientStore
    {
        private readonly ConcurrentDictionary<string, (string Json, DateTimeOffset Saved)> files = new();
        private readonly ConcurrentDictionary<string, DateTimeOffset> folders = new();

        public Task<IReadOnlyList<ClientFolderInfo>> ListClientsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ClientFolderInfo>>(folders.OrderBy(f => f.Key)
                .Select(f => new ClientFolderInfo(f.Key, f.Key, files.TryGetValue(f.Key, out var x) ? x.Saved : f.Value, files.ContainsKey(f.Key)))
                .ToList());

        public Task<ClientFolderInfo> CreateClientAsync(string firstName, string lastName, CancellationToken ct = default)
        {
            var name = ClientFolders.FolderName(firstName, lastName);
            var when = folders.GetOrAdd(name, DateTimeOffset.Now);
            return Task.FromResult(new ClientFolderInfo(name, name, when, files.ContainsKey(name)));
        }

        public Task<IntakeRecord?> LoadAsync(string clientId, CancellationToken ct = default) =>
            Task.FromResult(files.TryGetValue(clientId, out var f) ? IntakeJson.Deserialize(f.Json) : null);

        public Task SaveAsync(string clientId, IntakeRecord record, CancellationToken ct = default)
        {
            files[clientId] = (IntakeJson.Serialize(record), DateTimeOffset.Now);
            return Task.CompletedTask;
        }
    }
}
