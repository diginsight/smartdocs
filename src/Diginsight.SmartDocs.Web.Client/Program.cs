using System.Text.Json;
using Diginsight.SmartDocs.Web.Client;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Rendering;
using Diginsight.SmartDocs.Web.Shared.Services;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Talk back to the origin that served the app (the Diginsight.SmartDocs.Web host).
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// In WASM, content and rendered pages are fetched over HTTP; since C21 the server renders
// Markdown once, so no renderer is registered here.
builder.Services.AddScoped<HttpContentSource>();
builder.Services.AddScoped<IContentSource>(sp => sp.GetRequiredService<HttpContentSource>());
builder.Services.AddScoped<IRenderedPageResolver>(sp => sp.GetRequiredService<HttpContentSource>());
builder.Services.AddScoped<PageLoader>();
builder.Services.AddScoped<TocState>();
builder.Services.AddScoped<ThemeState>();
builder.Services.AddScoped<SidebarState>();
builder.Services.AddScoped<NavStats>();
builder.Services.AddScoped<ArticleState>();
// Singleton so the instance filled below, before the first render, is the one every component sees.
builder.Services.AddSingleton<SiteShellState>();
builder.Services.AddScoped<INavProvider, HttpNavProvider>();
builder.Services.AddScoped<NavHubClient>();
builder.Services.AddSingleton<NavigationBootstrapState>();

WebAssemblyHost host = builder.Build();
string? bootstrapJson = await host.Services.GetRequiredService<IJSRuntime>()
    .InvokeAsync<string?>("appUi.readBootstrap");
if (!string.IsNullOrWhiteSpace(bootstrapJson) &&
    JsonSerializer.Deserialize<NavigationBootstrapState>(bootstrapJson) is { } restored)
{
    NavigationBootstrapState bootstrap = host.Services.GetRequiredService<NavigationBootstrapState>();
    bootstrap.Site = restored.Site;
    bootstrap.PageRoute = restored.PageRoute;
    bootstrap.Page = restored.Page;
    bootstrap.Levels = restored.Levels;
    bootstrap.FolderRecords = restored.FolderRecords;
}

await host.RunAsync();
