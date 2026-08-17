namespace Diginsight.SmartDocs.Web.Shared.Navigation;

/// <summary>
/// Per-circuit state for the currently viewed article. Published by <c>ContentView</c>
/// when a page renders; consumed by the footer in <c>MainLayout</c>.
/// </summary>
public sealed class ArticleState
{
    /// <summary>Title of the currently rendered article, or null when on a section landing / home.</summary>
    public string? Title { get; private set; }

    /// <summary>Approximate word count of the article's text content.</summary>
    public int? WordCount { get; private set; }

    /// <summary>Raised when the active article changes.</summary>
    public event Action? Changed;

    /// <summary>Sets the currently displayed article's metadata and notifies subscribers.</summary>
    public void Set(string? title, int? wordCount)
    {
        if (Title == title && WordCount == wordCount) return;
        Title = title;
        WordCount = wordCount;
        Changed?.Invoke();
    }

    /// <summary>Clears the article info (e.g. when navigating to a section landing).</summary>
    public void Clear()
    {
        if (Title is null && WordCount is null) return;
        Title = null;
        WordCount = null;
        Changed?.Invoke();
    }
}
