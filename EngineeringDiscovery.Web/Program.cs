using EngineeringDiscovery.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// Shared investigation state for workspace components
builder.Services.AddSingleton<EngineeringDiscovery.Web.Services.InvestigationState>();
builder.Services.AddSingleton<EngineeringDiscovery.Web.Services.CurrentTaskState>();
// Workspace state: persistent single workspace
builder.Services.AddSingleton<EngineeringDiscovery.Web.Services.WorkspaceState>();

var app = builder.Build();

// After building services, seed CurrentTaskState and InvestigationState from persisted workspace if present
var workspaceState = app.Services.GetRequiredService<EngineeringDiscovery.Web.Services.WorkspaceState>();
var currentTaskState = app.Services.GetRequiredService<EngineeringDiscovery.Web.Services.CurrentTaskState>();
var investigationState = app.Services.GetRequiredService<EngineeringDiscovery.Web.Services.InvestigationState>();

if (workspaceState.HasWorkspace && workspaceState.ActiveWorkspace is not null)
{
    currentTaskState.SeedFromWorkspace(workspaceState.ActiveWorkspace.CurrentTask);
    // Seed the investigation only if the persisted workspace contains a serializable Investigation model.
    // The persisted Investigation type is the simplified model under EngineeringDiscovery.Core.Domain.Models.Investigation
    // to avoid coupling to the rich aggregate root in EngineeringDiscovery.Core.Domain.Investigation.
    // Seed investigation state using the simplified model stored in the persisted workspace if available.
    // Workspace.ActiveWorkspace.Investigation maps to EngineeringDiscovery.Core.Domain.Investigation.Investigation (rich aggregate)
    // but InvestigationState accepts the same type for view purposes. Use the persisted value directly.
    investigationState.SetInvestigation(workspaceState.ActiveWorkspace.Investigation);
}

// Register persistence hooks so changes to the compatibility state are saved into the Workspace
workspaceState.RegisterPersistenceHooks(currentTaskState, investigationState);

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
