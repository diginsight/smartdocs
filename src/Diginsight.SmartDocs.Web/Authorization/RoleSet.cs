using System.Collections.Immutable;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Diginsight.SmartDocs.Web.Authorization;

/// <summary>
/// The only input authorization takes from a principal: whether the reader is signed in, plus the
/// case-insensitive set of their app role values. Readers with equal role sets see the same spaces,
/// so <see cref="Id"/> can key anything that is shared by such readers, such as a push group.
/// <para>
/// Roles only add: the set has no order, no precedence and no primary role, and repeats collapse.
/// </para>
/// </summary>
public sealed class RoleSet : IEquatable<RoleSet>
{
    private static readonly ImmutableSortedSet<string> NoRoles =
        ImmutableSortedSet.Create<string>(StringComparer.OrdinalIgnoreCase);

    private RoleSet(bool isSignedIn, ImmutableSortedSet<string> roles)
    {
        IsSignedIn = isSignedIn;
        Roles = roles;
        Id = ComputeId(isSignedIn, roles);
    }

    /// <summary>The reader who hasn't signed in: no permission in any space.</summary>
    public static RoleSet Anonymous { get; } = new(false, NoRoles);

    /// <summary>True when the reader is signed in, and therefore holds <c>authenticated</c> in every space.</summary>
    public bool IsSignedIn { get; }

    /// <summary>The reader's app role values, case-insensitive and sorted. Always empty for an anonymous reader.</summary>
    public ImmutableSortedSet<string> Roles { get; }

    /// <summary>A stable identifier derived from <see cref="IsSignedIn"/> and <see cref="Roles"/>, equal for equal sets.</summary>
    public string Id { get; }

    /// <summary>A signed-in reader holding <paramref name="roles"/>; blank values are ignored.</summary>
    public static RoleSet SignedIn(IEnumerable<string>? roles) =>
        new(true, ImmutableSortedSet.CreateRange(
            StringComparer.OrdinalIgnoreCase,
            (roles ?? []).Where(static r => !string.IsNullOrWhiteSpace(r)).Select(static r => r.Trim())));

    /// <summary>
    /// The role set of <paramref name="user"/>: anonymous unless an identity is authenticated, and
    /// otherwise every app role value the authenticated identities carry: in a <c>roles</c> claim as
    /// Entra ID issues it, in <see cref="ClaimTypes.Role"/> when inbound claim mapping renamed it, or
    /// in a claim of the identity's own role claim type. Role values only add, so collecting from
    /// every authenticated identity can't take anything away.
    /// </summary>
    public static RoleSet FromPrincipal(ClaimsPrincipal? user)
    {
        if (user?.Identities.Any(static i => i.IsAuthenticated) != true)
        {
            return Anonymous;
        }

        return SignedIn(user.Identities
            .Where(static i => i.IsAuthenticated)
            .SelectMany(static i => i.Claims.Where(c =>
                string.Equals(c.Type, RolesClaimType, StringComparison.Ordinal) ||
                string.Equals(c.Type, ClaimTypes.Role, StringComparison.Ordinal) ||
                string.Equals(c.Type, i.RoleClaimType, StringComparison.Ordinal)))
            .Select(static c => c.Value));
    }

    /// <summary>The claim type Entra ID issues app role values in.</summary>
    public const string RolesClaimType = "roles";

    // Roles compare case-insensitively, so the identifier is computed over their upper-invariant form:
    // "Reader" and "reader" are one role and must produce one identifier.
    private static string ComputeId(bool isSignedIn, ImmutableSortedSet<string> roles)
    {
        if (!isSignedIn)
        {
            return "anonymous";
        }

        string canonical = "signed-in\n" + string.Join('\n', roles.Select(static r => r.ToUpperInvariant()).Order(StringComparer.Ordinal));
        return "s-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)), 0, 16);
    }

    public bool Equals(RoleSet? other) => other is not null && string.Equals(Id, other.Id, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is RoleSet other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id);

    public override string ToString() =>
        IsSignedIn ? $"signed in [{string.Join(", ", Roles)}]" : "anonymous";
}
