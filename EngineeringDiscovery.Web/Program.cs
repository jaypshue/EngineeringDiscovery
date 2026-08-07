using EngineeringDiscovery.Web.Components;
using EngineeringDiscovery.Core.Services;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// WorkspaceState is the canonical domain state owner (Core). Presentation services must provide
// view-state storage via IViewStateStore. Register the Core WorkspaceState and the presentation
// view-state store implementation below.
// Register persistence implementation and WorkspaceState. WorkspaceState constructor no longer performs I/O;
// hosts must explicitly load persisted workspace and call ReplaceWorkspace.
builder.Services.AddSingleton<IWorkspacePersistence>(sp => new FileWorkspacePersistence(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EngineeringDiscovery")));
builder.Services.AddSingleton<EngineeringDiscovery.Core.Services.WorkspaceState>();
// Register EngineeringPartner abstraction
builder.Services.AddSingleton<EngineeringDiscovery.Core.Services.IEngineeringPartner, EngineeringDiscovery.Core.Services.EngineeringPartner>();
// Register in-memory engineering model repository (same as WPF host)
builder.Services.AddSingleton<EngineeringDiscovery.Core.Services.IEngineeringModelRepository, EngineeringDiscovery.Core.Services.InMemoryEngineeringModelRepository>();
// Production repo fingerprint service
builder.Services.AddSingleton<EngineeringDiscovery.Core.Services.IRepoFingerprintService, EngineeringDiscovery.Core.Services.FileRepoFingerprintService>();

// Core services for current-task workflow
builder.Services.AddSingleton<EngineeringDiscovery.Core.Services.ITimeProvider, EngineeringDiscovery.Core.Services.SystemTimeProvider>();
builder.Services.AddSingleton<EngineeringDiscovery.Core.Services.ICurrentTaskService, EngineeringDiscovery.Core.Services.CurrentTaskService>();

// Presentation services (implemented in Web project)
builder.Services.AddSingleton<EngineeringDiscovery.Web.Services.EngineeringAdvisorService>();
builder.Services.AddSingleton<EngineeringDiscovery.Web.Services.EngineeringInsightService>();
builder.Services.AddSingleton<EngineeringDiscovery.Web.Services.EngineeringRecommendationService>();
// Register presentation view state store (per-circuit for Blazor Server). Use scoped for server-side.
builder.Services.AddScoped<EngineeringDiscovery.Core.Services.IViewStateStore, EngineeringDiscovery.Web.Services.WebViewStateStore>();
// Repository selection interaction service (presentation-owned)
builder.Services.AddScoped<EngineeringDiscovery.Web.Services.IRepositorySelectionService, EngineeringDiscovery.Web.Services.RepositorySelectionService>();

var app = builder.Build();

// After building services, explicitly load persisted workspace (if any) and initialize WorkspaceState.
using (var scope = app.Services.CreateScope())
{
    var persistence = scope.ServiceProvider.GetRequiredService<IWorkspacePersistence>();
    var workspaceState = scope.ServiceProvider.GetRequiredService<EngineeringDiscovery.Core.Services.WorkspaceState>();
    var loaded = persistence.LoadAsync().GetAwaiter().GetResult();
    if (loaded is not null)
    {
        workspaceState.ReplaceWorkspace(loaded);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
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

app.Run();
