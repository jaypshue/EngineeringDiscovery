using EngineeringDiscovery.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// WorkspaceState is the canonical domain state owner (Core). Presentation services must provide
// view-state storage via IViewStateStore. Register the Core WorkspaceState and the presentation
// view-state store implementation below.
builder.Services.AddSingleton<EngineeringDiscovery.Core.Services.WorkspaceState>();
// Register presentation view state store (per-circuit for Blazor Server). Use scoped for server-side.
builder.Services.AddScoped<EngineeringDiscovery.Core.Services.IViewStateStore, EngineeringDiscovery.Web.Services.WebViewStateStore>();

var app = builder.Build();

// After building services, seed CurrentTaskState and InvestigationState from persisted workspace if present
var workspaceState = app.Services.GetRequiredService<EngineeringDiscovery.Core.Services.WorkspaceState>();
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
