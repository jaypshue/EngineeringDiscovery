using EngineeringDiscovery.Web.Components;
using EngineeringDiscovery.Core.Services;
using System.IO;
using EngineeringDiscovery.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Developer-only startup mode (default: ResumePrevious). Set via environment variable ENGINEOS_STARTUP_MODE
// Valid values: ResumePrevious | AlwaysStartFresh | AskEveryTime
var startupModeEnv = Environment.GetEnvironmentVariable("ENGINEOS_STARTUP_MODE") ?? "ResumePrevious";
if (!System.Enum.TryParse<EngineeringDiscovery.Web.Services.DeveloperStartupMode>(startupModeEnv, out var developerStartupMode))
{
    developerStartupMode = EngineeringDiscovery.Web.Services.DeveloperStartupMode.ResumePrevious;
}
// Register as an instance by type because enums are value types
builder.Services.AddSingleton(typeof(EngineeringDiscovery.Web.Services.DeveloperStartupMode), developerStartupMode);

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
// Presentation helper to coordinate one-time startup interactions between Landing and Conversation
builder.Services.AddSingleton<EngineeringDiscovery.Web.Services.SessionStartupService>();
// Register EngineeringPartner abstraction
builder.Services.AddSingleton<EngineeringDiscovery.Core.Services.IEngineeringPartner, EngineeringDiscovery.Core.Services.EngineeringPartner>();
// Register in-memory engineering model repository (same as WPF host)
builder.Services.AddSingleton<EngineeringDiscovery.Core.Services.IEngineeringModelRepository, EngineeringDiscovery.Core.Services.InMemoryEngineeringModelRepository>();
// Production repo fingerprint service
builder.Services.AddSingleton<EngineeringDiscovery.Core.Services.IRepoFingerprintService, EngineeringDiscovery.Core.Services.FileRepoFingerprintService>();

// Observation Engine (core) - ingest observations and update Engineering State
builder.Services.AddSingleton<EngineeringDiscovery.Core.Services.IObservationService, EngineeringDiscovery.Core.Services.ObservationEngine>();

// HTTP client for spike Luna conversational calls
builder.Services.AddHttpClient<EngineeringDiscovery.Core.Services.LunaConversationService>();

// Core services for current-task workflow
builder.Services.AddSingleton<EngineeringDiscovery.Core.Services.ITimeProvider, EngineeringDiscovery.Core.Services.SystemTimeProvider>();
builder.Services.AddSingleton<EngineeringDiscovery.Core.Services.ICurrentTaskService, EngineeringDiscovery.Core.Services.CurrentTaskService>();

// Presentation services (implemented in Web project)
builder.Services.AddSingleton<EngineeringDiscovery.Web.Services.EngineeringAdvisorService>();
builder.Services.AddSingleton<EngineeringDiscovery.Web.Services.EngineeringInsightService>();
builder.Services.AddSingleton<EngineeringDiscovery.Web.Services.EngineeringRecommendationService>();
builder.Services.AddSingleton<EngineeringDiscovery.Web.Services.WorkspaceStateService>();
// Diagnostic circuit handler (temporary, logging-only)
builder.Services.AddSingleton<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, EngineeringDiscovery.Web.Services.DiagnosticCircuitHandler>();
// Session-scoped WorkContract manager for ED-303
builder.Services.AddScoped<EngineeringDiscovery.Web.Services.WorkContractService>();
// EngineeringStateService must be scoped to the same lifetime as WorkContractService because it
// observes session-scoped contract changes and therefore cannot be a singleton.
builder.Services.AddScoped<EngineeringDiscovery.Web.Services.EngineeringStateService>();
// Iteration service (presentation-scoped) for Engineering Iteration MVP
builder.Services.AddScoped<EngineeringDiscovery.Web.Services.IterationService>();
// Register presentation view state store (per-circuit for Blazor Server). Use scoped for server-side.
builder.Services.AddScoped<EngineeringDiscovery.Core.Services.IViewStateStore, EngineeringDiscovery.Web.Services.WebViewStateStore>();
// Repository selection interaction service (presentation-owned)
builder.Services.AddScoped<EngineeringDiscovery.Web.Services.IRepositorySelectionService, EngineeringDiscovery.Web.Services.RepositorySelectionService>();

var app = builder.Build();

// Temporary global exception hooks for diagnostic capture (logging-only)
try
{
    AppDomain.CurrentDomain.UnhandledException += (s, e) =>
    {
        try { Console.WriteLine($"[UNHANDLED] {DateTime.UtcNow:o} {e.ExceptionObject?.ToString()}"); } catch { }
    };

    TaskScheduler.UnobservedTaskException += (s, e) =>
    {
        try { Console.WriteLine($"[UNOBSERVED] {DateTime.UtcNow:o} {e.Exception?.ToString()}"); } catch { }
    };
}
catch { }

// Hook startup workspace notification to the WorkspaceStateService now that the app provider is built
try
{
    var startup = app.Services.GetService<SessionStartupService>();
    var workspace = app.Services.GetService<WorkspaceStateService>();
    if (startup != null && workspace != null)
    {
        startup.WorkspaceStateReady += (repoName, repoPath, goal, story, status) => workspace.SetState(repoName, repoPath, goal, story, status);
    }
}
catch { }

// After building services, explicitly load persisted workspace (if any) and initialize WorkspaceState.
using (var scope = app.Services.CreateScope())
{
    // Wire SessionStartupService.WorkspaceStateReady to update WorkspaceStateService when available.
    var startup = scope.ServiceProvider.GetService<EngineeringDiscovery.Web.Services.SessionStartupService>();
    var workspace = scope.ServiceProvider.GetService<EngineeringDiscovery.Web.Services.WorkspaceStateService>();
    if (startup is not null && workspace is not null)
    {
        startup.WorkspaceStateReady += (repoName, repoPath, goal, story, status) => workspace.SetState(repoName, repoPath, goal, story, status);
    }

    var persistence = scope.ServiceProvider.GetRequiredService<IWorkspacePersistence>();
    var workspaceState = scope.ServiceProvider.GetRequiredService<EngineeringDiscovery.Core.Services.WorkspaceState>();
    var loaded = persistence.LoadAsync().GetAwaiter().GetResult();
    if (loaded is not null)
    {
        // Replace canonical workspace in core state. WorkspaceState.ReplaceWorkspace will perform a
        // non-destructive migration from legacy RepositoryPath into ImportedRepositories when needed.
        workspaceState.ReplaceWorkspace(loaded);

        // Mirror a presentation-friendly summary into the WorkspaceStateService. Presentation state
        // must not be treated as authoritative for repository collection. Prefer ImportedRepositories
        // when available and fall back to legacy RepositoryPath only for backward compatibility.
        var presentationWorkspace = scope.ServiceProvider.GetService<EngineeringDiscovery.Web.Services.WorkspaceStateService>();
        if (presentationWorkspace is not null)
        {
            string repoPath = string.Empty;
            string repoName = string.Empty;
            string status = loaded.Investigation?.Status.ToString() ?? "Ready";

            try
            {
                if (loaded.ImportedRepositories != null && loaded.ImportedRepositories.Count > 0)
                {
                    // Use the first imported repository as a presentation-friendly selection seed.
                    repoPath = loaded.ImportedRepositories[0].RepositoryPath ?? string.Empty;
                }
                else
                {
                    repoPath = loaded.RepositoryPath ?? string.Empty; // legacy fallback
                }

                repoName = string.IsNullOrWhiteSpace(repoPath) ? string.Empty : System.IO.Path.GetFileName(repoPath);
            }
            catch { }

            presentationWorkspace.SetState(repoName, repoPath, string.Empty, string.Empty, status);
        }
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

// Request logging middleware (temporary, diagnostic-only)
app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;
    try
    {
        try { Console.WriteLine($"[REQ] {start:o} {context.Request.Method} {context.Request.Path}{context.Request.QueryString}"); } catch { }
        await next();
        try { Console.WriteLine($"[REQ] {DateTime.UtcNow:o} {context.Request.Method} {context.Request.Path} responded {context.Response.StatusCode}"); } catch { }
    }
    catch (Exception ex)
    {
        try { Console.WriteLine($"[REQ-ERR] {DateTime.UtcNow:o} {context.Request.Method} {context.Request.Path} Exception={ex}"); } catch { }
        throw;
    }
});

app.Run();
