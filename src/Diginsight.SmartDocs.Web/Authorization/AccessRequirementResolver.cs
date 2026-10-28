using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.ContentSources;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.Authorization;

/// <summary>
/// Computes a node's effective requirement: the union of the site default, its space's floor, and
/// its space's access file. Each source can only add, so a repository can't lift a floor or the
/// default, and a missing, invalid or unreadable file never makes a space more visible than its
/// declarations allow.
/// <para>
/// The resolver adds no cache entry. Each call combines the provider's cached access file with the
/// configured default and floor, so no cached value holds an effective requirement and a changed
/// file or setting applies at the next check.
/// </para>
/// </summary>
public sealed class AccessRequirementResolver(
    SpaceRegistry spaces,
    AccessFileProvider files,
    SiteAuthorizationOptions authorization,
    ILogger<AccessRequirementResolver> logger)
{
    /// <summary>The requirement of every node of the space <paramref name="spaceId"/>.</summary>
    public async Task<AccessRequirement> ForSpaceAsync(string spaceId, CancellationToken cancellationToken = default) =>
        (await ResolveSpaceAsync(spaceId, cancellationToken)).Requirement;

    /// <summary>
    /// The requirement of the space <paramref name="spaceId"/> together with the access file it was
    /// computed from. A caller that also needs the space's role map takes both from here, so the
    /// requirement and the grants it is checked against always come from the same read of the file.
    /// </summary>
    public async Task<SpaceAccessState> ResolveSpaceAsync(string spaceId, CancellationToken cancellationToken = default)
    {
        using var activity = Observability.HotPathActivitySource.StartMethodActivity(logger, () => new { spaceId });

        if (spaces.ById(spaceId) is not { } space)
        {
            return new SpaceAccessState(AccessRequirement.UnresolvableIn(spaceId), null);
        }

        var names = new List<string>();
        if (!TryAddConfigured(names, authorization.DefaultRequirement) || !TryAddConfigured(names, space.Access))
        {
            return new SpaceAccessState(AccessRequirement.UnresolvableIn(space.Id), null);
        }

        AccessFileOutcome outcome = await files.GetAsync(space.Id, cancellationToken);
        AccessRequirement requirement = outcome.Status switch
        {
            AccessFileStatus.Valid => AccessRequirement.Of(space.Id, names.Concat(outcome.File!.Requires)),
            AccessFileStatus.Missing when !space.AccessFileRequired => AccessRequirement.Of(space.Id, names),
            _ => AccessRequirement.UnresolvableIn(space.Id),
        };

        activity?.SetOutput(requirement.ToString());
        return new SpaceAccessState(requirement, outcome.Status == AccessFileStatus.Valid ? outcome.File : null);
    }

    /// <summary>The requirement of nodes outside every space: the site default alone.</summary>
    public AccessRequirement ForOutsideSpaces()
    {
        var names = new List<string>();
        return TryAddConfigured(names, authorization.DefaultRequirement)
            ? AccessRequirement.Of(null, names)
            : AccessRequirement.Unresolvable;
    }

    /// <summary>The requirement of the folder <paramref name="prefix"/>, a mounted content path.</summary>
    public Task<AccessRequirement> ForFolderAsync(string? prefix, CancellationToken cancellationToken = default) =>
        ForPathAsync(prefix, cancellationToken);

    /// <summary>The requirement of the file <paramref name="key"/>, a mounted content key.</summary>
    public Task<AccessRequirement> ForContentKeyAsync(string? key, CancellationToken cancellationToken = default) =>
        ForPathAsync(key, cancellationToken);

    /// <summary>
    /// Finds the owning space exactly as <c>SpaceMountedContentSource</c> does — same normalization,
    /// same <see cref="SpaceRegistry.TryResolve"/> — so the space checked is always the space read.
    /// A path outside every space gets the site default alone. A path with a dot, empty or encoded
    /// segment, which a store could resolve into another space, is covered by no reader; the mounted
    /// source refuses to read it too.
    /// </summary>
    private Task<AccessRequirement> ForPathAsync(string? path, CancellationToken cancellationToken)
    {
        if (!ContentKeyGuard.IsSafe(path))
        {
            return Task.FromResult(AccessRequirement.Unresolvable);
        }

        if (spaces.TryResolve(ContentPathCacheKey.Normalize(path ?? string.Empty), out SpaceOptions space, out _))
        {
            return ForSpaceAsync(space.Id, cancellationToken);
        }

        return Task.FromResult(ForOutsideSpaces());
    }

    /// <summary>
    /// Adds a floor or the site default. Startup refuses any value other than empty or
    /// <c>authenticated</c>; should one get here anyway, it fails closed instead of being ignored.
    /// </summary>
    private static bool TryAddConfigured(List<string> names, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!SiteAuthorizationOptions.IsAcceptedRequirement(value))
        {
            return false;
        }

        names.Add(AccessRequirement.Authenticated);
        return true;
    }
}

/// <summary>
/// A space's effective requirement and the valid access file it came from, or null when the file is
/// missing, invalid or unreadable, in which case no role grants anything in the space.
/// </summary>
public sealed record SpaceAccessState(AccessRequirement Requirement, AccessFile? File);
