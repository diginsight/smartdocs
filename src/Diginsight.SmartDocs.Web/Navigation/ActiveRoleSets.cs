using Diginsight.SmartDocs.Web.Authorization;

namespace Diginsight.SmartDocs.Web.Navigation;

/// <summary>
/// The role sets of the open hub connections, with a reference count per set. Readers with equal role
/// sets see the same spaces, so each set is one SignalR group, <c>rs:&lt;Id&gt;</c>, and a push is
/// evaluated once per active set instead of once per connection.
/// </summary>
public sealed class ActiveRoleSets
{
    private readonly object gate = new();
    private readonly Dictionary<string, RoleSet> byConnection = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (RoleSet Roles, int Count)> byId = new(StringComparer.Ordinal);

    /// <summary>The SignalR group of the connections that hold <paramref name="roles"/>.</summary>
    public static string GroupOf(RoleSet roles) => "rs:" + roles.Id;

    /// <summary>Records a connection's role set. A reconnect is a new connection, so it is recorded again.</summary>
    public void Add(string connectionId, RoleSet roles)
    {
        lock (gate)
        {
            if (byConnection.TryAdd(connectionId, roles))
            {
                byId[roles.Id] = byId.TryGetValue(roles.Id, out var entry) ? (entry.Roles, entry.Count + 1) : (roles, 1);
            }
        }
    }

    /// <summary>Forgets a connection, and its role set once no other connection holds it.</summary>
    public void Remove(string connectionId)
    {
        lock (gate)
        {
            if (!byConnection.Remove(connectionId, out RoleSet? roles) || !byId.TryGetValue(roles.Id, out var entry))
            {
                return;
            }

            if (entry.Count <= 1)
            {
                byId.Remove(roles.Id);
            }
            else
            {
                byId[roles.Id] = (entry.Roles, entry.Count - 1);
            }
        }
    }

    /// <summary>The role sets that have at least one open connection.</summary>
    public IReadOnlyList<RoleSet> Snapshot()
    {
        lock (gate)
        {
            return byId.Values.Select(static entry => entry.Roles).ToList();
        }
    }
}
