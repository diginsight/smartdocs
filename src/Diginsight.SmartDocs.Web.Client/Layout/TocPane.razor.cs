namespace Diginsight.SmartDocs.Web.Client.Layout;

public partial class TocPane
{
    protected override void OnInitialized() => Toc.Changed += OnChanged;

    private void OnChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => Toc.Changed -= OnChanged;
}
