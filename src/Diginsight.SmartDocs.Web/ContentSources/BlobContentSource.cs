using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using log4net.Repository.Hierarchy;

namespace Diginsight.SmartDocs.Web.ContentSources;

/// <summary>
/// Reads content from the storage account container over HTTPS using a managed/CLI identity.
/// Used in production; the browser never sees storage credentials.
/// </summary>
public sealed class BlobContentSource : IContentSource, IConditionalContentSource, IContentLister
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<BlobContentSource> logger;

    public BlobContentSource(string accountUri, string containerName, ILogger<BlobContentSource> logger)
    {
        this.logger = logger;
        using var activity = Observability.ActivitySource.StartMethodActivity(this.logger, () => new { containerName });

        var account = new Uri(accountUri.TrimEnd('/') + "/");
        var containerUri = new Uri(account, containerName);
        // Excluded: a VS sign-in from a different tenant fails hard instead of falling back,
        // and once DefaultAzureCredential latches onto it every subsequent call keeps failing.
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ExcludeVisualStudioCredential = true });
        _container = new BlobContainerClient(containerUri, credential);
    }

    public async Task<ContentResult?> GetAsync(string contentKey, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { contentKey });
        ContentValidationResult result = await DownloadAsync(contentKey, null, ct);
        activity?.SetOutput(new
        {
            found = result.Content is not null,
            contentType = result.Content?.ContentType,
            length = result.Content?.Bytes.Length ?? 0,
        });
        return result.Content;
    }

    public async Task<ContentValidationResult> GetIfChangedAsync(
        string contentKey, string etag, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { contentKey });
        ContentValidationResult result = await DownloadAsync(contentKey, etag, ct);
        activity?.SetOutput(new { result.NotModified, found = result.NotModified || result.Content is not null });
        return result;
    }

    private async Task<ContentValidationResult> DownloadAsync(
        string contentKey, string? etag, CancellationToken ct)
    {
        if (!TryGetBlob(contentKey, out BlobClient? blob))
        {
            return ContentValidationResult.Missing;
        }

        Response<BlobDownloadResult> response;
        try
        {
            var options = new BlobDownloadOptions
            {
                Conditions = etag is null ? null : new BlobRequestConditions { IfNoneMatch = new ETag(etag) },
            };
            response = await blob.DownloadContentAsync(options, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 304)
        {
            return ContentValidationResult.Unchanged;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return ContentValidationResult.Missing;
        }

        byte[] bytes = response.Value.Content.ToArray();
        string contentType = string.IsNullOrEmpty(response.Value.Details.ContentType)
            ? "text/plain; charset=utf-8"
            : response.Value.Details.ContentType;
        var result = new ContentResult(bytes, contentType, response.Value.Details.ETag.ToString());
        return ContentValidationResult.Changed(result);
    }

    public async Task<IReadOnlyList<ChildEntry>> ListChildrenAsync(string prefix, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { prefix });

        string p = (prefix ?? string.Empty).Replace('\\', '/').TrimStart('/');
        if (!ContentKeyGuard.IsSafe(p))
        {
            return [];
        }

        if (p.Length > 0 && !p.EndsWith('/'))
        {
            p += "/";
        }

        var items = new List<ChildEntry>();
        await foreach (BlobHierarchyItem item in _container.GetBlobsByHierarchyAsync(
                           BlobTraits.None, BlobStates.None, delimiter: "/", prefix: p, cancellationToken: ct))
        {
            if (item.IsPrefix)
            {
                string full = item.Prefix.TrimEnd('/');
                string name = full.Contains('/') ? full[(full.LastIndexOf('/') + 1)..] : full;
                items.Add(new ChildEntry(name, true, full));
            }
            else
            {
                string full = item.Blob.Name;
                string name = full.Contains('/') ? full[(full.LastIndexOf('/') + 1)..] : full;
                items.Add(new ChildEntry(name, false, full));
            }
        }

        activity?.SetOutput(new { count = items.Count });
        return items;
    }

    public async Task<string?> ReadHeadAsync(string key, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { key });

        if (!TryGetBlob(key, out BlobClient? blob))
        {
            return null;
        }

        try
        {
            // Ranged read: pull only the head of the blob, enough for the frontmatter block.
            Response<BlobDownloadStreamingResult> resp =
                await blob.DownloadStreamingAsync(new BlobDownloadOptions { Range = new HttpRange(0, 64 * 1024) }, ct);
            await using Stream s = resp.Value.Content;
            string? head = await FrontMatter.ReadHeadAsync(s, ct);
            activity?.SetOutput(new { found = head is not null, length = head?.Length ?? 0 });
            return head;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    /// <summary>
    /// The blob a key names in this container, or false for a key the SDK could resolve elsewhere:
    /// it collapses dot segments when it builds the blob URI, so <c>../other/x.md</c> would address
    /// the container <c>other</c>. Keys are refused by <see cref="ContentKeyGuard"/> first, and the
    /// resulting client must still name this container.
    /// </summary>
    private bool TryGetBlob(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BlobClient? blob)
    {
        blob = null;
        if (!ContentKeyGuard.IsSafe(key))
        {
            return false;
        }

        BlobClient candidate = _container.GetBlobClient(key);
        if (!string.Equals(candidate.BlobContainerName, _container.Name, StringComparison.Ordinal))
        {
            logger.LogWarning("Refused content key '{Key}': it resolves outside container '{Container}'.", key, _container.Name);
            return false;
        }

        blob = candidate;
        return true;
    }
}
