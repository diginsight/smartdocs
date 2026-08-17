using Diginsight.SmartDocs.Web.Shared;
using Microsoft.AspNetCore.Components;

namespace Diginsight.SmartDocs.Web.Client.Layout;

public partial class AboutMenu
{
    protected override void OnInitialized() => Theme.Changed += OnChanged;

    private void OnChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => Theme.Changed -= OnChanged;

    private RenderFragment ThemeItem(ThemeOption opt) => builder =>
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "class", opt.Id == Theme.ThemeId ? "dropdown-action active" : "dropdown-action");
        builder.AddAttribute(2, "type", "button");
        builder.AddAttribute(3, "onclick", EventCallback.Factory.Create(this, () => Theme.SetTheme(opt.Id)));
        builder.OpenElement(4, "span");
        builder.AddAttribute(5, "class", "theme-swatch");
        builder.AddAttribute(6, "style", $"--sw-bg:{opt.Bg};--sw-accent:{opt.Accent}");
        builder.CloseElement();
        builder.AddContent(7, opt.Name);
        builder.CloseElement();
    };
}
