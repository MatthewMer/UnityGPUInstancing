using Custom.Buffer;
using System.Collections.Generic;
using System;
using Custom.Threading;

namespace Custom
{
    namespace BufferPool
    {
        namespace BufferPoolHelper
        {
            public interface IBatchParams
            {
                public BufferMode Mode { get; }
                public int BaseSize { get; }
                public int BatchSize { get; }
            }

            public static class BatchHelper
            {
                public static int CeilToBaseSize(IBatchParams param, int count)
                {
                    return count > param.BaseSize ? count : param.BaseSize;
                }

                public static int CeilToBatch(IBatchParams param, int count)
                {
                    var x = CeilToBaseSize(param, count);
                    return ((x + param.BatchSize - 1) / param.BatchSize) * param.BatchSize;
                }

                public static bool IsCompatible(IBatchParams param, int count)
                {
                    return
                        param.Mode == BufferMode.Batched ?
                        (count % param.BatchSize == 0) :
                        true;
                }

                public static bool NeedsReallocation(IBatchParams param, int count, int newCount)
                {
                    newCount = CeilToBaseSize(param, newCount);

                    switch (param.Mode)
                    {
                        case BufferMode.Precise:
                            return count != newCount;

                        case BufferMode.Batched:
                            return
                                CeilToBatch(param, count) != CeilToBatch(param, newCount);
                        default:
                            throw new NotSupportedException();
                    }
                }
            }
        }

        public enum BufferMode
        {
            Precise,
            Batched
        }

        public abstract class BufferPool<Tbuffer, Tdesc> : IDisposable
            where Tbuffer : IManagedBuffer<Tdesc>
        {
            private readonly object m_Lock = new();
            private readonly TimeSpan m_TTL;
            private readonly System.Threading.Timer m_CleanupTimer;

            private readonly Dictionary<Tdesc, Stack<PooledBuffer>> m_FreeDict = new();

            private readonly HashSet<Tbuffer> m_Free = new();
            private readonly HashSet<Tbuffer> m_Reserved = new();

            private class PooledBuffer : IEquatable<PooledBuffer>
            {
                public Tbuffer buffer;
                public DateTime lastUsed;

                public bool Equals(PooledBuffer other)
                    => EqualityComparer<Tbuffer>.Default.Equals(buffer, other.buffer);

                public override int GetHashCode()
                    => EqualityComparer<Tbuffer>.Default.GetHashCode(buffer);
            }

            public BufferPool(int ttl)
            {
                m_TTL = TimeSpan.FromSeconds(ttl);

                if (ttl > 0)
                {
                    m_CleanupTimer = new System.Threading.Timer(CleanupCallback, null, m_TTL, m_TTL);
                }
            }

            protected abstract Tbuffer AllocateBuffer(Tdesc desc);

            public bool IsRented(Tbuffer buffer)
            {
                lock (m_Lock)
                {
                    return m_Reserved.Contains(buffer);
                }
            }

            public bool IsRegistered(Tbuffer buffer)
            {
                lock (m_Lock)
                {
                    return m_Reserved.Contains(buffer) && m_Free.Contains(buffer);
                }
            }

            protected Tbuffer RentInternal(Tdesc desc)
            {
                lock (m_Lock)
                {
                    if (m_FreeDict.TryGetValue(desc, out var stack) && stack.Count > 0)
                    {
                        var pooledBuffer = stack.Pop();
                        if (stack.Count == 0) m_FreeDict.Remove(desc);

                        var buffer = pooledBuffer.buffer;
                        m_Free.Remove(buffer);
                        m_Reserved.Add(buffer);
                        return buffer;
                    }
                }

                var newBuffer = AllocateBuffer(desc);

                lock (m_Lock)
                {
                    m_Free.Remove(newBuffer);
                    m_Reserved.Add(newBuffer);
                }

                return newBuffer;
            }

            protected bool ReturnInternal(Tbuffer buffer)
            {
                lock (m_Lock)
                {
                    if (m_Free.Contains(buffer))
                    {
                        return false;
                    }
                    else
                    {
                        m_Reserved.Remove(buffer);
                        m_Free.Add(buffer);

                        var desc = buffer.Descriptor;
                        if (!m_FreeDict.TryGetValue(desc, out var stack))
                        {
                            stack = new Stack<PooledBuffer>();
                            m_FreeDict[desc] = stack;
                        }

                        PooledBuffer pooledBuffer = new()
                        {
                            buffer = buffer,
                            lastUsed = DateTime.UtcNow
                        };
                        stack.Push(pooledBuffer);

                        return true;
                    }
                }
            }

            public virtual void Dispose()
            {
                lock (m_Lock)
                {
                    m_FreeDict.Clear();

                    foreach (var buffer in m_Free)
                    {
                        buffer.Dispose();
                    }
                    m_Free.Clear();

                    foreach (var buffer in m_Reserved)
                    {
                        buffer.Dispose();
                    }
                    m_Reserved.Clear();
                }
            }

            private void CleanupCallback(object state)
            {
                lock (m_Lock)
                {
                    var now = DateTime.UtcNow;
                    foreach (var (desc, stack) in m_FreeDict)
                    {
                        if (stack.Count > 0)
                        {
                            var stackCopy = new Stack<PooledBuffer>(stack);
                            stack.Clear();

                            while (stackCopy.TryPop(out var pooledBuffer))
                            {
                                if (now - pooledBuffer.lastUsed > m_TTL)
                                {
                                    UnityMainThreadDispatcher.ScheduleLateUpdate(pooledBuffer.buffer.Dispose);
                                }
                                else
                                {
                                    stack.Push(pooledBuffer);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}