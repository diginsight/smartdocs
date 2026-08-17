using Microsoft.AspNetCore.Components;

namespace Diginsight.SmartDocs.Web.Client.Layout;

public partial class NotificationPanel
{
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private readonly List<string> _items = new();

    private Task Close() => OnClose.InvokeAsync();

    private void DismissAll() => _items.Clear();
}
