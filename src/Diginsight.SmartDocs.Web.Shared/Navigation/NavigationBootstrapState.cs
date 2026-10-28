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

    /// <summary>
    /// The signed-in reader as the shell shows them, or null for an anonymous reader. Only the
    /// display name and the sign-out form's antiforgery token travel: the server alone decides what
    /// a reader may see, so the browser needs neither roles nor identity tokens.
    /// </summary>
    [PersistentState]
    public BootstrapReader? Reader { get; set; }

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

/// <summary>
/// The signed-in reader in the bootstrap record: a display name, and the antiforgery field the
/// sign-out form posts. The page that carries them is the reader's own and is never cached.
/// </summary>
public sealed class BootstrapReader
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The form field name the host's antiforgery validation reads.</summary>
    public string SignOutFormField { get; set; } = string.Empty;

    /// <summary>The antiforgery request token for this reader's sign-out form.</summary>
    public string SignOutToken { get; set; } = string.Empty;
}
