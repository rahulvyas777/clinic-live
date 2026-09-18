using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ClinicLive.Api;
using ClinicLive.Components;
using ClinicLive.Components.Account;
using ClinicLive.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
// Factory, not plain AddDbContext: interactive Blazor components outlive a request,
// so each operation needs its own short-lived context. (The factory also registers
// a scoped ApplicationDbContext, which Identity keeps using.)
// UseVector teaches Npgsql the pgvector types; without it the vector(768) column on
// knowledge_chunk comes back as an unmapped "vector" and every read throws.
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npg => npg.UseVector()).UseSnakeCaseNamingConvention());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Staff accounts are created by an admin/seeder, not self-service signup —
        // no email pipeline exists, so confirmed accounts would lock everyone out.
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddSingleton<ClinicLive.Services.ClinicTime>();
builder.Services.AddScoped<ClinicLive.Services.BookingService>();
builder.Services.AddScoped<ClinicLive.Services.QueueService>();
builder.Services.AddScoped<ClinicLive.Services.ChatService>();
builder.Services.AddSingleton<ClinicLive.Services.ChatRoom>();
builder.Services.AddScoped<ClinicLive.Services.PocketService>();
builder.Services.AddSignalR();

// Season four: the assistant runs on the clinic's own box. One HTTP client to Ollama,
// shared by every circuit (singleton); the service that wraps it is per-circuit.
var ai = ClinicLive.Services.Ai.AiOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(ai);
builder.Services.AddSingleton<Microsoft.Extensions.AI.IChatClient>(
    new OllamaSharp.OllamaApiClient(new Uri(ai.Endpoint), ai.ChatModel));
builder.Services.AddScoped<ClinicLive.Services.Ai.AssistantService>();

// Part 6: the same Ollama box, a different model — embeddings, 768 dimensions.
builder.Services.AddSingleton<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>(
    new OllamaSharp.OllamaApiClient(new Uri(ai.Endpoint), ai.EmbeddingModel));
builder.Services.AddScoped<ClinicLive.Services.Ai.KnowledgeIngester>();

// Push notifications (season three, Part 6). Configured = a Firebase service-account
// file OUTSIDE the repo (user-secrets locally, server config in production).
// Unconfigured = NullPushSender, which logs what it would have sent. Same app,
// same tests, no key required to run it.
var serviceAccountPath = builder.Configuration["Push:ServiceAccountPath"];
if (!string.IsNullOrWhiteSpace(serviceAccountPath) && File.Exists(serviceAccountPath))
{
    builder.Services.AddSingleton<ClinicLive.Services.IPushSender>(sp =>
        new ClinicLive.Services.FcmPushSender(serviceAccountPath, sp.GetRequiredService<ILogger<ClinicLive.Services.FcmPushSender>>()));
}
else
{
    builder.Services.AddSingleton<ClinicLive.Services.IPushSender, ClinicLive.Services.NullPushSender>();
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider, app.Environment.IsDevelopment());
}

// `dotnet run --project src/ClinicLive -- ingest` loads the clinic's markdown into
// pgvector and stops. Same host, same configuration, same migrations as the web app —
// a separate console project would have had to duplicate all three.
if (args.Contains("ingest", StringComparer.OrdinalIgnoreCase))
{
    using var ingestScope = app.Services.CreateScope();
    var ingester = ingestScope.ServiceProvider.GetRequiredService<ClinicLive.Services.Ai.KnowledgeIngester>();
    var results = await ingester.IngestAsync();

    var slugWidth = Math.Max(4, results.Count == 0 ? 4 : results.Max(r => r.Slug.Length));
    Console.WriteLine();
    Console.WriteLine($"{"slug".PadRight(slugWidth)}  {"audience",-8}  {"chunks",6}  status");
    Console.WriteLine($"{new string('-', slugWidth)}  {new string('-', 8)}  {new string('-', 6)}  --------");
    foreach (var r in results)
    {
        Console.WriteLine($"{r.Slug.PadRight(slugWidth)}  {r.Audience,-8}  {r.ChunkCount,6}  {(r.Skipped ? "skipped" : "embedded")}");
    }
    Console.WriteLine();
    Console.WriteLine($"{results.Count} documents, {results.Sum(r => r.ChunkCount)} chunks, {results.Count(r => r.Skipped)} skipped.");
    return 0;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<ClinicLive.Hubs.QueueHub>("/hubs/queue");

// Season three: the Pocket app's public API.
app.MapPocketApi();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();

// The ingest branch above returns an exit code, so this path needs one too.
return 0;
