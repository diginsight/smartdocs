namespace Diginsight.SmartDocs.Web.Shared.Rendering;

/// <summary>
/// Shared, per-circuit state for the on-page table of contents. The page
/// (<c>ContentView</c>) publishes the current headings here; the docked TOC
/// pane in the layout subscribes and renders them. This lets the TOC live in
/// the layout as a real docked column (like the VS Code chat pane) instead of
/// an overlay, so it never overlaps the article body.
/// </summary>
public sealed class TocState
{
    public IReadOnlyList<TocEntry> Entries { get; private set; } = Array.Empty<TocEntry>();

    public bool Open { get; private set; } = true;

    public event Action? Changed;

    public void SetEntries(IReadOnlyList<TocEntry> entries)
    {
        Entries = entries ?? Array.Empty<TocEntry>();
        Changed?.Invoke();
    }

    public void SetOpen(bool open)
    {
        if (Open == open)
        {
            return;
        }

        Open = open;
        Changed?.Invoke();
    }

    public void Toggle() => SetOpen(!Open);
}
