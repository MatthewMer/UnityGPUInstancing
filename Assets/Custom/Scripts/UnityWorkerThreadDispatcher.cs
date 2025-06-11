using UnityEngine;
using System;
using System.Threading.Tasks;

namespace Custom
{
    public class UnityWorkerThreadDispatcher : MonoBehaviour
    {
        public static void Enqueue(Action action)
        {
            Task.Run(action);
        }

        public static Task EnqueueAsync(Action action)
        {
            return Task.Run(action);
        }
    }
}