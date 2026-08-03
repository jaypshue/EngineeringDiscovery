using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using EngineeringDiscovery.Web;

namespace EngineeringDiscovery.Desktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { })
            .Services.AddMauiBlazorWebView();

        // Reuse essential services from the Web project
        // Register WorkspaceState and any small singleton services the UI expects.
        builder.Services.AddSingleton<EngineeringDiscovery.Web.Services.WorkspaceState>();

        // Register any other lightweight services expected by Razor components that do not depend on HTTP context.
        // For more complex server-only services, see migration notes.

        // If EngineeringDiscovery.Web exposes additional service registration helpers, call them here.

        return builder.Build();
    }
}
