namespace Diginsight.SmartDocs.Web.Shared;

/// <summary>
/// Abstracts where raw content bytes (Markdown, images, css, …) come from, so the same
/// rendering pipeline runs unchanged over blob storage (server), the local repo clone
/// (developer machine), or an HTTP endpoint (WASM client).
/// </summary>
public interface IContentSource
{
    /// <summary>Returns the bytes for a content key, or <c>null</c> when it does not exist.</summary>
    Task<ContentResult?> GetAsync(string contentKey, CancellationToken ct = default);
}

/// <summary>Raw content fetched from an <see cref="IContentSource"/>.</summary>
public sealed record ContentResult(byte[] Bytes, string? ContentType, string ETag);
