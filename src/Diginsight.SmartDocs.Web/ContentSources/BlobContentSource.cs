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
public sealed class BlobContentSource : IContentSource, IContentLister
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<BlobContentSource> _logger;

    public BlobContentSource(string accountUri, string containerName, ILogger<BlobContentSource> logger)
    {
        _logger = logger;

        var account = new Uri(accountUri.TrimEnd('/') + "/");
        var containerUri = new Uri(account, containerName);
        // Excluded: a VS sign-in from a different tenant fails hard instead of falling back,
        // and once DefaultAzureCredential latches onto it every subsequent call keeps failing.
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ExcludeVisualStudioCredential = true });
        _container = new BlobContainerClient(containerUri, credential);
    }

    public async Task<ContentResult?> GetAsync(string contentKey, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(_logger, () => new { contentKey });

        BlobClient blob = _container.GetBlobClient(contentKey);
        if (!await blob.ExistsAsync(ct))
        {
            return null;
        }

        var response = await blob.DownloadContentAsync(ct);
        byte[] bytes = response.Value.Content.ToArray();
        string contentType = string.IsNullOrEmpty(response.Value.Details.ContentType)
            ? "text/plain; charset=utf-8"
            : response.Value.Details.ContentType;
        var result = new ContentResult(bytes, contentType, response.Value.Details.ETag.ToString());
        activity?.SetOutput(new { contentType, length = bytes.Length });
        return result;
    }

    public async Task<IReadOnlyList<ChildEntry>> ListChildrenAsync(string prefix, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(_logger, () => new { prefix });

        string p = (prefix ?? string.Empty).Replace('\\', '/').TrimStart('/');
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
        using var activity = Observability.ActivitySource.StartMethodActivity(_logger, () => new { key });

        BlobClient blob = _container.GetBlobClient(key);
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
}
