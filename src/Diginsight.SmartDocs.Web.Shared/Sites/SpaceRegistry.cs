namespace Diginsight.SmartDocs.Web.Shared.Sites;

/// <summary>
/// Resolves a request path to the space that owns it and the content key within that space.
/// <para>
/// This type lives in the shared project deliberately: the server runs it during prerender and the
/// WebAssembly client runs it after hydration, and the two must agree. Selecting a space from
/// <c>IHttpContextAccessor</c> inside a DI factory would resolve correctly during prerender and not
/// at all in the browser, replacing the prerendered article at hydration.
/// </para>
/// </summary>
public sealed class SpaceRegistry
{
    private readonly IReadOnlyList<SpaceOptions> prefixed; // longest route base first
    private readonly SpaceOptions? root;

    public SpaceRegistry(IEnumerable<SpaceOptions> spaces)
    {
        All = spaces.ToList();
        Validate(All);

        root = All.FirstOrDefault(static s => s.IsRootMounted);
        prefixed = All.Where(static s => !s.IsRootMounted)
            .OrderByDescending(static s => s.NormalizedRouteBase.Length)
            .ToList();
    }

    public IReadOnlyList<SpaceOptions> All { get; }

    /// <summary>True when no space claims the site root, so <c>/</c> serves the generated index.</summary>
    public bool ServesIndexAtRoot => root is null;

    public SpaceOptions? ById(string id) =>
        All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Splits an incoming path into the owning space and the content key beneath it. Longest route
    /// base wins, so <c>/diginsight.tools/x</c> is matched before a root-mounted space's catch-all.
    /// Returns false when no space claims the path — the caller then serves the generated index.
    /// </summary>
    public bool TryResolve(string? path, out SpaceOptions space, out string contentKey)
    {
        string normalized = "/" + (path ?? string.Empty).Trim('/');

        foreach (SpaceOptions candidate in prefixed)
        {
            string b = candidate.NormalizedRouteBase;
            bool matches = normalized.Equals(b, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(b + "/", StringComparison.OrdinalIgnoreCase);
            if (!matches)
            {
                continue;
            }

            space = candidate;
            contentKey = normalized.Length > b.Length ? normalized[(b.Length + 1)..] : string.Empty;
            return true;
        }

        if (root is not null)
        {
            space = root;
            contentKey = normalized.Trim('/');
            return true;
        }

        space = null!;
        contentKey = string.Empty;
        return false;
    }

    /// <summary>Prepends the space's route base to an in-space path, for links emitted into rendered HTML.</summary>
    public static string ToRoute(SpaceOptions space, string contentKey) =>
        space.IsRootMounted
            ? "/" + contentKey.TrimStart('/')
            : space.NormalizedRouteBase + "/" + contentKey.TrimStart('/');

    /// <summary>
    /// Startup validation for the four routing rules. Throws rather than starting a host whose URL
    /// space is ambiguous — a site that silently picks one of two root claims is worse than one that
    /// refuses to boot.
    /// </summary>
    private static void Validate(IReadOnlyList<SpaceOptions> spaces)
    {
        if (spaces.Count == 0)
        {
            throw new InvalidOperationException("Site:Spaces is empty. Configure at least one space.");
        }

        foreach (SpaceOptions s in spaces)
        {
            if (string.IsNullOrWhiteSpace(s.Id))
            {
                throw new InvalidOperationException("Every space requires a non-empty Id.");
            }

            if (!s.IsRootMounted && s.NormalizedRouteBase.Trim('/').Contains('/'))
            {
                throw new InvalidOperationException(
                    $"Space '{s.Id}' has RouteBase '{s.RouteBase}'. A route base must be a single path segment.");
            }

            bool blob = string.Equals(s.Source, "Blob", StringComparison.OrdinalIgnoreCase);
            bool file = string.Equals(s.Source, "FileSystem", StringComparison.OrdinalIgnoreCase);
            if (!blob && !file)
            {
                throw new InvalidOperationException(
                    $"Space '{s.Id}' has Source '{s.Source}'. Expected 'Blob' or 'FileSystem'.");
            }

            if (blob && string.IsNullOrWhiteSpace(s.Blob.ContainerName))
            {
                throw new InvalidOperationException($"Space '{s.Id}' uses Blob but no ContainerName is configured.");
            }
        }

        string[] duplicateIds = spaces
            .GroupBy(static s => s.Id, StringComparer.OrdinalIgnoreCase)
            .Where(static g => g.Count() > 1)
            .Select(static g => g.Key)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate space Id(s): {string.Join(", ", duplicateIds)}.");
        }

        SpaceOptions[] rootClaims = spaces.Where(static s => s.IsRootMounted).ToArray();
        if (rootClaims.Length > 1)
        {
            throw new InvalidOperationException(
                "More than one space claims the site root: " +
                string.Join(", ", rootClaims.Select(static s => s.Id)) +
                ". At most one space may have RouteBase '/'.");
        }

        string[] duplicateBases = spaces
            .Where(static s => !s.IsRootMounted)
            .GroupBy(static s => s.NormalizedRouteBase, StringComparer.OrdinalIgnoreCase)
            .Where(static g => g.Count() > 1)
            .Select(static g => g.Key)
            .ToArray();
        if (duplicateBases.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate RouteBase value(s): {string.Join(", ", duplicateBases)}.");
        }
    }
}
