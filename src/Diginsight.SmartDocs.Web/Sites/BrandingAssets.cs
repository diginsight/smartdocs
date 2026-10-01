using Diginsight.SmartDocs.Web.ContentSources;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.Extensions.Options;

namespace Diginsight.SmartDocs.Web.Sites;

/// <summary>
/// This deployment's brand mark, located once at startup.
/// <para>
/// Branding identifies the publisher, so one deployment has one mark and every page shows it —
/// including the generated space index, which belongs to no space at all. The asset stays content
/// rather than something compiled in, so it is fetched from a space's content set; but it MUST NOT
/// be addressed through the space-mounted namespace. That namespace is keyed by route base, and a
/// site whose spaces are all prefixed leaves the root unclaimed, so a deployment-level key resolves
/// to no space and the mark silently 404s.
/// </para>
/// <para>
/// Resolution therefore asks each configured space, in declaration order, for
/// <see cref="BrandingOptions.LogoPath"/> against that space's own content set; the first space
/// carrying it wins. The shell then renders <see cref="LogoRoute"/> — one stable URL, identical on
/// every page of every space, so the browser fetches the mark once.
/// </para>
/// </summary>
public sealed class BrandingAssets
{
    /// <summary>Stable, space-independent URL of the brand mark this deployment serves.</summary>
    public const string LogoRoute = "/_branding/logo";

    /// <summary>Space whose content set carries the mark, or null when no space does.</summary>
    public string? LogoSpaceId { get; private set; }

    /// <summary>Key of the mark inside <see cref="LogoSpaceId"/>'s own content set.</summary>
    public string? LogoContentKey { get; private set; }

    /// <summary>What the shell renders, or empty to fall back to the built-in icon.</summary>
    public string LogoUrl { get; private set; } = string.Empty;

    /// <summary>The mark was found in a space and is served by this host.</summary>
    public void UseServedLogo(string spaceId, string contentKey)
    {
        LogoSpaceId = spaceId;
        LogoContentKey = contentKey;
        LogoUrl = LogoRoute;
    }

    /// <summary>A mark hosted elsewhere is linked as configured and never fetched by this host.</summary>
    public void UseExternalLogo(string url) => LogoUrl = url;
}

public static class BrandingAssetResolver
{
    /// <summary>
    /// Locates the configured brand mark once, after the container is built and before the first
    /// request. Doing it here rather than per request means the prerendered page already carries the
    /// mark, so the header does not visibly change at hydration; it also keeps the browser from
    /// having to ask a question only the host can answer.
    /// <para>
    /// A mark added to the content store later needs a restart to be picked up. That is deliberate:
    /// branding changes about as often as the deployment itself, and the alternative — re-checking
    /// on some cadence — buys nothing for an asset nobody edits.
    /// </para>
    /// </summary>
    public static void Resolve(IServiceProvider services, ILogger logger)
    {
        var assets = services.GetRequiredService<BrandingAssets>();
        SiteOptions site = services.GetRequiredService<IOptions<SiteOptions>>().Value;

        string configured = (site.Branding.LogoPath ?? string.Empty).Trim();
        if (configured.Length == 0)
        {
            return;
        }

        if (configured.Contains("://", StringComparison.Ordinal))
        {
            assets.UseExternalLogo(configured);
            logger.LogInformation("Brand mark linked from {Url}", configured);
            return;
        }

        string key = configured.Replace('\\', '/').TrimStart('/');

        // Declaration order, read from the space registry's list rather than the content registry's
        // dictionary: "the first configured space that carries it" is only a rule if the order it
        // names is the configured one.
        var spaceRegistry = services.GetRequiredService<SpaceRegistry>();
        var contentRegistry = services.GetRequiredService<SpaceContentRegistry>();

        foreach (SpaceOptions space in spaceRegistry.All)
        {
            if (!contentRegistry.TryGet(space.Id, out SpaceContentAccess access))
            {
                continue;
            }

            ContentResult? found;
            try
            {
                found = access.Source.GetAsync(key).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // One unreachable space must not decide the whole site's branding.
                logger.LogWarning(ex, "Brand mark lookup failed in space '{SpaceId}'", space.Id);
                continue;
            }

            if (found is null)
            {
                continue;
            }

            assets.UseServedLogo(space.Id, key);
            logger.LogInformation(
                "Brand mark '{Key}' resolved in space '{SpaceId}', served at {Route}",
                key, space.Id, BrandingAssets.LogoRoute);
            return;
        }

        logger.LogWarning(
            "Site:Branding:LogoPath is '{Path}' but no configured space carries it; "
            + "the shell falls back to the built-in icon '{IconClass}'.",
            configured, site.Branding.IconClass);
    }
}
