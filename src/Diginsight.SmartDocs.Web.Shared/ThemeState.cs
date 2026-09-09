namespace Diginsight.SmartDocs.Web.Shared;

/// <summary>A selectable site theme. <see cref="Accent"/> and <see cref="Bg"/> drive the picker swatch.
/// <para><see cref="Signature"/> marks the four expressive palettes that ship with the reading
/// experience; they are grouped ahead of the plain Bootswatch-derived themes in the picker.</para></summary>
public sealed record ThemeOption(string Id, string Name, bool Dark, string Accent, string Bg, bool Signature = false);

/// <summary>
/// Shared, per-circuit theme state. The layout applies the selected theme as a CSS class
/// (<c>theme-{id}</c>); the standalone toggle button and the About menu's theme picker all
/// drive it through this single source of truth. The default light/dark pair is Azure and
/// Aurora.
/// </summary>
public sealed class ThemeState
{
    public const string DefaultLight = "azure";
    public const string DefaultDark = "aurora";

    /// <summary>The theme a reader sees before they have expressed any preference.</summary>
    public const string Initial = DefaultLight;

    /// <summary>Curated light + dark themes, in menu order. Signature palettes come first.</summary>
    public static readonly IReadOnlyList<ThemeOption> Options = new[]
    {
        new ThemeOption("azure", "Azure", false, "#1f6feb", "#f7f9fc", Signature: true),
        new ThemeOption("aurora", "Aurora", true, "#7c5cff", "#0b0f1c", Signature: true),
        new ThemeOption("editorial", "Editorial", false, "#b4530a", "#fbf9f5", Signature: true),
        new ThemeOption("forest", "Forest", true, "#4ade80", "#0d1512", Signature: true),
        new ThemeOption("graphite", "Graphite", false, "#3f5871", "#f4f5f7", Signature: true),
        new ThemeOption("cosmo", "Cosmo", false, "#1f6feb", "#ffffff"),
        new ThemeOption("sandstone", "Sandstone", false, "#2f6f7d", "#fcfbf7"),
        new ThemeOption("solarized-light", "Solarized Light", false, "#268bd2", "#fdf6e3"),
        new ThemeOption("minty", "Minty", false, "#18b58c", "#ffffff"),
        new ThemeOption("github-dark", "GitHub Dark", true, "#388bfd", "#0d1117"),
        new ThemeOption("darkly", "Darkly", true, "#00bc8c", "#1a1d20"),
        new ThemeOption("nord", "Nord", true, "#88c0d0", "#2e3440"),
        new ThemeOption("solarized-dark", "Solarized Dark", true, "#2aa198", "#002b36"),
    };

    public string ThemeId { get; private set; } = Initial;

    private string lastLight = DefaultLight;
    private string lastDark = DefaultDark;

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

        // Remember the choice per side so the light/dark button can return to it. Without this the
        // flip would always land on the two defaults, silently discarding a reader who had picked,
        // say, Forest as their dark palette.
        if (Dark)
        {
            lastDark = id;
        }
        else
        {
            lastLight = id;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Quick light/dark flip used by the standalone topbar button. Returns to the reader's most
    /// recent theme on the other side rather than to the built-in default.
    /// </summary>
    public void Toggle() => SetTheme(Dark ? lastLight : lastDark);

    public void Reset() => SetTheme(Initial);
}
