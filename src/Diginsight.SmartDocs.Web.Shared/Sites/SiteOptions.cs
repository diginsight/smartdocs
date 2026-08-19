namespace Diginsight.SmartDocs.Web.Shared.Sites;

/// <summary>
/// Site configuration bound from the <c>Site</c> section. One deployment serves one site, which
/// publishes one or more <see cref="SpaceOptions">spaces</see>. Where a space mounts is stated by
/// its <see cref="SpaceOptions.RouteBase"/> and by nothing else — the number of configured spaces
/// never affects routing.
/// </summary>
public sealed class SiteOptions
{
    /// <summary>Site title shown in the shell and used as the page-title fallback.</summary>
    public string Title { get; set; } = "Diginsight SmartDocs";

    /// <summary>Path served (with a 404) when a request resolves to nothing.</summary>
    public string NotFoundPath { get; set; } = "404.html";

    /// <summary>Optional shared secret guarding the cache-invalidation endpoint.</summary>
    public string InvalidateApiKey { get; set; } = string.Empty;

    /// <summary>Publisher-level branding, applied to every space this deployment serves.</summary>
    public BrandingOptions Branding { get; set; } = new();

    public IList<SpaceOptions> Spaces { get; set; } = new List<SpaceOptions>();
}

/// <summary>Publisher identity. Branding is per deployment, never per space.</summary>
public sealed class BrandingOptions
{
    public string ProductName { get; set; } = "Diginsight SmartDocs";

    /// <summary>Content-relative path to the logo, or empty to use the built-in icon.</summary>
    public string LogoPath { get; set; } = string.Empty;

    /// <summary>Bootstrap-icon name used when <see cref="LogoPath"/> is empty.</summary>
    public string IconClass { get; set; } = "bi-lightbulb-fill";

    /// <summary>Named theme applied on first load; users may override it locally.</summary>
    public string DefaultTheme { get; set; } = string.Empty;
}

public sealed class SiteShellOptions
{
    public string Title { get; set; } = "Diginsight SmartDocs";
    public BrandingOptions Branding { get; set; } = new();

    public static SiteShellOptions From(SiteOptions site) => new()
    {
        Title = site.Title,
        Branding = site.Branding,
    };
}

public sealed class SiteShellState
{
    public string Title { get; private set; } = "Diginsight SmartDocs";
    public BrandingOptions Branding { get; private set; } = new();
    public bool IsConfigured { get; private set; }

    public event Action? Changed;

    public SiteShellState()
    {
    }

    public SiteShellState(SiteOptions site)
    {
        Apply(SiteShellOptions.From(site));
    }

    public void Apply(SiteShellOptions site)
    {
        Title = string.IsNullOrWhiteSpace(site.Title) ? "Diginsight SmartDocs" : site.Title;
        Branding = site.Branding ?? new BrandingOptions();
        IsConfigured = true;
        Changed?.Invoke();
    }
}

/// <summary>
/// One published documentation set. <see cref="Id"/> and <see cref="BlobOptions.ContainerName"/>
/// are configured independently and are never derived from one another: identifiers read naturally
/// with dots, container names must satisfy Azure's lowercase-and-hyphen rule.
/// </summary>
public sealed class SpaceOptions
{
    /// <summary>Stable identifier, e.g. <c>diginsight.smartdocs</c>. Keys the cache and the metrics snapshot.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Mount point. <c>/</c> (or empty) mounts the space at the site root; any other value mounts it
    /// under that prefix and reserves the prefix's first segment.
    /// </summary>
    public string RouteBase { get; set; } = "/";

    /// <summary>Display name shown in the space switcher and the generated index.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Emoji or icon shown beside the title.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>Repository this documentation was generated from, linked from the index.</summary>
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>Active content source for this space: <c>Blob</c> or <c>FileSystem</c>.</summary>
    public string Source { get; set; } = "Blob";

    public SpaceBlobOptions Blob { get; set; } = new();

    public SpaceFileSystemOptions FileSystem { get; set; } = new();

    /// <summary>True when this space claims the site root.</summary>
    public bool IsRootMounted =>
        string.IsNullOrWhiteSpace(RouteBase) || RouteBase == "/";

    /// <summary>The route base without trailing slash, e.g. <c>/diginsight.smartdocs</c>. Empty when root-mounted.</summary>
    public string NormalizedRouteBase =>
        IsRootMounted ? string.Empty : "/" + RouteBase.Trim('/');
}

public sealed class SpaceBlobOptions
{
    public string AccountUri { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
}

public sealed class SpaceFileSystemOptions
{
    /// <summary>Root folder holding the Markdown content, resolved against the content root.</summary>
    public string RootPath { get; set; } = ".";
    public bool WatchForChanges { get; set; }
}
