using Custom.BufferPool;
using System;
using Unity.Collections;

namespace Custom
{
    namespace Buffer
    {
        public struct SystemBufferDescriptor : IBufferDescriptor<SystemBufferDescriptor>
        {
            public int count;
            public Allocator allocator;

            public bool Equals(SystemBufferDescriptor other)
                => (count, allocator).Equals((other.count, other.allocator));

            public override int GetHashCode()
                => (count, allocator).GetHashCode();
        }

        public class ManagedSystemBuffer<T> : ManagedBuffer<NativeArray<T>, SystemBufferDescriptor>
            where T : struct
        {
            public ManagedSystemBuffer(SystemBufferDescriptor desc)
                : base(new NativeArray<T>(desc.count, desc.allocator), desc)
            { }

            protected override void Dispose(bool disposing)
            {
                if (m_Buffer.IsCreated)
                {
                    m_Buffer.Dispose();
                }
            }

            public override bool Equals(ManagedBuffer<NativeArray<T>, SystemBufferDescriptor> other)
                => m_Buffer.Equals(other?.Buffer);
        }
    }
}