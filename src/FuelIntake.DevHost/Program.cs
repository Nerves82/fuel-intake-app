using FuelIntake.DevHost;

var builder = WebApplication.CreateBuilder(args);
var components = builder.Services.AddRazorComponents();
#if !OFFLINE_PREVIEW
components.AddInteractiveServerComponents();
#endif
DevServices.Add(builder.Services, builder.Environment.ContentRootPath);

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
var endpoints = app.MapRazorComponents<HostPage>();
#if !OFFLINE_PREVIEW
endpoints.AddInteractiveServerRenderMode();
#endif
// Dev only: /print-html?sample=0&forms=FuelOrder,Access returns the printable HTML (fake data) for checking layouts.
app.MapGet("/print-html", async (int? sample, string? forms, IServiceProvider services, FuelIntake.Core.Storage.IListSource lists) =>
{
    var today = DateOnly.FromDateTime(DateTime.Now);
    var s = FuelIntake.Core.TestData.SampleIntakes.All[Math.Clamp(sample ?? 0, 0, FuelIntake.Core.TestData.SampleIntakes.All.Count - 1)];
    var record = s.Build("Testy", "Sampleton", today);
    var vendors = await lists.GetVendorsAsync();
    record.FuelElectric.Vendor = vendors.FuelVendors.First().DisplayName;
    record.FuelElectric.ElectricProvider = vendors.ElectricProviders.First().DisplayName;
    record.Staff.StaffName = "Pat Example";
    var ctx = new FuelIntake.Core.Model.CalculationContext(await lists.GetSettingsAsync(), await lists.GetStaffAsync(), vendors, today);
    var p = new FuelIntake.UI.Printing.PrintContext(record, new FuelIntake.Core.Model.IntakeCalculations(record, ctx), ctx.Settings, today);
    var list = string.IsNullOrEmpty(forms) ? FuelIntake.Core.Packets.PacketCatalog.AllForms
        : forms.Split(',').Select(f => Enum.Parse<FuelIntake.Core.Packets.FormId>(f, true)).ToList();
    var html = await new FuelIntake.UI.Printing.PacketRenderer(services).RenderAsync(p, list, "preview");
    return Results.Content(html, "text/html");
});

app.Run();
