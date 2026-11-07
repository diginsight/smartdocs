using Diginsight.SmartDocs.Web.Authorization;

namespace Diginsight.SmartDocs.Web.Metadata;

/// <summary>Compares policy content independently of YAML layout and case-insensitive names.</summary>
public static class MetadataPolicyComparer
{
    public static bool Equivalent(AccessFile left, AccessFile right) =>
        left.Permissions.SetEquals(right.Permissions) &&
        left.Requires.SetEquals(right.Requires) &&
        left.PermissionDescriptions.All(pair =>
            right.PermissionDescriptions.TryGetValue(pair.Key, out string? description) &&
            string.Equals(pair.Value, description, StringComparison.Ordinal)) &&
        left.Roles.Count == right.Roles.Count &&
        left.Roles.All(pair =>
            right.Roles.TryGetValue(pair.Key, out var grants) && pair.Value.SetEquals(grants));
}
