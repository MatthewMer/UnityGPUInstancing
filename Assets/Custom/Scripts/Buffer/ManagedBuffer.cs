using System;

namespace Custom
{
    namespace Buffer
    {
        // @brief buffer descriptors are hashed and compared based on their members 
        public interface IBufferDescriptor<Tdesc> : IEquatable<Tdesc>
        { }

        public interface IManagedBuffer<Tdesc> : IDisposable
        {
            public Tdesc Descriptor { get; }
        }

        // @brief buffers are compared based on their internal base address
        public abstract class ManagedBuffer<Tbuffer, Tdesc> : IManagedBuffer<Tdesc>, IEquatable<ManagedBuffer<Tbuffer, Tdesc>>
        {
            protected Tbuffer m_Buffer;
            protected readonly Tdesc m_Descriptor;

            public Tbuffer Buffer => m_Buffer;
            public Tdesc Descriptor => m_Descriptor;

            private bool m_Disposed = false;

            public ManagedBuffer(Tbuffer buffer, Tdesc descriptor)
            {
                m_Buffer = buffer;
                m_Descriptor = descriptor;
            }

            ~ManagedBuffer()
                => Dispose(false);

            public void Dispose()
            {
                if (m_Disposed) return;
                Dispose(true);
                m_Disposed = true;

                GC.SuppressFinalize(this);
            }

            protected abstract void Dispose(bool dispose);

            public override bool Equals(object obj)
                => Equals(obj as ManagedBuffer<Tbuffer, Tdesc>);

            public abstract bool Equals(ManagedBuffer<Tbuffer, Tdesc> other);

            public override int GetHashCode()
                => m_Buffer?.GetHashCode() ?? 0;
        }
    }
}