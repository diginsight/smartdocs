using System.Collections.Concurrent;
using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.ContentSources;

/// <summary>
/// Presents every configured space as one path namespace: each prefixed space is mounted under its
/// route-base segment (<c>apidevice/…</c>), and a root-mounted space, if any, owns everything else.
/// <para>
/// Each space keeps its own physical source (file system or blob container). Only the keys are
/// joined. Every layer above this one — the SmartCache entries, the navigation levels, the folder
/// metrics, the invalidation rules and the hub pushes — is addressed by content path, so a key that
/// carries its space's segment is unique across the site, and none of those layers needs a separate
/// space dimension.
/// </para>
/// </summary>
public sealed class SpaceMountedContentSource : IContentSource, IContentLister
{
    private readonly SpaceRegistry registry;
    private readonly IReadOnlyDictionary<string, SpaceContentAccess> bySpaceId;
    private readonly ILogger<SpaceMountedContentSource> logger;
    private readonly ConcurrentDictionary<string, byte> warnedNestedAccessFiles = new(StringComparer.Ordinal);

    public SpaceMountedContentSource(
        SpaceRegistry registry, SpaceContentRegistry spaces, ILogger<SpaceMountedContentSource> logger)
    {
        this.registry = registry;
        bySpaceId = spaces.All.ToDictionary(static a => a.Space.Id, StringComparer.OrdinalIgnoreCase);
        this.logger = logger;
    }

    public Task<ContentResult?> GetAsync(string contentKey, CancellationToken ct = default)
    {
        using var activity = Observability.HotPathActivitySource.StartMethodActivity(logger, () => new { contentKey });

        // An access file reveals which permissions and roles open its space, so no content surface
        // ever serves one, at any depth. AccessFileProvider reads it from the physical source instead.
        if (AccessFile.IsAccessFileKey(contentKey))
        {
            return Task.FromResult<ContentResult?>(null);
        }

        return TryMap(contentKey, out SpaceContentAccess? access, out string inner)
            ? access.Source.GetAsync(inner, ct)
            : Task.FromResult<ContentResult?>(null);
    }

    public async Task<IReadOnlyList<ChildEntry>> ListChildrenAsync(string prefix, CancellationToken ct = default)
    {
        using var activity = Observability.HotPathActivitySource.StartMethodActivity(logger, () => new { prefix });

        string rel = Normalize(prefix);
        if (rel.Length == 0)
        {
            return await ListSiteRootAsync(ct);
        }

        if (!TryMap(rel, out SpaceContentAccess? access, out string inner))
        {
            return [];
        }

        IReadOnlyList<ChildEntry> children = WithoutAccessFiles(access.Space, await access.Lister.ListChildrenAsync(inner, ct) ?? []);
        if (access.Space.IsRootMounted)
        {
            return children;
        }

        string mount = Segment(access.Space);
        return children
            .Select(c => c with { Path = mount + "/" + c.Path.Replace('\\', '/').TrimStart('/') })
            .ToList();
    }

    public Task<string?> ReadHeadAsync(string key, CancellationToken ct = default)
    {
        using var activity = Observability.HotPathActivitySource.StartMethodActivity(logger, () => new { key });

        if (AccessFile.IsAccessFileKey(key))
        {
            return Task.FromResult<string?>(null);
        }

        return TryMap(key, out SpaceContentAccess? access, out string inner)
            ? access.Lister.ReadHeadAsync(inner, ct)
            : Task.FromResult<string?>(null);
    }

    /// <summary>
    /// The site root: one folder per prefixed space, plus the root-mounted space's own top level.
    /// A root-space entry that shares its name with a mount is hidden — the mount reserves the segment.
    /// </summary>
    private async Task<IReadOnlyList<ChildEntry>> ListSiteRootAsync(CancellationToken ct)
    {
        var items = new List<ChildEntry>();

        if (registry.Root is { } root && bySpaceId.TryGetValue(root.Id, out SpaceContentAccess? rootAccess))
        {
            IReadOnlyList<ChildEntry> own = WithoutAccessFiles(root, await rootAccess.Lister.ListChildrenAsync(string.Empty, ct) ?? []);
            items.AddRange(own.Where(c => registry.MountedAt(c.Name) is null));
        }

        foreach (SpaceOptions space in registry.Prefixed)
        {
            string mount = Segment(space);
            items.Add(new ChildEntry(mount, true, mount));
        }

        return items;
    }

    /// <summary>
    /// Drops access files from one level of a space's listing. A file below the space's content root
    /// is logged once per path: version 1 reads only the root file, so a misplaced one must not look
    /// as if it restricted its folder. Only listings warn — they report files that exist — so request
    /// traffic for invented keys can't flood the log.
    /// </summary>
    private IReadOnlyList<ChildEntry> WithoutAccessFiles(SpaceOptions space, IReadOnlyList<ChildEntry> children)
    {
        List<ChildEntry>? kept = null;
        for (int i = 0; i < children.Count; i++)
        {
            ChildEntry child = children[i];
            if (!AccessFile.IsAccessFileKey(child.Name))
            {
                kept?.Add(child);
                continue;
            }

            kept ??= children.Take(i).ToList();
            string inner = Normalize(child.Path);
            if (AccessFile.IsNestedAccessFileKey(inner)
                && warnedNestedAccessFiles.TryAdd($"{space.Id}/{inner}".ToLowerInvariant(), 0))
            {
                logger.LogWarning(
                    "Space '{SpaceId}' has {FileName} at '{Path}', below its content root. Version 1 reads only the file at the content root, so this one restricts nothing. It is never served.",
                    space.Id,
                    AccessFile.FileName,
                    inner);
            }
        }

        return kept ?? children;
    }

    private bool TryMap(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SpaceContentAccess? access, out string inner)
    {
        // A key with a dot, empty or encoded segment could be resolved by a store into another space
        // than the one its first segment names, which is the space the access check placed it in.
        if (ContentKeyGuard.IsSafe(key)
            && registry.TryResolve(Normalize(key), out SpaceOptions space, out inner)
            && bySpaceId.TryGetValue(space.Id, out access))
        {
            return true;
        }

        access = null;
        inner = string.Empty;
        return false;
    }

    private static string Segment(SpaceOptions space) => space.NormalizedRouteBase.Trim('/');

    private static string Normalize(string? path) => (path ?? string.Empty).Replace('\\', '/').Trim('/');
}
