using System.Text.Json.Serialization;

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

    /// <summary>Introductory line of the generated space index, editable without a rebuild.</summary>
    public string IndexIntro { get; set; } = SiteShellOptions.DefaultIndexIntro;

    /// <summary>
    /// Palettes this deployment offers, replacing the product's built-in set when non-empty.
    /// <para>
    /// Declared ONLY by an environment overlay, never here — configuration binds a JSON array by
    /// index, so a base-file entry would be patched rather than replaced and a deployment could
    /// never offer fewer themes than the product ships. This mirrors the rule <c>Site:Spaces</c>
    /// already follows, and for the same reason.
    /// </para>
    /// </summary>
    public IList<ThemeOption> Themes { get; set; } = new List<ThemeOption>();

    /// <summary>
    /// Folder names to treat as asset folders in addition to the built-in set
    /// (<c>images</c>, <c>img</c>, <c>assets</c>, <c>asset</c>, <c>media</c>, <c>attachments</c>,
    /// <c>files</c>). An asset folder never appears in navigation, at any depth. Declared only by an
    /// environment overlay, for the same array-binding reason as <see cref="Themes"/>.
    /// </summary>
    public IList<string> AssetFolders { get; set; } = new List<string>();

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

/// <summary>
/// The part of the site configuration the shell needs, sent to the browser through <c>/_site</c>.
/// Spaces travel as <see cref="SpaceShellInfo"/>, so no storage identity ever reaches the client.
/// </summary>
public sealed class SiteShellOptions
{
    public const string DefaultIndexIntro = "The current site includes documentation for the following repositories:";

    public string Title { get; set; } = "Diginsight SmartDocs";
    public BrandingOptions Branding { get; set; } = new();
    public string IndexIntro { get; set; } = DefaultIndexIntro;
    public List<SpaceShellInfo> Spaces { get; set; } = new();

    /// <summary>
    /// Where the shell fetches the brand mark, or empty to fall back to
    /// <see cref="BrandingOptions.IconClass"/>. Resolved by the host, never by the browser: the
    /// configured <see cref="BrandingOptions.LogoPath"/> names a key inside some space's content
    /// set, and which space that is — or whether any space carries it at all — is knowledge the
    /// client does not have.
    /// </summary>
    public string LogoUrl { get; set; } = string.Empty;

    /// <summary>
    /// The palettes the picker offers, resolved by the host. The browser is told which themes exist
    /// but never how they are painted — the host has already written their tokens into the document,
    /// so a theme cannot be selectable without being renderable.
    /// </summary>
    public List<ThemeOption> Themes { get; set; } = new();

    public static SiteShellOptions From(SiteOptions site, string logoUrl = "") => new()
    {
        Title = site.Title,
        Branding = site.Branding,
        IndexIntro = site.IndexIntro,
        Spaces = site.Spaces.Select(SpaceShellInfo.From).ToList(),
        LogoUrl = logoUrl,
        Themes = ThemeCatalog.Resolve(site.Themes).ToList(),
    };
}

/// <summary>Public, display-only view of a <see cref="SpaceOptions"/>.</summary>
public sealed class SpaceShellInfo
{
    public string Id { get; set; } = string.Empty;
    public string RouteBase { get; set; } = "/";
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;

    public static SpaceShellInfo From(SpaceOptions space) => new()
    {
        Id = space.Id,
        RouteBase = space.RouteBase,
        Title = string.IsNullOrWhiteSpace(space.Title) ? space.Id : space.Title,
        Icon = space.Icon,
        Description = space.Description,
        RepositoryUrl = space.RepositoryUrl,
    };

    [JsonIgnore]
    public bool IsRootMounted => string.IsNullOrWhiteSpace(RouteBase) || RouteBase.Trim() == "/";

    /// <summary>The route-base segment (e.g. <c>apidevice</c>), or empty for a root-mounted space.</summary>
    [JsonIgnore]
    public string Segment => IsRootMounted ? string.Empty : RouteBase.Trim().Trim('/');

    /// <summary>The space's landing route, rooted: <c>/</c> or <c>/apidevice</c>.</summary>
    [JsonIgnore]
    public string HomeRoute => "/" + Segment;
}

public sealed class SiteShellState
{
    public string Title { get; private set; } = "Diginsight SmartDocs";
    public BrandingOptions Branding { get; private set; } = new();
    public string IndexIntro { get; private set; } = SiteShellOptions.DefaultIndexIntro;
    public IReadOnlyList<SpaceShellInfo> Spaces { get; private set; } = Array.Empty<SpaceShellInfo>();
    public bool IsConfigured { get; private set; }

    /// <summary>Brand-mark URL resolved by the host, or empty to use the built-in icon.</summary>
    public string LogoUrl { get; private set; } = string.Empty;

    /// <summary>The palettes this deployment offers, in menu order.</summary>
    public IReadOnlyList<ThemeOption> Themes { get; private set; } = ThemeCatalog.BuiltIn;

    /// <summary>True when this deployment has a brand mark to render in place of the icon.</summary>
    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoUrl);

    public event Action? Changed;

    public SiteShellState()
    {
    }

    public SiteShellState(SiteOptions site, string logoUrl = "")
    {
        Apply(SiteShellOptions.From(site, logoUrl));
    }

    /// <summary>True when no space claims the site root, so <c>/</c> serves the generated space index.</summary>
    public bool ServesIndexAtRoot => Spaces.Count > 0 && !Spaces.Any(static s => s.IsRootMounted);

    /// <summary>True when the site publishes more than one space, so a switcher is worth showing.</summary>
    public bool IsMultiSpace => Spaces.Count > 1;

    public void Apply(SiteShellOptions site)
    {
        Title = string.IsNullOrWhiteSpace(site.Title) ? "Diginsight SmartDocs" : site.Title;
        Branding = site.Branding ?? new BrandingOptions();
        IndexIntro = string.IsNullOrWhiteSpace(site.IndexIntro) ? SiteShellOptions.DefaultIndexIntro : site.IndexIntro;
        Spaces = site.Spaces ?? new List<SpaceShellInfo>();
        LogoUrl = site.LogoUrl ?? string.Empty;
        Themes = ThemeCatalog.Resolve(site.Themes);
        IsConfigured = true;
        Changed?.Invoke();
    }

    /// <summary>
    /// The space that owns a route, by the same rule as <see cref="SpaceRegistry.TryResolve"/>:
    /// a prefixed space whose segment starts the route wins, else the root-mounted space, else none
    /// (the route belongs to the generated index).
    /// </summary>
    public SpaceShellInfo? ResolveSpace(string? route)
    {
        string path = (route ?? string.Empty).Replace('\\', '/').Trim('/');
        int slash = path.IndexOf('/');
        string first = slash >= 0 ? path[..slash] : path;

        if (first.Length > 0)
        {
            foreach (SpaceShellInfo space in Spaces)
            {
                if (!space.IsRootMounted && string.Equals(space.Segment, first, StringComparison.OrdinalIgnoreCase))
                {
                    return space;
                }
            }
        }

        return Spaces.FirstOrDefault(static s => s.IsRootMounted);
    }

    /// <summary>True when <paramref name="segment"/> is the route base of a prefixed space.</summary>
    public bool IsMountSegment(string? segment) =>
        !string.IsNullOrEmpty(segment) &&
        Spaces.Any(s => !s.IsRootMounted && string.Equals(s.Segment, segment.Trim('/'), StringComparison.OrdinalIgnoreCase));
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

    /// <summary>Optional one-line summary shown on the space's card in the generated index.</summary>
    public string Description { get; set; } = string.Empty;

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
