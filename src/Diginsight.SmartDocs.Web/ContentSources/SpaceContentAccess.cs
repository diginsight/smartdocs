using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.ContentSources;

/// <summary>
/// The reader and the lister for one space, held as two explicitly typed members.
/// <para>
/// They are the same object today — <see cref="CachedContentSource"/>, <see cref="BlobContentSource"/>
/// and <see cref="FileSystemContentSource"/> all implement both interfaces. Capturing that as a
/// downcast (<c>(IContentLister)sp.GetRequiredService&lt;IContentSource&gt;()</c>) compiles whatever
/// the factory returns and fails only at runtime, inside a warm-up whose catch turns the
/// <see cref="InvalidCastException"/> into a log warning: articles render, the sidebar stays empty,
/// and the symptom reads like a storage fault. Naming both members makes the requirement a
/// compile-time one.
/// </para>
/// </summary>
public sealed record SpaceContentAccess(SpaceOptions Space, IContentSource Source, IContentLister Lister);

/// <summary>
/// Singleton registry of per-space singletons. Content sources stay singletons — the navigation
/// builders capture them — so the space is selected by an explicit argument rather than by a scoped
/// factory observing the request, which would be a captive dependency.
/// </summary>
public sealed class SpaceContentRegistry
{
    private readonly IReadOnlyDictionary<string, SpaceContentAccess> bySpaceId;

    public SpaceContentRegistry(IEnumerable<SpaceContentAccess> entries)
    {
        bySpaceId = entries.ToDictionary(static e => e.Space.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<SpaceContentAccess> All => bySpaceId.Values;

    public SpaceContentAccess Get(string spaceId) =>
        bySpaceId.TryGetValue(spaceId, out SpaceContentAccess? access)
            ? access
            : throw new InvalidOperationException($"No content access registered for space '{spaceId}'.");

    public bool TryGet(string spaceId, out SpaceContentAccess access) =>
        bySpaceId.TryGetValue(spaceId, out access!);
}
