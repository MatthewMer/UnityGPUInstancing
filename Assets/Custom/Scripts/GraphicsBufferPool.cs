using System.Collections.Generic;
using Custom.GraphicsBuffer;
using System;
using UnityEngine;
using Unity.VisualScripting;

namespace Custom
{
    namespace GraphicsBufferPool
    {
        public enum BufferMode
        {
            Precise,
            Batched
        }

        public abstract class GraphicsBufferPool<Tbuffer, Tdesc> : IDisposable
            where Tbuffer : IManagedBuffer<Tdesc>
        {
            public delegate Tbuffer CreateDelegate(Tdesc desc);
            private readonly CreateDelegate m_CreateFunc;

            private readonly object m_Lock = new();
            private readonly TimeSpan m_TTL;
            private readonly System.Threading.Timer m_CleanupTimer;

            private readonly Dictionary<object, Stack<PooledBuffer>> m_Free = new();
            private readonly HashSet<Tbuffer> m_Reserved = new();

            private class PooledBuffer
            {
                public Tbuffer buffer;
                public DateTime lastUsed;
            }

            public GraphicsBufferPool(CreateDelegate createFunc, int ttl)
            {
                m_CreateFunc = createFunc;
                m_TTL = TimeSpan.FromSeconds(ttl);

                if (ttl > 0)
                {
                    m_CleanupTimer = new System.Threading.Timer(CleanupCallback, null, m_TTL, m_TTL);
                }
            }

            protected Tbuffer RentInternal(Tdesc desc)
            {
                lock (m_Lock)
                {
                    Tbuffer buffer;
                    if (m_Free.TryGetValue(desc, out var stack) && stack.Count > 0)
                    {
                        var pooledBuffer = stack.Pop();
                        if (stack.Count == 0) m_Free.Remove(desc);

                        buffer = pooledBuffer.buffer;
                    }
                    else
                    {
                        buffer = m_CreateFunc(desc);
                    }

                    m_Reserved.Add(buffer);
                    return buffer;
                }
            }

            protected void ReturnInternal(Tbuffer buffer)
            {
                lock (m_Lock)
                {
                    m_Reserved.Remove(buffer);

                    var desc = buffer.Descriptor;
                    if (!m_Free.TryGetValue(desc, out var stack))
                    {
                        stack = new Stack<PooledBuffer>();
                        m_Free[desc] = stack;
                    }

                    PooledBuffer pooledBuffer = new()
                    {
                        buffer = buffer,
                        lastUsed = DateTime.UtcNow
                    };
                    stack.Push(pooledBuffer);
                }
            }

            public void Dispose()
            {
                lock (m_Lock)
                {
                    foreach (var (desc, stack) in m_Free)
                    {
                        while (stack.TryPop(out var pooledBuffer))
                        {
                            pooledBuffer.buffer.Dispose();
                        }
                        stack.Clear();
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
                    foreach (var (desc, stack) in m_Free)
                    {
                        if (stack.Count > 0)
                        {
                            var stackCopy = new Stack<PooledBuffer>(stack);
                            stack.Clear();

                            while (stackCopy.TryPop(out var pooledBuffer))
                            {
                                if (now - pooledBuffer.lastUsed > m_TTL)
                                {
                                    UnityMainThreadDispatcher.EnqueueLateUpdate(pooledBuffer.buffer.Dispose);
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

        public struct ComputeBufferPoolParams
        {
            public int baseSize;
            public int stride;
            public ComputeBufferType type;
            public BufferMode mode;
            public int batchSize;
            public int ttl;
        }

        public class ComputeBufferPool : GraphicsBufferPool<ManagedComputeBuffer, ComputeBufferDescriptor>
        {
            private readonly ComputeBufferPoolParams m_Params;
            public ComputeBufferPoolParams Params => m_Params;

            public ComputeBufferPool(ComputeBufferPoolParams param)
                : base(Create, param.ttl)
            {
                m_Params = param;
            }

            private static ManagedComputeBuffer Create(ComputeBufferDescriptor desc)
            {
                return new ManagedComputeBuffer(desc);
            }

            public ManagedComputeBuffer Rent(int count)
            {
                if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "count must be greater than 0");
                count = CeilToBaseSize(count);

                var desc = new ComputeBufferDescriptor()
                {
                    stride = m_Params.stride,
                    type = m_Params.type
                };

                switch (m_Params.mode)
                {
                    case BufferMode.Precise:
                        desc.count = count;
                        return RentInternal(desc);

                    case BufferMode.Batched:
                        desc.count = CeilToBatch(count);
                        return RentInternal(desc);

                    default:
                        throw new NotSupportedException();
                }
            }

            public bool Return(ManagedComputeBuffer buffer)
            {
                var desc = buffer.Descriptor;
                if (IsCompatible(desc))
                {
                    ReturnInternal(buffer);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public bool NeedsReallocation(ManagedComputeBuffer buffer, int newCount)
            {
                var desc = buffer.Descriptor;

                if (!IsCompatible(desc))
                {
                    return true;
                }

                newCount = CeilToBaseSize(newCount);

                switch (m_Params.mode)
                {
                    case BufferMode.Precise:
                        return desc.count != newCount;

                    case BufferMode.Batched:
                        return
                            CeilToBatch(desc.count) != CeilToBatch(newCount);
                    default:
                        throw new NotSupportedException();
                }
            }

            public bool IsCompatible(ComputeBufferDescriptor desc)
            {
                return
                    0 < desc.count &&
                    m_Params.baseSize <= desc.count &&
                    m_Params.stride == desc.stride &&
                    m_Params.type == desc.type &&
                    (m_Params.mode == BufferMode.Batched ?
                    (desc.count % m_Params.batchSize == 0) :
                    true);
            }

            private int CeilToBaseSize(int size)
            {
                return size > m_Params.baseSize ? size : m_Params.baseSize;
            }

            private int CeilToBatch(int size)
            {
                var x = CeilToBaseSize(size);
                int batchSize = m_Params.batchSize;
                return ((x + batchSize - 1) / batchSize) * batchSize;
            }
        }

        /*
        public struct RenderTexturePoolParams
        {

        }

        public class RenderTexturePool : GraphicsBufferPool<ManagedRenderTexture, RenderTextureDescriptor>
        {
            private readonly RenderTexturePoolParams m_Params;
            public RenderTexturePoolParams Params => m_Params;

            public RenderTexturePool(RenderTexturePoolParams param)
                : base(Create)
            {
                m_Params = param;
            }

            private static ManagedRenderTexture Create(RenderTextureDescriptor desc)
            {
                return new ManagedRenderTexture(desc);
            }

            public ManagedRenderTexture Rent(RenderTextureDescriptor desc)
            {
                throw new NotImplementedException();
            }

            public void Return(ManagedRenderTexture buffer)
            {
                throw new NotImplementedException();
            }
        }
        */
    }
}