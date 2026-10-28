using Diginsight.SmartDocs.Web.Navigation;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.Authorization;

/// <summary>
/// What the current request's reader may see. Scoped to the request and initialized once, at the
/// start of the pipeline, by the middleware <c>Program</c> places ahead of navigation bootstrap, so
/// every later component and endpoint reads the same verdicts.
/// <para>
/// Filters find each node's space from its <see cref="NavChild.SourceFolder"/>, never from its route:
/// a mount node and a root-mounted space's page can share a route. A node that carries no source
/// folder can't be placed in a space, so it is dropped whenever the reader doesn't see everything.
/// </para>
/// </summary>
public sealed class ReaderAccess(
    SpaceRegistry spaces,
    SpaceVisibilityEvaluator evaluator,
    AccessRequirementResolver requirements,
    FolderRecordProvider folders,
    ReaderCountComposer counts)
{
    private ReaderVisibility? visibility;

    /// <summary>The verdicts of this request. Reading them before <see cref="InitializeAsync"/> is a wiring fault.</summary>
    private ReaderVisibility Visibility => visibility
        ?? throw new InvalidOperationException("ReaderAccess was read before the request pipeline initialized it.");

    /// <summary>True once <see cref="InitializeAsync"/> has run for this request.</summary>
    public bool IsInitialized => visibility is not null;

    /// <summary>The reader's role set.</summary>
    public RoleSet Roles => Visibility.Roles;

    /// <summary>The spaces the reader may see, in configuration order.</summary>
    public IReadOnlyList<SpaceOptions> VisibleSpaces =>
        spaces.All.Where(space => IsSpaceVisible(space.Id)).ToList();

    /// <summary>True when the reader sees every space and every node outside them.</summary>
    public bool SeesEverything => Visibility.SeesEverything;

    /// <summary>Evaluates every configured space for the reader of <paramref name="context"/>. Runs on every request.</summary>
    public async Task InitializeAsync(HttpContext context)
    {
        visibility = await evaluator.EvaluateAsync(RoleSet.FromPrincipal(context.User), context.RequestAborted);
    }

    /// <summary>True when the reader may see the space <paramref name="spaceId"/>.</summary>
    public bool IsSpaceVisible(string? spaceId) =>
        spaceId is not null && Visibility.Spaces.TryGetValue(spaceId, out SpaceVisibility? space) && space.IsVisible;

    /// <summary>
    /// True when the reader's permissions in the space that owns <paramref name="requirement"/> cover
    /// it. The space must also be visible in this request's evaluation, so a requirement computed
    /// from a newer read of the access file can only hide more, never open a space the evaluation of
    /// this request closed.
    /// </summary>
    public bool IsVisible(AccessRequirement requirement) => Visibility.IsVisible(requirement);

    /// <summary>Drops the nodes of one navigation level whose space the reader can't see.</summary>
    public Task<IReadOnlyList<NavChild>> FilterLevelAsync(IReadOnlyList<NavChild> level)
    {
        if (SeesEverything)
        {
            return Task.FromResult(level);
        }

        IReadOnlyList<NavChild> kept = level.Where(child => IsFolderVisible(child.SourceFolder)).ToList();
        return Task.FromResult(kept);
    }

    /// <summary>Drops the leaves of the article index whose space the reader can't see.</summary>
    public Task<IReadOnlyList<NavLeaf>> FilterIndexAsync(IReadOnlyList<NavLeaf> leaves)
    {
        if (SeesEverything)
        {
            return Task.FromResult(leaves);
        }

        IReadOnlyList<NavLeaf> kept = leaves.Where(leaf => IsFolderVisible(leaf.SourceFolder)).ToList();
        return Task.FromResult(kept);
    }

    /// <summary>
    /// The folder record of <paramref name="prefix"/> as this reader may see it. A folder the reader
    /// can't see answers the record of a folder that doesn't exist. The empty prefix is the site's own
    /// record, built from site settings, so it is never hidden; its counts cover only the spaces the
    /// reader sees.
    /// </summary>
    public async Task<FolderRecord> GetFolderAsync(string? prefix, CancellationToken cancellationToken = default)
    {
        string normalized = (prefix ?? string.Empty).Replace('\\', '/').Trim('/');
        if (normalized.Length > 0 && !SeesEverything)
        {
            AccessRequirement requirement = await requirements.ForFolderAsync(normalized, cancellationToken);
            if (!IsVisible(requirement))
            {
                return folders.Unknown(normalized);
            }
        }

        FolderRecord record = await folders.GetAsync(normalized, cancellationToken);
        return normalized.Length == 0 ? counts.ComposeRootRecord(record, Visibility) : record;
    }

    /// <summary>The site-wide counts this reader may see, or null while the index has no root cell.</summary>
    public FolderMetrics? GetTotal() => counts.ComposeRoot(Visibility);

    /// <summary>
    /// The verdict for a node coming from <paramref name="sourceFolder"/>, a mounted content folder.
    /// The owning space is found exactly as the mounted content source finds it.
    /// </summary>
    private bool IsFolderVisible(string? sourceFolder)
    {
        if (sourceFolder is null)
        {
            return false;
        }

        string normalized = sourceFolder.Replace('\\', '/').Trim('/');
        return spaces.TryResolve(normalized, out SpaceOptions space, out _)
            ? IsSpaceVisible(space.Id)
            : Visibility.OutsideVisible;
    }
}
