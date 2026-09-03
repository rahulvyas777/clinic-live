using ClinicLive.Pocket.Shared.Services.Device;
using ClinicLive.Pocket.Web.Components;
using ClinicLive.Pocket.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The web host answers the same capability questions as the phone —
// honestly. A browser is not a phone, and the fallbacks say so.
builder.Services.AddSingleton<IPlatformInfo, PlatformInfo>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(ClinicLive.Pocket.Shared._Imports).Assembly);

app.Run();
