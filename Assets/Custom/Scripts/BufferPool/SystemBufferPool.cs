using Custom.Buffer;
using Custom.BufferPool.BufferPoolHelper;
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Custom
{
    namespace BufferPool
    {
        public struct SystemBufferPoolParams : IBatchParams
        {
            public int batchSize;
            public int baseSize;
            public BufferMode mode;
            public int ttl;

            public BufferMode Mode => mode;
            public int BaseSize => baseSize;
            public int BatchSize => batchSize;
        }

        public class SystemBufferPool<T> : BufferPool<ManagedSystemBuffer<T>, SystemBufferDescriptor>
            where T : struct
        {
            private readonly SystemBufferPoolParams m_Params;
            public SystemBufferPoolParams Parameters => m_Params;

            private const Allocator k_AllocatorMode = Allocator.Persistent;

            public SystemBufferPool(SystemBufferPoolParams param)
                : base(param.ttl)
            { }

            public override void Dispose()
                => base.Dispose();

            protected override ManagedSystemBuffer<T> AllocateBuffer(SystemBufferDescriptor desc)
            {
                return new ManagedSystemBuffer<T>(desc);
            }

            public ManagedSystemBuffer<T> Rent(int count)
            {
                if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "count must be greater than 0");
                count = BatchHelper.CeilToBaseSize(m_Params, count);

                var desc = new SystemBufferDescriptor()
                {
                    count = count,
                    allocator = k_AllocatorMode
                };

                switch (m_Params.mode)
                {
                    case BufferMode.Precise:
                        desc.count = count;
                        return RentInternal(desc);

                    case BufferMode.Batched:
                        desc.count = BatchHelper.CeilToBatch(m_Params, count);
                        return RentInternal(desc);

                    default:
                        throw new NotSupportedException();
                }
            }

            public bool Return(ManagedSystemBuffer<T> buffer)
            {
                if (IsRented(buffer) || IsCompatible(buffer.Descriptor))
                {
                    return ReturnInternal(buffer);
                }
                else
                {
                    return false;
                }
            }

            public bool NeedsReallocation(ManagedSystemBuffer<T> buffer, int newCount)
            {
                var desc = buffer.Descriptor;

                if (!IsCompatible(desc))
                {
                    return true;
                }

                return BatchHelper.NeedsReallocation(m_Params, desc.count, newCount);
            }

            public bool IsCompatible(SystemBufferDescriptor desc)
            {
                return
                    0 < desc.count &&
                    m_Params.baseSize <= desc.count &&
                    k_AllocatorMode == desc.allocator &&
                    BatchHelper.IsCompatible(m_Params, desc.count);
            }
        }

        public class GenericSystemBufferPool : IDisposable
        {
            private readonly SystemBufferPoolParams m_Params;
            public SystemBufferPoolParams Parameters => m_Params;

            private readonly Dictionary<Type, object> k_SystemBufferPools = new();

            public GenericSystemBufferPool(SystemBufferPoolParams param)
            {
                m_Params = param;
            }

            ~GenericSystemBufferPool()
                => Dispose();

            public void Dispose()
            {
                foreach (var poolObj in k_SystemBufferPools.Values)
                {
                    if(poolObj is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                k_SystemBufferPools.Clear();
            }

            private SystemBufferPool<T> GetPool<T>() where T : struct
            {
                var type = typeof(T);
                if (!k_SystemBufferPools.TryGetValue(type, out var poolObj))
                {
                    var pool = new SystemBufferPool<T>(m_Params);
                    k_SystemBufferPools[type] = pool;
                    return pool;
                }
                return (SystemBufferPool<T>)poolObj;
            }

            public ManagedSystemBuffer<T> Rent<T>(int count) where T : struct
            {
                var pool = GetPool<T>();
                return pool.Rent(count);
            }

            public bool Return<T>(ManagedSystemBuffer<T> buffer) where T : struct
            {
                var pool = GetPool<T>();
                return pool.Return(buffer);
            }
        }
    }
}