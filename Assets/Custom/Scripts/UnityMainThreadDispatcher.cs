using System;
using UnityEngine;

// simple main thread dispatcher for Unity
namespace Custom
{
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly CommandQueue m_UpdateQueue = new(CommandQueue.FlushMode.Update);
        private static readonly CommandQueue m_LateUpdateQueue = new(CommandQueue.FlushMode.LateUpdate);

        public static void EnqueueUpdate(Action action)
        {
            m_UpdateQueue.Enqueue(action);
        }

        public static void EnqueueLateUpdate(Action action)
        {
            m_LateUpdateQueue.Enqueue(action);
        }

        private void Update()
        {
            m_UpdateQueue.Flush();
        }

        private void LateUpdate()
        {
            m_LateUpdateQueue.Flush();
        }
    }
}