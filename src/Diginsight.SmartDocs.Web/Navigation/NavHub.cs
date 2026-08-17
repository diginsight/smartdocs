using Microsoft.AspNetCore.SignalR;

namespace Diginsight.SmartDocs.Web.Navigation;

/// <summary>
/// Server→client broadcast hub for live navigation metadata. Clients only subscribe; there are no
/// client→server methods. The server pushes <c>MetadataChanged</c> (on content change, via
/// <see cref="NavChangePublisher"/>) and <c>CountsReady</c> (once the startup warm-up has computed
/// the recursive folder counts). See <see cref="Diginsight.SmartDocs.Web.Shared.Navigation.NavHubContract"/>.
/// <para>
/// On connect the hub sends the connecting client the current root counts. The warm-up
/// <c>CountsReady</c> broadcast only reaches already-connected clients, so a browser that connects
/// after warm-up (the common case) would otherwise never receive the counts and its footer total
/// would stay at the cold-start value.
/// </para>
/// </summary>
public sealed class NavHub(NavChangePublisher publisher) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await publisher.SendCurrentCountsAsync(Clients.Caller);
    }
}
