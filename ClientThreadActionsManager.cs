using System;
using System.Collections.Concurrent;

public class ClientThreadActionsManager
{
    private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

    public void EnqueueAction(Action action)
    {
        _mainThreadActions.Enqueue(action);
    }

    public void ProcessActions()
    {
        while (_mainThreadActions.TryDequeue(out Action action))
        {
            action.Invoke();
        }
    }
}
