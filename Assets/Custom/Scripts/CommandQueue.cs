using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Custom;

public class CommandQueue
{
    public enum FlushMode
    {
        Update,
        LateUpdate,
        Background
    }

    protected readonly Queue<Action>[] m_CommandQueues = { new(), new() };
    private int m_ExecuteIndex = 0;

    protected readonly FlushMode m_FlushMode;
    private Action m_AfterExecute;

    private readonly object m_EnqueueLock = new object();
    protected readonly object m_FlushLock = new object();

    protected bool m_IsFlushing = false;
    protected bool m_FlushRequested = false;

    public CommandQueue(FlushMode mode, Action afterExecute = null)
    {
        m_FlushMode = mode;
        m_AfterExecute = afterExecute;
    }

    public virtual void Enqueue(Action action)
    {
        lock (m_EnqueueLock)
        {
            m_CommandQueues[1 - m_ExecuteIndex].Enqueue(action);
        }
    }

    public void Flush()
    {
        if (CheckFlush())
        {
            FlushInternal();
        }
    }

    protected void FlushInternal()
    {
        lock (m_EnqueueLock) m_ExecuteIndex = 1 - m_ExecuteIndex;

        while (m_CommandQueues[m_ExecuteIndex].Count > 0)
        {
            m_CommandQueues[m_ExecuteIndex].Dequeue().Invoke();
        }

        m_AfterExecute?.Invoke();

        lock (m_FlushLock)
        {
            m_IsFlushing = false;
            if (m_FlushRequested)
            {
                m_FlushRequested = false;
                DispatchFlush();
            }
        }
    }

    protected bool CheckFlush()
    {
        lock (m_FlushLock)
        {
            if (!m_IsFlushing)
            {
                m_IsFlushing = true;
                return true;
            }
            else
            {
                m_FlushRequested = true;
                return false;
            }
        }
    }

    protected void DispatchFlush()
    {
        switch (m_FlushMode)
        {
            case FlushMode.Update:
                UnityMainThreadDispatcher.EnqueueUpdate(Flush);
                break;
            case FlushMode.LateUpdate:
                UnityMainThreadDispatcher.EnqueueLateUpdate(Flush);
                break;
            case FlushMode.Background:
                UnityMainThreadDispatcher.EnqueueLateUpdate(() => { Task.Run(FlushInternal); });
                break;
        }
    }
}

public class CommandQueueAutoFlush : CommandQueue
{
    public CommandQueueAutoFlush(FlushMode mode, Action afterExecute = null)
        : base(mode, afterExecute)
    { }

    public override void Enqueue(Action action)
    {
        base.Enqueue(action);

        if (CheckFlush())
        {
            DispatchFlush();
        }
    }
}