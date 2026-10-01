using System.Net.Http.Json;
using Diginsight.SmartDocs.Web.Client;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Rendering;
using Diginsight.SmartDocs.Web.Shared.Services;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Talk back to the origin that served the app (the Diginsight.SmartDocs.Web host).
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// In WASM, content is fetched over HTTP; rendering runs in-browser with the same Markdig engine.
builder.Services.AddScoped<IContentSource, HttpContentSource>();
builder.Services.AddScoped<IMarkdownRenderer, MarkdigMarkdownRenderer>();
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

WebAssemblyHost host = builder.Build();

// The space list decides what a route renders (a space's page or the generated index) and how the
// menus are scoped, so it has to be known before hydration re-renders the prerendered page.
// MainLayout still fetches it if this load fails.
try
{
    using var siteHttp = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    if (await siteHttp.GetFromJsonAsync<SiteShellOptions>("_site") is { } site)
    {
        host.Services.GetRequiredService<SiteShellState>().Apply(site);
    }
}
catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
{
}

await host.RunAsync();
