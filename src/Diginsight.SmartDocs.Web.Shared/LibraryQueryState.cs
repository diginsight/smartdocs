namespace Diginsight.SmartDocs.Web.Shared;

/// <summary>How the Explore surface presents its results.</summary>
public enum LibraryView
{
    Cards,
    Timeline,
}

/// <summary>
/// The library search box and the Cards/Timeline switch live in the topbar, but the results they
/// govern are rendered by the Explore page. This holds the pair so both sides read the same value
/// instead of each keeping a copy and drifting apart.
/// <para>Unlike <see cref="PreferencesState"/> this is deliberately <em>not</em> persisted: a query
/// is about the current moment, and restoring yesterday's search on load would be surprising.</para>
/// </summary>
public sealed class LibraryQueryState
{
    private string query = string.Empty;
    private LibraryView view = LibraryView.Cards;

    public event Action? Changed;

    public string Query => query;

    public LibraryView View => view;

    public bool HasQuery => !string.IsNullOrWhiteSpace(query);

    public void SetQuery(string? value)
    {
        string next = value ?? string.Empty;
        if (next == query)
        {
            return;
        }

        query = next;
        Changed?.Invoke();
    }

    public void SetView(LibraryView value)
    {
        if (value == view)
        {
            return;
        }

        view = value;
        Changed?.Invoke();
    }

    public void Clear() => SetQuery(string.Empty);
}
