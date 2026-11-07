using Diginsight.SmartDocs.Web.Authorization;

namespace Diginsight.SmartDocs.Web.Metadata;

/// <summary>Recognizes canonical and transitional metadata names, including filesystem aliases.</summary>
public static class MetadataFiles
{
    public const string LegacyRepositoryFileName = "repository.metadata.yml";

    public static bool IsCanonicalKey(string? key) => AccessFile.IsKeyNamed(key, MetadataParser.FileName);

    public static bool IsLegacyRepositoryKey(string? key) => AccessFile.IsKeyNamed(key, LegacyRepositoryFileName);

    public static bool IsCarrierKey(string? key) =>
        IsCanonicalKey(key) ||
        AccessFile.IsAccessFileKey(key) ||
        IsLegacyRepositoryKey(key);

    public static string InvalidationScope(string? path)
    {
        string normalized = (path ?? string.Empty).Replace('\\', '/').Trim('/');
        if (!IsCarrierKey(normalized))
        {
            return normalized;
        }

        int slash = normalized.LastIndexOf('/');
        return slash < 0 ? string.Empty : normalized[..slash];
    }
}
