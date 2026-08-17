namespace Diginsight.SmartDocs.Web.Shared;

/// <summary>A selectable site theme. <see cref="Accent"/> and <see cref="Bg"/> drive the picker swatch.</summary>
public sealed record ThemeOption(string Id, string Name, bool Dark, string Accent, string Bg);

/// <summary>
/// Shared, per-circuit theme state. The layout applies the selected theme as a CSS class
/// (<c>theme-{id}</c>); the standalone toggle button and the About menu's theme picker all
/// drive it through this single source of truth. The default light/dark pair is Cosmo and
/// GitHub Dark.
/// </summary>
public sealed class ThemeState
{
    public const string DefaultLight = "cosmo";
    public const string DefaultDark = "github-dark";

    /// <summary>Curated light + dark themes, in menu order.</summary>
    public static readonly IReadOnlyList<ThemeOption> Options = new[]
    {
        new ThemeOption("cosmo", "Cosmo", false, "#1f6feb", "#ffffff"),
        new ThemeOption("sandstone", "Sandstone", false, "#2f6f7d", "#fcfbf7"),
        new ThemeOption("solarized-light", "Solarized Light", false, "#268bd2", "#fdf6e3"),
        new ThemeOption("minty", "Minty", false, "#18b58c", "#ffffff"),
        new ThemeOption("github-dark", "GitHub Dark", true, "#388bfd", "#0d1117"),
        new ThemeOption("darkly", "Darkly", true, "#00bc8c", "#1a1d20"),
        new ThemeOption("nord", "Nord", true, "#88c0d0", "#2e3440"),
        new ThemeOption("solarized-dark", "Solarized Dark", true, "#2aa198", "#002b36"),
    };

    public string ThemeId { get; private set; } = DefaultLight;

    public ThemeOption Current => Options.FirstOrDefault(o => o.Id == ThemeId) ?? Options[0];

    public bool Dark => Current.Dark;

    public event Action? Changed;

    public void SetTheme(string? id)
    {
        if (string.IsNullOrEmpty(id) || id == ThemeId || Options.All(o => o.Id != id))
        {
            return;
        }

        ThemeId = id;
        Changed?.Invoke();
    }

    /// <summary>Quick light/dark flip used by the standalone topbar button.</summary>
    public void Toggle() => SetTheme(Dark ? DefaultLight : DefaultDark);

    public void Reset() => SetTheme(DefaultLight);
}
