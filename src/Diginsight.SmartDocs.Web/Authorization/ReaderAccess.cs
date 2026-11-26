using System.Collections.Concurrent;
using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Caching;
using Diginsight.SmartDocs.Web.ContentSources;
using Diginsight.SmartDocs.Web.Navigation;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.Authorization;

/// <summary>
/// Captures the current request's reader and lazily resolves only the policy scopes that its
/// navigation or content requests need. Per-request memoization keeps grants and requirements on
/// the same policy revision without storing a principal's verdict in a shared cache.
/// <para>
/// Filters find each node's space from its <see cref="NavChild.SourceFolder"/>, never from its route:
/// a mount node and a root-mounted space's page can share a route. A node that carries no source
/// folder can't be placed in a space, so it is dropped whenever the reader doesn't see everything.
/// The site root's generated <b>Home</b> link is the one exception, decided by what <c>/</c> shows.
/// </para>
/// </summary>
public sealed class ReaderAccess(
    SpaceRegistry spaces,
    SpaceVisibilityEvaluator evaluator,
    AccessRequirementResolver requirements,
    FolderRecordProvider folders,
    ReaderCountComposer counts,
    ILogger<ReaderAccess> logger)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<SpaceVisibility>>> spaceVisibility =
        new(StringComparer.OrdinalIgnoreCase);
    private RoleSet? roles;
    private Task<ReaderVisibility>? allVisibility;
    private CancellationToken requestAborted;

    /// <summary>True once <see cref="InitializeAsync"/> has run for this request.</summary>
    public bool IsInitialized => roles is not null;

    /// <summary>The reader's role set.</summary>
    public RoleSet Roles => roles
        ?? throw new InvalidOperationException("ReaderAccess was read before the request pipeline initialized it.");

    /// <summary>The spaces visible to a full-page shell after its global navigation policy has been evaluated.</summary>
    public IReadOnlyList<SpaceOptions> VisibleSpaces =>
        spaces.All.Where(space => IsSpaceVisible(space.Id)).ToArray();

    public bool IsSpaceVisible(string spaceId) =>
        GetCompletedVisibility().Spaces.TryGetValue(spaceId, out SpaceVisibility? verdict) && verdict.IsVisible;

    /// <summary>Captures the request's principal without reading unrelated space policies.</summary>
    public Task InitializeAsync(HttpContext context)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        roles = RoleSet.FromPrincipal(context.User);
        requestAborted = context.RequestAborted;
        activity?.SetOutput(new { roles.IsSignedIn });
        return Task.CompletedTask;
    }

    /// <summary>Whether the current reader may see a mounted path, using only its owning space.</summary>
    public async Task<PathAccessDecision> ResolvePathAccessAsync(
        string? path, CancellationToken cancellationToken = default)
    {
        string normalized = ContentPathCacheKey.Normalize(path ?? string.Empty);
        if (!ContentKeyGuard.IsSafe(normalized))
        {
            return new PathAccessDecision(false, false);
        }

        if (spaces.TryResolve(normalized, out SpaceOptions space, out _))
        {
            SpaceVisibility visibility = await GetSpaceVisibilityAsync(space.Id, cancellationToken);
            return new PathAccessDecision(visibility.IsVisible, visibility.Requirement.IsEmpty);
        }

        AccessRequirement requirement = requirements.ForOutsideSpaces();
        return new PathAccessDecision(IsOutsideVisible(requirement), requirement.IsEmpty);
    }

    public async Task<bool> IsPathVisibleAsync(string? path, CancellationToken cancellationToken = default) =>
        (await ResolvePathAccessAsync(path, cancellationToken)).IsVisible;

    /// <summary>Visible spaces in configured order; global shell generation legitimately needs every space.</summary>
    public async Task<IReadOnlyList<SpaceOptions>> GetVisibleSpacesAsync(CancellationToken cancellationToken = default)
    {
        ReaderVisibility visible = await GetAllVisibilityAsync(cancellationToken);
        return spaces.All.Where(space =>
            visible.Spaces.TryGetValue(space.Id, out SpaceVisibility? verdict) && verdict.IsVisible).ToArray();
    }

    /// <summary>Drops the nodes of one navigation level whose space the reader can't see.</summary>
    public async Task<IReadOnlyList<NavChild>> FilterLevelAsync(
        IReadOnlyList<NavChild> level, CancellationToken cancellationToken = default)
    {
        bool[] visible = await Task.WhenAll(level.Select(child =>
            IsSiteHome(child)
                ? IsSiteHomeVisibleAsync(cancellationToken)
                : IsPathVisibleAsync(child.SourceFolder, cancellationToken)));
        return level.Where((_, index) => visible[index]).ToArray();
    }

    /// <summary>Drops the leaves of the article index whose space the reader can't see.</summary>
    public async Task<IReadOnlyList<NavLeaf>> FilterIndexAsync(
        IReadOnlyList<NavLeaf> leaves, CancellationToken cancellationToken = default)
    {
        bool[] visible = await Task.WhenAll(leaves.Select(leaf =>
            IsPathVisibleAsync(leaf.SourceFolder, cancellationToken)));
        return leaves.Where((_, index) => visible[index]).ToArray();
    }

    /// <summary>
    /// The folder record of <paramref name="prefix"/> as this reader may see it. A folder the reader
    /// can't see answers the record of a folder that doesn't exist. The empty prefix is the site's own
    /// record, built from site settings, so it is never hidden; its counts cover only the spaces the
    /// reader sees.
    /// </summary>
    public async Task<FolderRecord> GetFolderAsync(string? prefix, CancellationToken cancellationToken = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefix });

        string normalized = (prefix ?? string.Empty).Replace('\\', '/').Trim('/');
        if (normalized.Length > 0 && !await IsPathVisibleAsync(normalized, cancellationToken))
        {
            return folders.Unknown(normalized);
        }

        FolderRecord record = await folders.GetAsync(normalized, cancellationToken);
        return normalized.Length == 0
            ? counts.ComposeRootRecord(record, await GetAllVisibilityAsync(cancellationToken))
            : record;
    }

    /// <summary>The site-wide counts this reader may see, or null while the index has no root cell.</summary>
    public async Task<FolderMetrics?> GetTotalAsync(CancellationToken cancellationToken = default) =>
        counts.ComposeRoot(await GetAllVisibilityAsync(cancellationToken));

    private Task<SpaceVisibility> GetSpaceVisibilityAsync(string spaceId, CancellationToken cancellationToken)
    {
        CancellationToken token = cancellationToken == default ? requestAborted : cancellationToken;
        Lazy<Task<SpaceVisibility>> lazy = spaceVisibility.GetOrAdd(
            spaceId,
            id => new Lazy<Task<SpaceVisibility>>(
                () => evaluator.EvaluateSpaceAsync(id, Roles, token),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    private Task<ReaderVisibility> GetAllVisibilityAsync(CancellationToken cancellationToken) =>
        allVisibility ??= evaluator.EvaluateAsync(Roles, cancellationToken == default ? requestAborted : cancellationToken);

    private ReaderVisibility GetCompletedVisibility() =>
        allVisibility is { IsCompletedSuccessfully: true } completed
            ? completed.Result
            : throw new InvalidOperationException("Global reader visibility was requested before the shell initialized it.");

    private async Task<bool> IsSiteHomeVisibleAsync(CancellationToken cancellationToken)
    {
        AccessRequirement outside = requirements.ForOutsideSpaces();
        bool outsideVisible = IsOutsideVisible(outside);
        return spaces.TryResolve(string.Empty, out SpaceOptions root, out _)
            ? (await GetSpaceVisibilityAsync(root.Id, cancellationToken)).IsVisible || outsideVisible
            : outsideVisible;
    }

    private bool IsOutsideVisible(AccessRequirement requirement)
    {
        string[] permissions = Roles.IsSignedIn ? [AccessRequirement.Authenticated] : [];
        return requirement.IsCoveredBy(permissions);
    }

    private static bool IsSiteHome(NavChild child) => !child.IsSection && child.Route is { Length: 0 };
}

public sealed record PathAccessDecision(bool IsVisible, bool IsPublic);
