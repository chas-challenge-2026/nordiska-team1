namespace Nordiska.Reporting.Worker.Lifecycle;


/*
    This class monitors/stores the state of a worker (whether its
    running,starting, draining och stopped). 
*/


internal enum WorkerLifecycleState
{
    Starting,   // worker loop is starting but not ready to accept jobs yet
    Running,    // new jobs can be accepted by the worker
    Draining,   // active work will progress but pauses intake of new jobs
    Stopped     // worker loop has ended
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

    /*
        This is bascially a graceful shutdown. So when the worker life should end
        we want to let its active jobs finish instead of shutting down immedietly. 

     */

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
