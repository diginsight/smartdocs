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
    private readonly Dictionary<string, Entry> byId = new(StringComparer.Ordinal);

    /// <summary>The SignalR group of the connections that hold <paramref name="roles"/>.</summary>
    public static string GroupOf(RoleSet roles) => "rs:" + roles.Id;

    /// <summary>Records a connection's role set. A reconnect is a new connection, so it is recorded again.</summary>
    public void Add(string connectionId, RoleSet roles)
    {
        lock (gate)
        {
            if (byConnection.TryAdd(connectionId, roles))
            {
                if (!byId.TryGetValue(roles.Id, out Entry? entry))
                {
                    entry = new Entry(roles);
                    byId.Add(roles.Id, entry);
                }

                entry.Count++;
            }
        }
    }

    /// <summary>Forgets a connection, and its role set once no other connection holds it.</summary>
    public void Remove(string connectionId)
    {
        lock (gate)
        {
            if (!byConnection.Remove(connectionId, out RoleSet? roles) || !byId.TryGetValue(roles.Id, out Entry? entry))
            {
                return;
            }

            if (entry.Count <= 1)
            {
                byId.Remove(roles.Id);
            }
            else
            {
                entry.Count--;
            }
        }
    }

    /// <summary>Remembers what connected readers could see without discarding earlier readers' view.</summary>
    public void RememberVisibility(RoleSet roles, IReadOnlyDictionary<string, string> visible)
    {
        lock (gate)
        {
            if (byId.TryGetValue(roles.Id, out Entry? entry))
            {
                foreach ((string space, string fingerprint) in visible)
                {
                    entry.Visible.TryAdd(space, fingerprint);
                }
            }
        }
    }

    /// <summary>Only a previously or currently visible scope may cause a refresh notification.</summary>
    public bool NeedsRefresh(RoleSet roles, IReadOnlyDictionary<string, string> current, string? changedSpace)
    {
        lock (gate)
        {
            if (!byId.TryGetValue(roles.Id, out Entry? entry))
            {
                return false;
            }

            if (changedSpace is not null)
            {
                return entry.Visible.ContainsKey(changedSpace) || current.ContainsKey(changedSpace);
            }

            return entry.Visible.Count != current.Count ||
                entry.Visible.Any(pair => !current.TryGetValue(pair.Key, out string? fingerprint) ||
                    !string.Equals(pair.Value, fingerprint, StringComparison.Ordinal));
        }
    }

    /// <summary>Advances the remembered view only after a refresh was delivered successfully.</summary>
    public void Refreshed(RoleSet roles, IReadOnlyDictionary<string, string> current, string? changedSpace)
    {
        lock (gate)
        {
            if (!byId.TryGetValue(roles.Id, out Entry? entry))
            {
                return;
            }

            if (changedSpace is null)
            {
                entry.Visible.Clear();
                foreach ((string space, string fingerprint) in current)
                {
                    entry.Visible[space] = fingerprint;
                }
            }
            else if (current.TryGetValue(changedSpace, out string? fingerprint))
            {
                entry.Visible[changedSpace] = fingerprint;
            }
            else
            {
                entry.Visible.Remove(changedSpace);
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

    private sealed class Entry(RoleSet roles)
    {
        public RoleSet Roles { get; } = roles;
        public int Count { get; set; }
        public Dictionary<string, string> Visible { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
