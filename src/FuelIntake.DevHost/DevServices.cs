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
        services.AddSingleton(new HostInfo("0.0.0-dev (preview)", "Web (preview)", Environment.MachineName, contentRoot,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, "Browser"));
        services.AddSingleton<IEnumerable<CheckTarget>>([new CheckTarget("Temp folder", Path.GetTempPath())]);
        services.AddSingleton<IRecentClients, MemoryRecent>();
        services.AddSingleton<IDataFolder, PreviewDataFolder>();
        services.AddSingleton<FuelIntake.Core.TestData.ITestDataSets, FuelIntake.Core.TestData.MemoryTestDataSets>();
        services.AddScoped<IntakeSession>();
        services.AddScoped<FuelIntake.UI.Printing.PacketRenderer>();
        services.AddSingleton<IPdfPrinter, NoPdf>();
        services.AddSingleton<IFileOutput, NoFiles>();
    }

    /// <summary>The browser preview can't make PDFs; use Preview, or the desktop app.</summary>
    private sealed class NoPdf : IPdfPrinter
    {
        public Task<byte[]> PrintAsync(string html, CancellationToken ct = default) =>
            throw new NotSupportedException("PDF saving works in the desktop app. Use Preview here.");
    }

    private sealed class NoFiles : IFileOutput
    {
        public Task<string> SaveAsync(string clientId, string fileName, byte[] content, CancellationToken ct = default) => Task.FromResult(fileName);
        public Task OpenAsync(string location, CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>The preview keeps everything in memory; it shows the folder card but can't pick a folder.</summary>
    private sealed class PreviewDataFolder : IDataFolder
    {
        private const string Default = @"C:\Users\pexample\Documents\Client Files - Fuel 26-27";
        public DataFolderInfo Current => new(Default, Default, true, true, null);
        public bool CanPick => false;
        public Task<string?> PickAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
        public string? Problem(string path) => "The browser preview keeps data in memory.";
        public string? Warning(string path) => null;
        public Task UseAsync(string? path, bool copyLists, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class MemoryRecent : IRecentClients
    {
        private List<RecentClient> list = [];
        public Task<IReadOnlyList<RecentClient>> GetAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RecentClient>>(list);
        public Task TouchAsync(ClientFolderInfo client, DateTimeOffset at, CancellationToken ct = default)
        { list = RecentClientList.Touch(list, client, at); return Task.CompletedTask; }
    }

    private sealed class FakeUser : ICurrentUser
    {
        public string DisplayName => "pexample";
        public string? Email => "pexample@example.org";
    }

    private sealed class FakeLists : IListSource
    {
        private List<StaffMember> staff =
        [
            new StaffMember("Pat Example", "PE", "8025550100", "pexample@example.org"),
            new StaffMember("Sam Placeholder", null, "8025550101", "splaceholder@example.org"),
        ];
        private List<Vendor>? vendors;
        private AgencySettings settings = new()
        {
            DefaultInvoiceEmail = "fuel@example.org", DefaultInvoicePhone = "8025550000",
            GrantSources = [new() { Name = "Crisis Fuel", Code = "PREVIEW-01", MaxPerAssist = 1000m, Notes = "Preview example" }, new() { Name = "Example Electric Fund", ForFuel = false, MaxPerAssist = 400m }],
        };

        public Task SaveStaffAsync(IEnumerable<StaffMember> s, CancellationToken ct = default) { staff = s.ToList(); return Task.CompletedTask; }
        public Task SaveVendorsAsync(IEnumerable<Vendor> v, CancellationToken ct = default) { vendors = v.ToList(); return Task.CompletedTask; }
        public Task SaveSettingsAsync(AgencySettings s, CancellationToken ct = default) { settings = s; return Task.CompletedTask; }

        public Task<StaffDirectory> GetStaffAsync(CancellationToken ct = default) => Task.FromResult(new StaffDirectory(staff));

        public Task<VendorDirectory> GetVendorsAsync(CancellationToken ct = default) =>
            Task.FromResult(new VendorDirectory(vendors ??= VendorCsv.BuiltIn().Vendors.ToList()));

        public Task<AgencySettings> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(settings);
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

        public Task<DateTimeOffset?> GetLastSavedAsync(string clientId, CancellationToken ct = default) =>
            Task.FromResult<DateTimeOffset?>(files.TryGetValue(clientId, out var f) ? f.Saved : null);
    }
}
