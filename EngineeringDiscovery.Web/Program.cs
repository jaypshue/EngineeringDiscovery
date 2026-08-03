using EngineeringDiscovery.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// WorkspaceState is now the single owner of application state
// (CurrentTaskState and InvestigationState are deprecated for state ownership)
// Workspace state: persistent single workspace
builder.Services.AddSingleton<EngineeringDiscovery.Web.Services.WorkspaceState>();

var app = builder.Build();

// After building services, seed CurrentTaskState and InvestigationState from persisted workspace if present
var workspaceState = app.Services.GetRequiredService<EngineeringDiscovery.Web.Services.WorkspaceState>();
// No seeding required: UI reads/writes via WorkspaceState directly

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
