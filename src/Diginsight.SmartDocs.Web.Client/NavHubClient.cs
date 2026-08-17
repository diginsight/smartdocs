using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Diginsight.SmartDocs.Web.Client;

/// <summary>
/// WASM client for the navigation metadata hub (<see cref="NavHubContract.Route"/>). It only
/// receives: the server pushes <c>MetadataChanged</c> (a changed folder + its ancestors' new
/// absolute counts) and <c>CountsReady</c> (all root counts, once warm-up finishes). Consumers
/// subscribe to the events and apply the aggregates to the cached nav tree — no polling.
/// </summary>
public sealed class NavHubClient(NavigationManager nav) : IAsyncDisposable
{
    private HubConnection? _connection;

    /// <summary>Raised when the server pushes updated folder aggregates after a content change.</summary>
    public event Action<IReadOnlyList<NavAggregateDelta>>? MetadataChanged;

    /// <summary>Raised once the server's startup warm-up has computed the root counts.</summary>
    public event Action<IReadOnlyList<NavAggregateDelta>>? CountsReady;

    /// <summary>Raised after the connection is re-established, so consumers can re-sync missed changes.</summary>
    public event Action? Reconnected;

    /// <summary>Builds the connection (auto-reconnect) and starts it. Idempotent.</summary>
    public async Task StartAsync()
    {
        if (_connection is not null)
        {
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(nav.ToAbsoluteUri(NavHubContract.Route))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<IReadOnlyList<NavAggregateDelta>>(
            NavHubContract.MetadataChanged, deltas => MetadataChanged?.Invoke(deltas));
        _connection.On<IReadOnlyList<NavAggregateDelta>>(
            NavHubContract.CountsReady, deltas => CountsReady?.Invoke(deltas));
        _connection.Reconnected += _ =>
        {
            Reconnected?.Invoke();
            return Task.CompletedTask;
        };

        try
        {
            await _connection.StartAsync();
        }
        catch
        {
            // Best-effort: the app still works via the initial nav fetch; a later reconnect retries.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
