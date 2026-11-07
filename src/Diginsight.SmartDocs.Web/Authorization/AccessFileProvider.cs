using System.Collections.Concurrent;
using Diginsight.Diagnostics;
using Diginsight.SmartDocs.Web.ContentSources;
using Diginsight.SmartDocs.Web.Metadata;
using Diginsight.SmartDocs.Web.Shared.Sites;

namespace Diginsight.SmartDocs.Web.Authorization;

/// <summary>
/// Adapts the shared root metadata snapshot to the existing access-policy contract.
/// It performs no independent source read, parsing, or caching.
/// </summary>
public sealed class AccessFileProvider(
    SpaceContentRegistry spaces,
    ContentMetadataProvider metadata,
    ILogger<AccessFileProvider> logger)
{
    private readonly ConcurrentDictionary<string, string> reported = new(StringComparer.OrdinalIgnoreCase);

    public async Task<AccessFileOutcome> GetAsync(string spaceId, CancellationToken cancellationToken = default)
    {
        using var activity = Observability.HotPathActivitySource.StartMethodActivity(logger, () => new { spaceId });

        SpaceContentAccess access = spaces.Get(spaceId);
        MetadataOutcome snapshot = await metadata.GetForSpaceAsync(spaceId, cancellationToken);
        AccessFileOutcome outcome = snapshot.Status switch
        {
            MetadataStatus.Valid when snapshot.Document?.Access is { } policy => AccessFileOutcome.Valid(policy),
            MetadataStatus.Valid or MetadataStatus.Missing => AccessFileOutcome.Missing,
            MetadataStatus.Invalid => AccessFileOutcome.Invalid(snapshot.Problem ?? "The metadata is invalid.", snapshot.Fingerprint),
            MetadataStatus.Unreadable => AccessFileOutcome.Unreadable(snapshot.Problem ?? "The metadata is unreadable."),
            _ => AccessFileOutcome.Invalid("The metadata outcome is unrecognized.", snapshot.Fingerprint),
        };
        Report(access.Space, outcome, null);
        activity?.SetOutput(new { outcome.Status, outcome.Fingerprint });
        return outcome;
    }

    /// <summary>Logs a space's outcome when it differs from the last one logged for that space.</summary>
    private void Report(SpaceOptions space, AccessFileOutcome outcome, Exception? failure)
    {
        string signature = $"{outcome.Status}|{outcome.Fingerprint}|{outcome.Problem}";
        if (reported.TryGetValue(space.Id, out string? previous) && previous == signature)
        {
            return;
        }

        reported[space.Id] = signature;
        switch (outcome.Status)
        {
            case AccessFileStatus.Valid:
                AccessFile file = outcome.File!;
                logger.LogInformation(
                    "Space '{SpaceId}' access file {Fingerprint} is valid. Requires: {Requires}. Roles: {Roles}.",
                    space.Id,
                    file.Fingerprint,
                    file.Requires.IsEmpty ? "nothing" : string.Join(", ", file.Requires),
                    file.Roles.IsEmpty
                        ? "none"
                        : string.Join("; ", file.Roles.Select(static r => $"{r.Key} -> [{string.Join(", ", r.Value)}]")));
                break;

            case AccessFileStatus.Invalid:
                logger.LogError(
                    "Space '{SpaceId}' access file {Fingerprint} is invalid, so the space is hidden until a valid file is published: {Problem}",
                    space.Id,
                    outcome.Fingerprint,
                    outcome.Problem);
                break;

            case AccessFileStatus.Unreadable:
                logger.LogError(
                    failure,
                    "Space '{SpaceId}' access file can't be read, so the space is hidden until a read succeeds: {Problem}",
                    space.Id,
                    outcome.Problem);
                break;

            case AccessFileStatus.Missing when space.AccessFileRequired:
                logger.LogError(
                    "Space '{SpaceId}' requires an access policy, but none is present at its content root, so the space is hidden.",
                    space.Id);
                break;

            default:
                logger.LogDebug(
                    "Space '{SpaceId}' has no access file; only the site default and its floor apply.", space.Id);
                break;
        }
    }

}
