using System.Security.Cryptography;
using Diginsight.Diagnostics;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Diginsight.SmartDocs.Web.Shared;
using Diginsight.SmartDocs.Web.Shared.Navigation;

namespace Diginsight.SmartDocs.Web.ContentSources;

/// <summary>
/// Reads content from the local repo clone. Used on the developer machine so the app renders
/// straight from source with no storage credentials.
/// </summary>
public sealed class FileSystemContentSource : IContentSource, IContentLister
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();
    private readonly string _root;
    private readonly ILogger<FileSystemContentSource> _logger;

    public FileSystemContentSource(string rootPath, ILogger<FileSystemContentSource> logger)
    {
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        _logger = logger;
    }

    public async Task<ContentResult?> GetAsync(string contentKey, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(_logger, () => new { contentKey });

        string relative = contentKey.Replace('\\', '/').TrimStart('/');
        string full = Path.GetFullPath(Path.Combine(_root, relative));

        // Path-traversal guard: never serve outside the configured root (OWASP A01/A05).
        string boundary = _root + Path.DirectorySeparatorChar;
        if (!full.Equals(_root, StringComparison.OrdinalIgnoreCase) &&
            !full.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!File.Exists(full))
        {
            return null;
        }

        byte[] bytes = await File.ReadAllBytesAsync(full, ct);
        string contentType = ContentTypes.TryGetContentType(full, out string? mime) && mime is not null
            ? mime
            : "text/plain; charset=utf-8";
        string etag = "\"" + Convert.ToHexString(SHA1.HashData(bytes)) + "\"";
        var result = new ContentResult(bytes, contentType, etag);
        activity?.SetOutput(new { contentType, length = bytes.Length });
        return result;
    }

    public Task<IReadOnlyList<ChildEntry>> ListChildrenAsync(string prefix, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(_logger, () => new { prefix });

        string rel = (prefix ?? string.Empty).Replace('\\', '/').Trim('/');
        string dir = string.IsNullOrEmpty(rel) ? _root : Path.GetFullPath(Path.Combine(_root, rel));

        string boundary = _root + Path.DirectorySeparatorChar;
        if (!dir.Equals(_root, StringComparison.OrdinalIgnoreCase) &&
            !dir.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<ChildEntry>>(Array.Empty<ChildEntry>());
        }

        var items = new List<ChildEntry>();
        if (Directory.Exists(dir))
        {
            string basePrefix = string.IsNullOrEmpty(rel) ? string.Empty : rel + "/";
            foreach (string d in Directory.EnumerateDirectories(dir))
            {
                string name = Path.GetFileName(d);
                items.Add(new ChildEntry(name, true, basePrefix + name));
            }

            foreach (string f in Directory.EnumerateFiles(dir))
            {
                string name = Path.GetFileName(f);
                items.Add(new ChildEntry(name, false, basePrefix + name));
            }
        }

        activity?.SetOutput(new { count = items.Count });
        return Task.FromResult<IReadOnlyList<ChildEntry>>(items);
    }

    public async Task<string?> ReadHeadAsync(string key, CancellationToken ct = default)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(_logger, () => new { key });

        string rel = (key ?? string.Empty).Replace('\\', '/').TrimStart('/');
        string full = Path.GetFullPath(Path.Combine(_root, rel));

        string boundary = _root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(boundary, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        {
            return null;
        }

        await using FileStream fs = File.OpenRead(full);
        string? head = await FrontMatter.ReadHeadAsync(fs, ct);
        activity?.SetOutput(new { found = head is not null, length = head?.Length ?? 0 });
        return head;
    }
}
