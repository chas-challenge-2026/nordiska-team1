namespace Nordiska.Reporting.Worker.Lifecycle;

internal enum WorkerLifecycleState
{
    Starting,
    Running,
    Draining,
    Stopped
}

internal sealed class WorkerLifecycle
{
    private int _state = (int)WorkerLifecycleState.Starting;

    public WorkerLifecycleState State =>
        (WorkerLifecycleState)Volatile.Read(ref _state);

    public bool IsAcceptingWork =>
        State == WorkerLifecycleState.Running;

    public void Start() =>
        Interlocked.CompareExchange(
            ref _state,
            (int)WorkerLifecycleState.Running,
            (int)WorkerLifecycleState.Starting);

    public bool BeginDraining()
    {
        while (true)
        {
            WorkerLifecycleState current = State;
            if (current is WorkerLifecycleState.Draining or
                WorkerLifecycleState.Stopped)
            {
                return false;
            }

            if (Interlocked.CompareExchange(
                    ref _state,
                    (int)WorkerLifecycleState.Draining,
                    (int)current) == (int)current)
            {
                return true;
            }
        }
    }

    public void MarkStopped() =>
        Interlocked.Exchange(
            ref _state,
            (int)WorkerLifecycleState.Stopped);
}
