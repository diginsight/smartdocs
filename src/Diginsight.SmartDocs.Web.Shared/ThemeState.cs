using System.Text;

namespace Diginsight.SmartDocs.Web.Shared;

/// <summary>
/// One selectable palette. <see cref="Tokens"/> is the whole palette: every entry becomes a CSS
/// custom property on <c>.page.theme-{Id}</c>, so a theme is data rather than a stylesheet the
/// product has to ship. The set of tokens is deliberately open — a deployment may set any property
/// the stylesheet reads, including ones added after that deployment was configured.
/// </summary>
public sealed class ThemeOption
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Label shown in the picker; falls back to <see cref="Id"/> when unset.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Places the theme under "Light themes" or "Dark themes" and drives the light/dark toggle.</summary>
    public bool Dark { get; set; }

    /// <summary>
    /// CSS custom properties for this palette, without the leading <c>--</c>. Keys are emitted
    /// verbatim, so <c>nav-active-bg</c> becomes <c>--nav-active-bg</c>.
    /// </summary>
    public Dictionary<string, string> Tokens { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

    /// <summary>
    /// Picker-swatch colours, read from the palette itself rather than configured a second time.
    /// Duplicating them invited a swatch that no longer matched the theme it previewed.
    /// </summary>
    public string Bg => Token("bg") ?? "#ffffff";

    public string Accent => Token("brand") ?? Token("selected") ?? "#888888";

    public string? Token(string name) =>
        Tokens.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}

/// <summary>
/// The palettes a deployment offers.
/// <para>
/// The catalog is configuration (<c>Site:Themes</c>), not product code: a deployment exposes exactly
/// the themes it wants, named and coloured as its own, and nothing about any one customer is
/// compiled into the application. <see cref="BuiltIn"/> is the product's own neutral set, used only
/// when a deployment declares no themes of its own — it carries no customer identity.
/// </para>
/// </summary>
public static class ThemeCatalog
{
    /// <summary>
    /// Product-default palettes. Deliberately generic: a deployment that wants its own identity
    /// declares <c>Site:Themes</c> and replaces this set wholesale.
    /// </summary>
    public static IReadOnlyList<ThemeOption> BuiltIn { get; } = new[]
    {
        Make("cosmo", "Cosmo", false,
            ("bg", "#ffffff"), ("fg", "#1f2328"), ("muted", "#57606a"), ("border", "#d0d7de"),
            ("sidebar", "#f6f8fa"), ("hover", "#eaeef2"), ("brand", "#2563eb"), ("on-brand", "#ffffff"),
            ("topbar", "#1f6feb"), ("topbar-2", "#17539f"), ("selected", "#1f6feb"),
            ("pre-bg", "#f6f8fa"), ("code-bg", "#eff1f3"), ("mark-bg", "#fef9c3"), ("mark-fg", "#1f2328")),

        Make("sandstone", "Sandstone", false,
            ("bg", "#fcfbf7"), ("fg", "#3a3730"), ("muted", "#857c6d"), ("border", "#e7ded0"),
            ("sidebar", "#f4efe4"), ("hover", "#ece4d5"), ("brand", "#2a7d6f"), ("on-brand", "#ffffff"),
            ("topbar", "#2f6f7d"), ("topbar-2", "#235460"), ("selected", "#2f6f7d"),
            ("pre-bg", "#f4efe4"), ("code-bg", "#efe7d8"), ("mark-bg", "#fbecc3"), ("mark-fg", "#3a3730")),

        Make("solarized-light", "Solarized Light", false,
            ("bg", "#fdf6e3"), ("fg", "#586e75"), ("muted", "#93a1a1"), ("border", "#eee8d5"),
            ("sidebar", "#f5eeda"), ("hover", "#eee3c8"), ("brand", "#268bd2"), ("on-brand", "#ffffff"),
            ("topbar", "#268bd2"), ("topbar-2", "#1d6da5"), ("selected", "#268bd2"),
            ("pre-bg", "#f5eeda"), ("code-bg", "#eee8d5"), ("mark-bg", "#ffe9a8"), ("mark-fg", "#586e75")),

        Make("minty", "Minty", false,
            ("bg", "#ffffff"), ("fg", "#24352e"), ("muted", "#6b7d75"), ("border", "#d6e7dd"),
            ("sidebar", "#eef7f1"), ("hover", "#e0f0e7"), ("brand", "#0f9d76"), ("on-brand", "#ffffff"),
            ("topbar", "#18b58c"), ("topbar-2", "#128e6d"), ("selected", "#18b58c"),
            ("pre-bg", "#eef7f1"), ("code-bg", "#e2f0ea"), ("mark-bg", "#fef3c3"), ("mark-fg", "#24352e")),

        Make("github-dark", "GitHub Dark", true,
            ("bg", "#0d1117"), ("fg", "#e6edf3"), ("muted", "#8b949e"), ("border", "#30363d"),
            ("sidebar", "#161b22"), ("hover", "#21262d"), ("brand", "#388bfd"), ("on-brand", "#ffffff"),
            ("topbar", "#161b22"), ("topbar-2", "#0f141b"), ("selected", "#388bfd"),
            ("pre-bg", "#161b22"), ("code-bg", "#21262d"), ("mark-bg", "#5c531f"), ("mark-fg", "#f6efc4")),

        Make("darkly", "Darkly", true,
            ("bg", "#1a1d20"), ("fg", "#e9ecef"), ("muted", "#adb5bd"), ("border", "#2c3136"),
            ("sidebar", "#212529"), ("hover", "#2c3136"), ("brand", "#00bc8c"), ("on-brand", "#04231c"),
            ("topbar", "#212529"), ("topbar-2", "#16191c"), ("selected", "#00bc8c"),
            ("pre-bg", "#212529"), ("code-bg", "#2c3136"), ("mark-bg", "#4d4a1f"), ("mark-fg", "#f0ecc6")),

        Make("nord", "Nord", true,
            ("bg", "#2e3440"), ("fg", "#eceff4"), ("muted", "#a9b1c0"), ("border", "#434c5e"),
            ("sidebar", "#3b4252"), ("hover", "#434c5e"), ("brand", "#88c0d0"), ("on-brand", "#2e3440"),
            ("topbar", "#3b4252"), ("topbar-2", "#2b303b"), ("selected", "#88c0d0"),
            ("pre-bg", "#3b4252"), ("code-bg", "#434c5e"), ("mark-bg", "#5e5330"), ("mark-fg", "#eceff4")),

        Make("solarized-dark", "Solarized Dark", true,
            ("bg", "#002b36"), ("fg", "#a7b6b6"), ("muted", "#7d9494"), ("border", "#0c4a5a"),
            ("sidebar", "#073642"), ("hover", "#0a5060"), ("brand", "#2aa198"), ("on-brand", "#04231f"),
            ("topbar", "#073642"), ("topbar-2", "#04222a"), ("selected", "#2aa198"),
            ("pre-bg", "#073642"), ("code-bg", "#0a4552"), ("mark-bg", "#4d4626"), ("mark-fg", "#eee8d5")),
    };

    /// <summary>
    /// The catalog a deployment actually offers: its own themes when it declares any, the product's
    /// otherwise. Declared themes replace rather than extend the built-ins, so a site shows exactly
    /// the list it configured.
    /// </summary>
    public static IReadOnlyList<ThemeOption> Resolve(IEnumerable<ThemeOption>? configured)
    {
        List<ThemeOption> declared = (configured ?? Enumerable.Empty<ThemeOption>())
            .Where(static t => !string.IsNullOrWhiteSpace(t.Id))
            .ToList();

        return declared.Count > 0 ? declared : BuiltIn;
    }

    /// <summary>
    /// Renders a catalog as CSS. Each theme becomes one <c>.page.theme-{id}</c> rule whose body is
    /// its tokens, which is exactly the shape these palettes had while they were hand-written into
    /// the stylesheet — the difference is only who states them.
    /// </summary>
    public static string ToCss(IEnumerable<ThemeOption> themes)
    {
        var css = new StringBuilder();

        foreach (ThemeOption theme in themes)
        {
            string id = SafeIdentifier(theme.Id);
            if (id.Length == 0 || theme.Tokens.Count == 0)
            {
                continue;
            }

            css.Append(".page.theme-").Append(id).AppendLine(" {");

            foreach ((string name, string value) in theme.Tokens)
            {
                string token = SafeIdentifier(name);
                string? safeValue = SafeValue(value);
                if (token.Length == 0 || safeValue is null)
                {
                    continue;
                }

                css.Append("  --").Append(token).Append(": ").Append(safeValue).AppendLine(";");
            }

            css.AppendLine("}");
        }

        return css.ToString();
    }

    /// <summary>
    /// Token and theme names reach the page as CSS identifiers, so they are restricted to what an
    /// identifier may contain. Configuration is operator-supplied rather than reader-supplied, but
    /// it is still text that ends up inside a style element, and an unchecked name could close the
    /// rule and open something else.
    /// </summary>
    private static string SafeIdentifier(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string trimmed = raw.Trim();
        return trimmed.All(static c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_')
            ? trimmed
            : string.Empty;
    }

    /// <summary>
    /// Values may be any colour notation — hex, <c>rgba(...)</c>, a gradient, or <c>var(--other)</c> —
    /// so they are filtered by what must NOT appear rather than by what may.
    /// </summary>
    private static string? SafeValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string trimmed = raw.Trim();
        return trimmed.IndexOfAny(new[] { ';', '{', '}', '<', '>', '@', '\\', '"' }) >= 0 ? null : trimmed;
    }

    private static ThemeOption Make(string id, string name, bool dark, params (string Key, string Value)[] tokens)
    {
        var option = new ThemeOption { Id = id, Name = name, Dark = dark };
        foreach ((string key, string value) in tokens)
        {
            option.Tokens[key] = value;
        }

        // The built-ins keep the filled-pill selection the product has always used. Stating it here
        // rather than in the stylesheet keeps every palette complete on its own terms.
        option.Tokens["nav-active-bg"] = "var(--selected)";
        option.Tokens["nav-active-fg"] = "var(--on-brand)";
        return option;
    }
}

/// <summary>
/// Shared, per-circuit theme state. The layout applies the selected theme as a CSS class
/// (<c>theme-{id}</c>); the standalone toggle button and the About menu's theme picker both drive it
/// through this single source of truth.
/// <para>
/// The catalog arrives from configuration, so this type holds no palette of its own and no hardcoded
/// default identifier: the defaults are simply the first light and first dark theme the deployment
/// offers, and <c>Site:Branding:DefaultTheme</c> may name a different one for first load.
/// </para>
/// </summary>
public sealed class ThemeState
{
    private IReadOnlyList<ThemeOption> options = ThemeCatalog.BuiltIn;
    private string? lastLight;
    private string? lastDark;

    public ThemeState()
    {
        ThemeId = DefaultLight;
    }

    public ThemeState(IReadOnlyList<ThemeOption> catalog)
    {
        SetCatalog(catalog);
    }

    /// <summary>The deployment's themes, in menu order.</summary>
    public IReadOnlyList<ThemeOption> Options => options;

    public string ThemeId { get; private set; } = string.Empty;

    public ThemeOption? Current => options.FirstOrDefault(o => o.Id == ThemeId);

    public bool Dark => Current?.Dark ?? false;

    /// <summary>First light theme on offer; the first theme of any kind when none is light.</summary>
    public string DefaultLight =>
        (options.FirstOrDefault(static o => !o.Dark) ?? options.FirstOrDefault())?.Id ?? string.Empty;

    /// <summary>First dark theme on offer; falls back to <see cref="DefaultLight"/> when none is dark.</summary>
    public string DefaultDark =>
        options.FirstOrDefault(static o => o.Dark)?.Id ?? DefaultLight;

    public event Action? Changed;

    /// <summary>
    /// Adopts the deployment's catalog. Called on the server at startup and in the browser once
    /// <c>/_site</c> lands. A selection the new catalog does not contain falls back to the default
    /// rather than leaving the shell on a theme whose tokens were never emitted.
    /// </summary>
    public void SetCatalog(IReadOnlyList<ThemeOption>? catalog)
    {
        options = catalog is { Count: > 0 } ? catalog : ThemeCatalog.BuiltIn;
        lastLight = null;
        lastDark = null;

        if (options.All(o => o.Id != ThemeId))
        {
            ThemeId = DefaultLight;
        }

        Changed?.Invoke();
    }

    public void SetTheme(string? id)
    {
        if (string.IsNullOrEmpty(id) || id == ThemeId || options.All(o => o.Id != id))
        {
            return;
        }

        ThemeId = id;

        // Remember the choice per side so the light/dark button can return to it. Without this the
        // flip would always land on the two defaults, silently discarding a reader who had picked,
        // say, Nord as their dark palette.
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
    public void Toggle() => SetTheme(Dark ? lastLight ?? DefaultLight : lastDark ?? DefaultDark);

    public void Reset() => SetTheme(DefaultLight);
}
