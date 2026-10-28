using System.Collections.Immutable;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.Authorization;

/// <summary>
/// What a reader needs to see one node: the permissions of the space that owns it, all of them.
/// <para>
/// A requirement names its owning space, or none for a path outside every space, because a
/// permission is the pair of a space and a name: a grant in one space never counts in another.
/// <see cref="Unresolvable"/> is the requirement of a space whose access file couldn't be read or
/// is invalid; no set of permissions covers it, so such a space is hidden from everyone.
/// </para>
/// <para>
/// This type stays on the server. The browser never evaluates access; it receives only what the
/// server has already filtered.
/// </para>
/// </summary>
public sealed class AccessRequirement
{
    /// <summary>The permission every signed-in reader holds in every space. Reserved: no access file may declare or grant it.</summary>
    public const string Authenticated = SiteAuthorizationOptions.Authenticated;

    private static readonly ImmutableSortedSet<string> NoPermissions =
        ImmutableSortedSet.Create<string>(StringComparer.OrdinalIgnoreCase);

    private AccessRequirement(string? spaceId, ImmutableSortedSet<string> permissions, bool isUnresolvable)
    {
        SpaceId = spaceId;
        Permissions = permissions;
        IsUnresolvable = isUnresolvable;
    }

    /// <summary>Requires nothing: every reader, anonymous included, is covered.</summary>
    public static AccessRequirement Empty { get; } = new(null, NoPermissions, false);

    /// <summary>Covered by no reader, whatever they hold.</summary>
    public static AccessRequirement Unresolvable { get; } = new(null, NoPermissions, true);

    /// <summary>The owning space's id, or null for a path outside every space.</summary>
    public string? SpaceId { get; }

    /// <summary>The permissions required, case-insensitive and sorted.</summary>
    public ImmutableSortedSet<string> Permissions { get; }

    /// <summary>True when no reader can meet this requirement.</summary>
    public bool IsUnresolvable { get; }

    /// <summary>True when every reader, anonymous included, meets this requirement.</summary>
    public bool IsEmpty => !IsUnresolvable && Permissions.IsEmpty;

    /// <summary>A requirement of <paramref name="spaceId"/>, or of no space, for every name in <paramref name="permissions"/>.</summary>
    public static AccessRequirement Of(string? spaceId, IEnumerable<string> permissions)
    {
        ImmutableSortedSet<string> set = ImmutableSortedSet.CreateRange(StringComparer.OrdinalIgnoreCase, permissions);
        return spaceId is null && set.IsEmpty ? Empty : new(spaceId, set, false);
    }

    /// <summary>The requirement of a space that no reader may see until its access file is fixed.</summary>
    public static AccessRequirement UnresolvableIn(string? spaceId) =>
        spaceId is null ? Unresolvable : new(spaceId, NoPermissions, true);

    /// <summary>
    /// True when <paramref name="permissions"/>, held in this requirement's space, include every
    /// required permission. Names compare case-insensitively whatever comparer the caller's set uses.
    /// </summary>
    public bool IsCoveredBy(IEnumerable<string>? permissions)
    {
        if (IsUnresolvable)
        {
            return false;
        }

        if (Permissions.IsEmpty)
        {
            return true;
        }

        if (permissions is null)
        {
            return false;
        }

        IReadOnlySet<string> held = permissions is ImmutableSortedSet<string> sorted
                                    && ReferenceEquals(sorted.KeyComparer, StringComparer.OrdinalIgnoreCase)
            ? sorted
            : new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);

        foreach (string required in Permissions)
        {
            if (!held.Contains(required))
            {
                return false;
            }
        }

        return true;
    }

    public override string ToString() =>
        (SpaceId ?? "(no space)") + ": " +
        (IsUnresolvable ? "unresolvable" : Permissions.IsEmpty ? "none" : string.Join(", ", Permissions));
}
