using UnityEngine;
using System;
using System.Threading.Tasks;
using Unity.Jobs;

namespace Custom
{
    namespace Threading
    {
        public static class UnityWorker
        {
            // Runs the action on a background thread without waiting for it to complete.
            public static void Run(Action action) => Task.Run(action);

            // Runs the action on a background thread and returns a Task to await its completion.
            public static Task RunAsync(Action action) => Task.Run(action);

            // Runs the function on a background thread and returns a Task<T> with the result.
            public static Task<T> RunAsync<T>(Func<T> func) => Task.Run(func);

            // helpers for burst compiled IJobs
            public static JobHandle RunJob<T>(T job) where T : struct, IJob
                => job.Schedule();

            public static JobHandle RunJobParallelFor<T>(T job, int length, int batchSize = 64) where T : struct, IJobParallelFor
                => job.Schedule(length, batchSize);

            public static void CompleteJob<T>(T job) where T : struct, IJob
            {
                var jobHandle = job.Schedule();
                jobHandle.Complete();
            }

            public static void CompleteJobParallelFor<T>(T job, int length, int batchSize = 64) where T : struct, IJobParallelFor
            {
                var jobHandle = job.Schedule(length, batchSize);
                jobHandle.Complete();
            }
        }
    }
}