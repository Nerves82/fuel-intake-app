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
app.Run();
