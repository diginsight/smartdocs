using System.Collections.Immutable;
using Diginsight.Runtime;
using Diginsight.SmartDocs.Web.Authorization;
using Diginsight.SmartDocs.Web.Shared.Navigation;

namespace Diginsight.SmartDocs.Web.Metadata;

/// <summary>Serializable cache representation; permission comparers are restored after reading it.</summary>
public sealed record MetadataSnapshotEnvelope(
    MetadataStatus Status,
    bool Versioned,
    FolderMeta? Navigation,
    RepositorySnapshot? Repository,
    AccessSnapshot? Access,
    string? Fingerprint,
    string? Problem,
    string[] Warnings,
    string Origin,
    long Generation) : ISizeableHeuristically
{
    public static MetadataSnapshotEnvelope From(MetadataOutcome outcome, string origin, long generation)
    {
        if (outcome.Status == MetadataStatus.Unreadable)
        {
            throw new InvalidOperationException("Unreadable metadata must not be cached.");
        }

        MetadataDocument? document = outcome.Document;
        return new MetadataSnapshotEnvelope(
            outcome.Status,
            document?.Versioned ?? false,
            document?.Navigation,
            document?.Repository is { } repository
                ? new RepositorySnapshot(repository.Id, repository.Visibility,
                    repository.InternalPeer.Select(static p => new MetadataValue(p.Key, p.Value)).ToArray(),
                    repository.DeliberatelyPublic.ToArray())
                : null,
            document?.Access is { } access
                ? new AccessSnapshot(access.Fingerprint,
                    access.PermissionDescriptions.Select(static p => new MetadataValue(p.Key, p.Value)).ToArray(),
                    access.Requires.ToArray(),
                    access.Roles.Select(static p => new MetadataRole(p.Key, p.Value.ToArray())).ToArray())
                : null,
            outcome.Fingerprint,
            outcome.Problem,
            document?.Warnings.ToArray() ?? [],
            origin,
            generation);
    }

    public MetadataOutcome ToOutcome()
    {
        if (Status == MetadataStatus.Missing)
        {
            return MetadataOutcome.Missing;
        }

        if (Status == MetadataStatus.Invalid)
        {
            return MetadataOutcome.Invalid(Problem ?? "The cached metadata is invalid.", Fingerprint);
        }

        if (Status != MetadataStatus.Valid || Navigation is null || Fingerprint is null || Warnings is null)
        {
            return MetadataOutcome.Invalid("The cached metadata entry is incomplete or unrecognized.", Fingerprint);
        }

        try
        {
            var navigationValues = (Navigation.Values ?? new Dictionary<string, string>())
                .ToImmutableDictionary(static p => p.Key, static p => p.Value, StringComparer.OrdinalIgnoreCase);
            if (navigationValues.ContainsKey("access") || navigationValues.ContainsKey("repository"))
            {
                return MetadataOutcome.Invalid("The cached navigation projection contains a reserved key.", Fingerprint);
            }

            RepositoryMetadata? repository = Repository is null
                ? null
                : new RepositoryMetadata(
                    Repository.Id,
                    Repository.Visibility,
                    Repository.InternalPeer.ToImmutableDictionary(static p => p.Name, static p => p.Value, StringComparer.Ordinal),
                    Repository.DeliberatelyPublic.ToImmutableArray());
            AccessFile? access = Access is null
                ? null
                : AccessFile.Restore(
                    Access.Permissions.Select(static p => p.Name),
                    Access.Requires,
                    Access.Roles.Select(static r => new KeyValuePair<string, IEnumerable<string>>(r.Name, r.Permissions)),
                    Access.Fingerprint,
                    Access.Permissions.Select(static p => new KeyValuePair<string, string?>(p.Name, p.Value)));
            return MetadataOutcome.Valid(new MetadataDocument(
                Versioned, Navigation with { Values = navigationValues }, repository, access,
                Fingerprint, Warnings.ToImmutableArray()));
        }
        catch (ArgumentException)
        {
            return MetadataOutcome.Invalid("The cached metadata entry contains duplicate or invalid values.", Fingerprint);
        }
    }

    public HeuristicSizeResult GetSizeHeuristically(HeuristicSizeGetter innerGet) =>
        new(512L + 2L * (
            (Fingerprint?.Length ?? 0) +
            (Problem?.Length ?? 0) +
            (Navigation?.Values?.Sum(static p => p.Key.Length + p.Value.Length) ?? 0) +
            (Access?.Permissions.Sum(static p => p.Name.Length + (p.Value?.Length ?? 0)) ?? 0) +
            (Access?.Requires.Sum(static p => p.Length) ?? 0) +
            (Access?.Roles.Sum(static r => r.Name.Length + r.Permissions.Sum(static p => p.Length)) ?? 0)));
}

public sealed record MetadataValue(string Name, string? Value);

public sealed record MetadataRole(string Name, string[] Permissions);

public sealed record RepositorySnapshot(
    string? Id,
    string? Visibility,
    MetadataValue[] InternalPeer,
    string[] DeliberatelyPublic);

public sealed record AccessSnapshot(
    string Fingerprint,
    MetadataValue[] Permissions,
    string[] Requires,
    MetadataRole[] Roles);
