using Diginsight.SmartDocs.Web.Rendering;
using Diginsight.SmartDocs.Web.Shared.Rendering;

namespace Diginsight.SmartDocs.Web.Authorization;

/// <summary>
/// The server's <see cref="IRenderedPageResolver"/>: resolves a route through the shared rendered-page
/// cache, then checks the file that was actually rendered against the reader's access. A page the
/// reader can't see answers <see cref="RenderedPageResolution.Hidden"/> — never another candidate
/// file — so authorization and rendering can't pick different files for one route.
/// <para>
/// The prerendered <c>ContentView</c>, through <c>PageLoader</c>, and the <c>/_page</c> endpoints read
/// pages only through this type.
/// </para>
/// </summary>
public sealed class AuthorizingRenderedPageResolver(
    RenderedPageProvider pages,
    AccessRequirementResolver requirements,
    ReaderAccess reader) : IRenderedPageResolver
{
    public async Task<RenderedPageResolution> ResolveRenderedAsync(
        string? routePath,
        CancellationToken cancellationToken = default)
    {
        RenderedPageResolution resolution = await pages.ResolveRenderedAsync(routePath, cancellationToken);
        if (resolution.Page is null || reader.SeesEverything)
        {
            return resolution;
        }

        // A rendered page whose file is unknown can't be placed in a space, so it is hidden.
        if (resolution.ContentKey is not { Length: > 0 } key)
        {
            return RenderedPageResolution.Hidden;
        }

        AccessRequirement requirement = await requirements.ForContentKeyAsync(key, cancellationToken);
        return reader.IsVisible(requirement) ? resolution : RenderedPageResolution.Hidden;
    }
}
