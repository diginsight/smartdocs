using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diginsight.SmartDocs.Web.Shared;

/// <summary>How the sidebar orders sibling nodes.</summary>
public enum NavSort
{
    /// <summary>The content hierarchy's own order (numeric filename prefixes and folder metadata).</summary>
    Curated,

    /// <summary>Largest recursive article count first.</summary>
    Count,

    /// <summary>Newest article in the subtree first.</summary>
    Recent,

    /// <summary>Alphabetical by label.</summary>
    Alpha,
}

/// <summary>The serialisable shape written to localStorage. Kept separate from the live state so
/// adding a field can never break deserialisation of an older payload.</summary>
public sealed class PreferencesData
{
    [JsonPropertyName("density")] public string? Density { get; set; }
    [JsonPropertyName("font")] public string? Font { get; set; }
    [JsonPropertyName("navSort")] public string? NavSort { get; set; }
    [JsonPropertyName("showCounts")] public bool? ShowCounts { get; set; }
    [JsonPropertyName("datedOnly")] public bool? DatedOnly { get; set; }
    [JsonPropertyName("pinned")] public List<string>? Pinned { get; set; }
    [JsonPropertyName("hidden")] public List<string>? Hidden { get; set; }
    [JsonPropertyName("favourites")] public List<string>? Favourites { get; set; }
}

/// <summary>
/// Per-user reading preferences: look (density, reading font), sidebar ordering, which sections are
/// pinned or hidden, and favourited articles.
/// <para>
/// The state is authoritative in memory and merely <em>mirrored</em> to localStorage, so it is fully
/// usable during server prerender (when no JS runtime exists). The layout hydrates it once the
/// browser is interactive and raises <see cref="Changed"/>, which re-renders every consumer.
/// </para>
/// <para>
/// Pinning and hiding are keyed by a section's <em>label</em> rather than its route prefix: a label is
/// what the reader actually recognises in the menu, and it survives a folder being renumbered.
/// </para>
/// </summary>
public sealed class PreferencesState
{
    /// <summary>localStorage key. Versioned so a future breaking shape can be introduced cleanly.</summary>
    public const string StorageKey = "smartdocs.prefs.v1";

    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly HashSet<string> pinned = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> hidden = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> favourites = new(StringComparer.Ordinal);

    /// <summary>Raised whenever any preference changes, so live components re-render.</summary>
    public event Action? Changed;

    /// <summary>Raised when a preference that must be mirrored to storage changes.</summary>
    public event Action<PreferencesData>? PersistRequested;

    public string Density { get; private set; } = "comfortable";

    public string Font { get; private set; } = "sans";

    public NavSort Sort { get; private set; } = NavSort.Curated;

    public bool ShowCounts { get; private set; } = true;

    public bool DatedOnly { get; private set; }

    /// <summary>True once the browser payload has been applied (or confirmed absent).</summary>
    public bool Hydrated { get; private set; }

    public IReadOnlyCollection<string> Pinned => pinned;

    public IReadOnlyCollection<string> Hidden => hidden;

    public IReadOnlyCollection<string> Favourites => favourites;

    public bool IsPinned(string label) => pinned.Contains(label);

    public bool IsHidden(string label) => hidden.Contains(label);

    public bool IsFavourite(string route) => favourites.Contains(route);

    public void TogglePinned(string label)
    {
        if (!pinned.Add(label))
        {
            pinned.Remove(label);
        }

        // A section cannot be both promoted and suppressed; pinning wins because it is the more
        // deliberate gesture.
        hidden.Remove(label);
        Commit();
    }

    public void ToggleHidden(string label)
    {
        if (!hidden.Add(label))
        {
            hidden.Remove(label);
        }

        pinned.Remove(label);
        Commit();
    }

    public void ToggleFavourite(string route)
    {
        if (!favourites.Add(route))
        {
            favourites.Remove(route);
        }

        Commit();
    }

    public void ClearHidden()
    {
        if (hidden.Count == 0)
        {
            return;
        }

        hidden.Clear();
        Commit();
    }

    public void SetDensity(string value) => Set(() => Density = value, Density == value);

    public void SetFont(string value) => Set(() => Font = value, Font == value);

    public void SetSort(NavSort value) => Set(() => Sort = value, Sort == value);

    public void SetShowCounts(bool value) => Set(() => ShowCounts = value, ShowCounts == value);

    public void SetDatedOnly(bool value) => Set(() => DatedOnly = value, DatedOnly == value);

    public void ResetAll()
    {
        pinned.Clear();
        hidden.Clear();
        favourites.Clear();
        Density = "comfortable";
        Font = "sans";
        Sort = NavSort.Curated;
        ShowCounts = true;
        DatedOnly = false;
        Commit();
    }

    /// <summary>Applies a payload read from storage. Does not re-persist — this *is* the stored value.</summary>
    public void Hydrate(string? json)
    {
        Hydrated = true;

        PreferencesData? data = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            // A corrupt or hand-edited payload must not take the shell down: fall back to defaults.
            try
            {
                data = JsonSerializer.Deserialize<PreferencesData>(json!, Json);
            }
            catch (JsonException)
            {
                data = null;
            }
        }

        if (data is null)
        {
            Changed?.Invoke();
            return;
        }

        Density = data.Density ?? Density;
        Font = data.Font ?? Font;
        ShowCounts = data.ShowCounts ?? ShowCounts;
        DatedOnly = data.DatedOnly ?? DatedOnly;
        Sort = Enum.TryParse(data.NavSort, ignoreCase: true, out NavSort s) ? s : Sort;

        Replace(pinned, data.Pinned);
        Replace(hidden, data.Hidden);
        Replace(favourites, data.Favourites);

        Changed?.Invoke();
    }

    /// <summary>Snapshots the current values for persistence.</summary>
    public PreferencesData Snapshot() => new()
    {
        Density = Density,
        Font = Font,
        NavSort = Sort.ToString(),
        ShowCounts = ShowCounts,
        DatedOnly = DatedOnly,
        Pinned = pinned.ToList(),
        Hidden = hidden.ToList(),
        Favourites = favourites.ToList(),
    };

    public static string Serialize(PreferencesData data) => JsonSerializer.Serialize(data, Json);

    /// <summary>Re-raises <see cref="Changed"/> and asks the shell to mirror the new value to storage.</summary>
    public void Commit()
    {
        Changed?.Invoke();
        PersistRequested?.Invoke(Snapshot());
    }

    private void Set(Action assign, bool unchanged)
    {
        if (unchanged)
        {
            return;
        }

        assign();
        Commit();
    }

    private static void Replace(HashSet<string> target, List<string>? values)
    {
        target.Clear();
        foreach (string v in values ?? [])
        {
            target.Add(v);
        }
    }
}
