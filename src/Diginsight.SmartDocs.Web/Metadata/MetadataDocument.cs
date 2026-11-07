using System.Collections.Immutable;
using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Shared.Navigation;

namespace Diginsight.SmartDocs.Web.Metadata;

public enum MetadataStatus
{
    Missing,
    Valid,
    Invalid,
    Unreadable,
}

/// <summary>Repository authoring information; it never grants runtime access.</summary>
public sealed record RepositoryMetadata(
    string? Id,
    string? Visibility,
    ImmutableDictionary<string, string?> InternalPeer,
    ImmutableArray<string> DeliberatelyPublic);

/// <summary>A parsed document and its distinct, server-side projections.</summary>
public sealed record MetadataDocument(
    bool Versioned,
    FolderMeta Navigation,
    RepositoryMetadata? Repository,
    AccessFile? Access,
    string Fingerprint,
    ImmutableArray<string> Warnings);

public sealed record MetadataOutcome(
    MetadataStatus Status,
    MetadataDocument? Document,
    string? Problem,
    string? Fingerprint)
{
    public static MetadataOutcome Missing { get; } = new(MetadataStatus.Missing, null, null, null);

    public static MetadataOutcome Valid(MetadataDocument document) =>
        new(MetadataStatus.Valid, document, null, document.Fingerprint);

    public static MetadataOutcome Invalid(string problem, string? fingerprint = null) =>
        new(MetadataStatus.Invalid, null, problem, fingerprint);

    public static MetadataOutcome Unreadable(string problem) =>
        new(MetadataStatus.Unreadable, null, problem, null);
}

public interface IMetadataDocumentParser
{
    MetadataOutcome Parse(byte[] bytes, bool isContentRoot);

    AccessFileOutcome ParseLegacyAccess(byte[] bytes);
}
