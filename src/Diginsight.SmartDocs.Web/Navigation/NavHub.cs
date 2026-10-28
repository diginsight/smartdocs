using Diginsight.SmartDocs.Web.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Diginsight.SmartDocs.Web.Navigation;

/// <summary>
/// Server→client hub for live navigation metadata. Clients only subscribe; there are no client→server
/// methods. The server pushes <c>MetadataChanged</c> (on content change, via
/// <see cref="NavChangePublisher"/>) and <c>CountsReady</c> (once the startup warm-up has computed
/// the recursive folder counts). See <see cref="Diginsight.SmartDocs.Web.Shared.Navigation.NavHubContract"/>.
/// <para>
/// Each connection joins the group of its reader's role set, and the publisher sends each group only
/// the records its readers can see. The connection closes when its authentication expires, so a
/// session that ended stops receiving pushes.
/// </para>
/// <para>
/// On connect the hub sends the connecting client the current root counts, composed for its reader. The
/// warm-up <c>CountsReady</c> broadcast only reaches already-connected clients, so a browser that
/// connects after warm-up (the common case) would otherwise never receive the counts and its footer
/// total would stay at the cold-start value.
/// </para>
/// </summary>
public sealed class NavHub(NavChangePublisher publisher, ActiveRoleSets activeRoleSets) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        RoleSet roles = RoleSet.FromPrincipal(Context.User);
        activeRoleSets.Add(Context.ConnectionId, roles);
        await Groups.AddToGroupAsync(Context.ConnectionId, ActiveRoleSets.GroupOf(roles));
        await publisher.SendCurrentCountsAsync(Clients.Caller, roles);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        activeRoleSets.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
