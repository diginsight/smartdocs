using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Diginsight.SmartDocs.Web.Client.Layout;

public partial class SearchOverlay
{
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private ElementReference _input;
    private string _query = string.Empty;
    private bool _wasOpen;

    private Task Close() => OnClose.InvokeAsync();

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            await Close();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Open && !_wasOpen)
        {
            _wasOpen = true;
            try { await _input.FocusAsync(); } catch { /* element not ready */ }
        }
        else if (!Open && _wasOpen)
        {
            _wasOpen = false;
            _query = string.Empty;
        }
    }

    public void Dispose() { }
}
