using System.Collections.Concurrent;
using System.Collections.Immutable;
using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.Authorization;

/// <summary>
/// Decides which spaces a <see cref="RoleSet"/> may see. Shared by every request and by the push hub,
/// so one rule decides visibility on every surface.
/// <para>
/// In each space an anonymous reader holds no permission, and a signed-in reader holds
/// <c>authenticated</c> plus whatever that space's own access file maps any of their roles to. Roles
/// combine by union, and a grant in one space never counts in another. A space is visible when those
/// permissions cover its effective requirement; a node outside every space is checked against the
/// site default, which only <c>authenticated</c> can cover.
/// </para>
/// <para>
/// The evaluator keeps no cache and no memo: each evaluation reads the access files through their
/// provider's content cache and combines them with the current configuration, so a changed file or
/// setting applies at the next check, and nothing derived from a principal is ever stored.
/// </para>
/// </summary>
public sealed class SpaceVisibilityEvaluator(
    SpaceRegistry spaces,
    AccessRequirementResolver resolver,
    ILogger<SpaceVisibilityEvaluator> logger)
{
    private static readonly ImmutableSortedSet<string> NoPermissions =
        ImmutableSortedSet.Create<string>(StringComparer.OrdinalIgnoreCase);

    private static readonly ImmutableSortedSet<string> AuthenticatedOnly =
        ImmutableSortedSet.Create(StringComparer.OrdinalIgnoreCase, AccessRequirement.Authenticated);

    private const int MaxWarnedUnmappedRoles = 256;

    // Process-wide, like the warning it suppresses: a role value, never anything tied to a reader.
    private readonly ConcurrentDictionary<string, byte> warnedUnmappedRoles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Evaluates every configured space, and the nodes outside every space, for <paramref name="roles"/>.</summary>
    public async Task<ReaderVisibility> EvaluateAsync(RoleSet roles, CancellationToken cancellationToken = default)
    {
        using var activity = Observability.HotPathActivitySource.StartMethodActivity(logger, () => new { roles = roles.ToString() });

        (SpaceVisibility Visibility, AccessFile? File)[] evaluated = await Task.WhenAll(
            spaces.All.Select(space => EvaluateSpaceAsync(space, roles, cancellationToken)));

        WarnUnmappedRoles(roles, evaluated);

        ImmutableSortedSet<string> outsidePermissions = BasePermissions(roles);
        AccessRequirement outsideRequirement = resolver.ForOutsideSpaces();
        var visibility = new ReaderVisibility(
            roles,
            evaluated.ToImmutableDictionary(static v => v.Visibility.SpaceId, static v => v.Visibility, StringComparer.OrdinalIgnoreCase),
            outsidePermissions,
            outsideRequirement.IsCoveredBy(outsidePermissions));

        activity?.SetOutput(new { visible = string.Join(", ", visibility.VisibleSpaceIds), visibility.OutsideVisible });
        return visibility;
    }

    /// <summary>Whether <paramref name="roles"/> may see the space <paramref name="spaceId"/>.</summary>
    public async Task<bool> IsSpaceVisibleAsync(string spaceId, RoleSet roles, CancellationToken cancellationToken = default) =>
        spaces.ById(spaceId) is { } space && (await EvaluateSpaceAsync(space, roles, cancellationToken)).Visibility.IsVisible;

    private async Task<(SpaceVisibility Visibility, AccessFile? File)> EvaluateSpaceAsync(SpaceOptions space, RoleSet roles, CancellationToken cancellationToken)
    {
        // One read of the access file yields both the requirement and the role map, so a file that
        // changes between two reads can never pair one version's requirement with another's grants.
        SpaceAccessState state = await resolver.ResolveSpaceAsync(space.Id, cancellationToken);
        ImmutableSortedSet<string> permissions = PermissionsIn(state.File, roles);
        return (new SpaceVisibility(space.Id, state.Requirement, permissions, state.Requirement.IsCoveredBy(permissions)), state.File);
    }

    /// <summary>
    /// Logs, once per value per process, a role value that no space with a valid access file maps.
    /// Such a value grants nothing anywhere, and the reader's other values still count. Role values
    /// come only from tokens Entra issued for this application, so the set of logged values is
    /// bounded by its app roles; the cap only guards against a misconfigured issuer.
    /// </summary>
    private void WarnUnmappedRoles(RoleSet roles, (SpaceVisibility Visibility, AccessFile? File)[] evaluated)
    {
        if (!roles.IsSignedIn || roles.Roles.IsEmpty)
        {
            return;
        }

        foreach (string role in roles.Roles)
        {
            if (warnedUnmappedRoles.ContainsKey(role) || warnedUnmappedRoles.Count >= MaxWarnedUnmappedRoles)
            {
                continue;
            }

            if (evaluated.Any(e => e.File is not null && e.File.Roles.ContainsKey(role)))
            {
                continue;
            }

            if (warnedUnmappedRoles.TryAdd(role, 0))
            {
                logger.LogWarning(
                    "The role '{Role}' is mapped by no space with a valid access file, so it grants nothing; the reader's other roles still count",
                    role);
            }
        }
    }

    /// <summary>What <paramref name="roles"/> hold in a space whose valid access file is <paramref name="file"/>.</summary>
    private static ImmutableSortedSet<string> PermissionsIn(AccessFile? file, RoleSet roles)
    {
        ImmutableSortedSet<string> held = BasePermissions(roles);
        if (file is null || !roles.IsSignedIn || roles.Roles.IsEmpty)
        {
            return held;
        }

        ImmutableSortedSet<string>.Builder builder = held.ToBuilder();
        foreach (string role in roles.Roles)
        {
            builder.UnionWith(file.Grants(role));
        }

        return builder.ToImmutable();
    }

    private static ImmutableSortedSet<string> BasePermissions(RoleSet roles) =>
        roles.IsSignedIn ? AuthenticatedOnly : NoPermissions;
}

/// <summary>One space's outcome for one role set: its requirement, what the reader holds there, and the verdict.</summary>
public sealed record SpaceVisibility(
    string SpaceId,
    AccessRequirement Requirement,
    ImmutableSortedSet<string> Permissions,
    bool IsVisible);

/// <summary>Every space's outcome for one role set, plus the verdict for nodes outside every space.</summary>
public sealed record ReaderVisibility(
    RoleSet Roles,
    ImmutableDictionary<string, SpaceVisibility> Spaces,
    ImmutableSortedSet<string> OutsidePermissions,
    bool OutsideVisible)
{
    /// <summary>The ids of the spaces the reader may see.</summary>
    public IEnumerable<string> VisibleSpaceIds => Spaces.Values.Where(static s => s.IsVisible).Select(static s => s.SpaceId);

    /// <summary>True when the reader sees every space and every node outside them, so no filter changes anything.</summary>
    public bool SeesEverything => OutsideVisible && Spaces.Values.All(static s => s.IsVisible);

    /// <summary>
    /// True when this reader's permissions in the space that owns <paramref name="requirement"/> cover
    /// it. The space must also be visible in this evaluation, so a requirement computed from a newer
    /// read of the access file can only hide more, never open a space this evaluation closed.
    /// </summary>
    public bool IsVisible(AccessRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        if (requirement.SpaceId is null)
        {
            return OutsideVisible && requirement.IsCoveredBy(OutsidePermissions);
        }

        return Spaces.TryGetValue(requirement.SpaceId, out SpaceVisibility? space)
            && space.IsVisible
            && requirement.IsCoveredBy(space.Permissions);
    }
}
