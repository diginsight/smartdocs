using Diginsight.SmartDocs.Web.Shared.Rendering;
using Diginsight.SmartDocs.Web.Shared.Sites;
using Microsoft.AspNetCore.Components;

namespace Diginsight.SmartDocs.Web.Shared.Navigation;

public sealed class NavigationBootstrapState
{
    [PersistentState]
    public SiteShellOptions? Site { get; set; }

    [PersistentState]
    public string? PageRoute { get; set; }

    [PersistentState]
    public RenderedPage? Page { get; set; }

    [PersistentState]
    public Dictionary<string, NavChild[]> Levels { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [PersistentState]
    public Dictionary<string, FolderRecord> FolderRecords { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetLevel(string prefix, out IReadOnlyList<NavChild> level)
    {
        if (Levels.TryGetValue(Normalize(prefix), out NavChild[]? stored))
        {
            level = stored;
            return true;
        }

        level = Array.Empty<NavChild>();
        return false;
    }

    public void SetLevel(string prefix, IReadOnlyList<NavChild> level) =>
        Levels[Normalize(prefix)] = level.ToArray();

    public void SetFolderRecords(IEnumerable<FolderRecord> records)
    {
        foreach (FolderRecord record in records)
        {
            FolderRecords[record.Prefix] = record;
        }
    }

    private static string Normalize(string prefix) =>
        (prefix ?? string.Empty).Replace('\\', '/').Trim('/');
}
