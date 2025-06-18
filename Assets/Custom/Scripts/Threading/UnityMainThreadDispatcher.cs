using System;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Custom
{
    namespace Threading {
        public class UnityMainThreadDispatcher : MonoBehaviour
        {
            private class QueueWrapper
            {
                private readonly Queue<Action>[] m_Queue = { new(), new() };
                private int m_ExecuteIndex = 0;
                private readonly object m_EnqueueLock = new object();

                public void Enqueue(Action action)
                {
                    lock (m_EnqueueLock)
                    {
                        m_Queue[1 - m_ExecuteIndex].Enqueue(action);
                    }
                }

                public void Flush()
                {
                    lock (m_EnqueueLock) m_ExecuteIndex = 1 - m_ExecuteIndex;

                    while (m_Queue[m_ExecuteIndex].TryDequeue(out var action))
                    {
                        action.Invoke();
                    }
                }
            }

            private readonly static QueueWrapper s_UpdateQueue = new QueueWrapper();
            private readonly static QueueWrapper s_LateUpdateQueue = new QueueWrapper();

            private static void EnqueueUpdate(Action action)
            {
                s_UpdateQueue.Enqueue(action);
            }

            private static void EnqueueLateUpdate(Action action)
            {
                s_LateUpdateQueue.Enqueue(action);
            }

            private void Update()
            {
                s_UpdateQueue.Flush();
            }

            private void LateUpdate()
            {
                s_LateUpdateQueue.Flush();
            }

            private static int s_MainThreadId;
            public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == s_MainThreadId;

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
            private static void Init()
            {
                s_MainThreadId = Thread.CurrentThread.ManagedThreadId;
            }

            public static void ScheduleUpdate(Action action)
            {
                EnqueueUpdate(() =>
                {
                    try { action.Invoke(); }
                    catch (Exception ex) { Debug.LogException(ex); }
                });
            }

            public static void ScheduleLateUpdate(Action action)
            {
                EnqueueLateUpdate(() =>
                {
                    try { action.Invoke(); }
                    catch (Exception ex) { Debug.LogException(ex); }
                });
            }

            public static void ScheduleUpdate<T>(Func<T> func, Action<T> resultHandler)
            {
                EnqueueUpdate(() =>
                {
                    try { resultHandler(func()); }
                    catch (Exception ex) { Debug.LogException(ex); }
                });
            }

            public static void ScheduleLateUpdate<T>(Func<T> func, Action<T> resultHandler)
            {
                EnqueueLateUpdate(() =>
                {
                    try { resultHandler(func()); }
                    catch (Exception ex) { Debug.LogException(ex); }
                });
            }

            public static async Task<Tres> AwaitUpdate<Tres>(Func<Tres> func)
            {
                var tcs = new TaskCompletionSource<Tres>();

                EnqueueUpdate(() =>
                {
                    try { tcs.SetResult(func.Invoke()); }
                    catch (Exception ex) { tcs.SetException(ex); }
                });

                return await tcs.Task.ConfigureAwait(false);
            }

            public static async Task<Tres> AwaitLateUpdate<Tres>(Func<Tres> func)
            {
                var tcs = new TaskCompletionSource<Tres>();

                EnqueueLateUpdate(() =>
                {
                    try { tcs.SetResult(func.Invoke()); }
                    catch (Exception ex) { tcs.SetException(ex); }
                });

                return await tcs.Task.ConfigureAwait(false);
            }

            public static Task<Tres> RunUpdate<Tres>(Func<Tres> func)
            {
                var tcs = new TaskCompletionSource<Tres>();

                EnqueueUpdate(() =>
                {
                    try { tcs.SetResult(func.Invoke()); }
                    catch (Exception ex) { tcs.SetException(ex); }
                });

                return tcs.Task;
            }

            public static Task<Tres> RunLateUpdate<Tres>(Func<Tres> func)
            {
                var tcs = new TaskCompletionSource<Tres>();

                EnqueueLateUpdate(() =>
                {
                    try { tcs.SetResult(func.Invoke()); }
                    catch (Exception ex) { tcs.SetException(ex); }
                });

                return tcs.Task;
            }
        }
    }
}