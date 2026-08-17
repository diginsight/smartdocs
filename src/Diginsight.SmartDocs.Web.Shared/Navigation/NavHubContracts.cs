namespace Diginsight.SmartDocs.Web.Shared.Navigation;

/// <summary>
/// A single folder's authoritative recursive aggregate, pushed from the server to connected clients
/// over the nav hub so the sidebar counts and footer total update in real time without polling.
/// <para>
/// The value is <em>absolute</em> (not a delta): the client replaces the cached count on the node
/// whose <see cref="Prefix"/> matches. A change publishes one entry for the changed folder and one
/// for each of its ancestors up to the root, so every ancestor keeps its own count in sync.
/// </para>
/// </summary>
public sealed record NavAggregateDelta(
    string Prefix,
    int ArticleCount,
    DateTimeOffset? LatestUtc,
    string? Author,
    Coverage Coverage = Coverage.Complete);

/// <summary>Well-known nav hub route and server→client method names (shared by server and WASM client).</summary>
public static class NavHubContract
{
    /// <summary>Hub endpoint route mapped by the server and connected to by the client.</summary>
    public const string Route = "/_nav/hub";

    /// <summary>Server→client: the changed folder plus each ancestor's new absolute aggregate.</summary>
    public const string MetadataChanged = "MetadataChanged";

    /// <summary>Server→client: all root sections' aggregates, sent once the startup warm-up finishes.</summary>
    public const string CountsReady = "CountsReady";
}
