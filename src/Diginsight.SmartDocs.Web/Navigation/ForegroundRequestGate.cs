namespace Diginsight.SmartDocs.Web.Navigation;

public sealed class ForegroundRequestGate
{
    private readonly object _sync = new();
    private readonly TaskCompletionSource _firstResponse =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TaskCompletionSource _idle =
        CompletedSource();
    private int _activeRequests;

    public IDisposable Enter()
    {
        lock (_sync)
        {
            if (_activeRequests++ == 0)
            {
                _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        return new Lease(this);
    }

    public async Task WaitForFirstResponseOrGraceAsync(TimeSpan grace, CancellationToken cancellationToken)
    {
        await Task.WhenAny(_firstResponse.Task, Task.Delay(grace, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task WaitForBackgroundTurnAsync(TimeSpan maximumPause, CancellationToken cancellationToken)
    {
        Task idle;
        lock (_sync)
        {
            idle = _activeRequests == 0 ? Task.CompletedTask : _idle.Task;
        }

        await Task.WhenAny(idle, Task.Delay(maximumPause, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void Exit()
    {
        TaskCompletionSource? becameIdle = null;
        lock (_sync)
        {
            if (--_activeRequests == 0)
            {
                becameIdle = _idle;
            }
        }

        becameIdle?.TrySetResult();
        _firstResponse.TrySetResult();
    }

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }

    private sealed class Lease(ForegroundRequestGate owner) : IDisposable
    {
        private ForegroundRequestGate? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Exit();
    }
}
