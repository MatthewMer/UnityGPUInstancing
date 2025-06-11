using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Custom
{
    namespace GraphicsBuffer
    {
        public interface IGraphicsBuffer<Tdesc> : IDisposable
        {
            public Tdesc Descriptor { get; }
        }

        public abstract class ManagedGraphicsBuffer<Tbuffer, Tdesc> : IGraphicsBuffer<Tdesc>
        {
            protected Tbuffer m_Buffer;
            public Tbuffer Buffer => m_Buffer;
            protected readonly Tdesc m_Descriptor;
            public Tdesc Descriptor => m_Descriptor;

            protected bool m_Disposed = false;

            public ManagedGraphicsBuffer(Tbuffer buffer, Tdesc descriptor)
            {
                m_Buffer = buffer;
                m_Descriptor = descriptor;
            }

            public abstract void Dispose();
        }

        public struct ComputeBufferDescriptor
        {
            public int count;
            public int stride;
            public ComputeBufferType type;
        }

        public class ManagedComputeBuffer : ManagedGraphicsBuffer<ComputeBuffer, ComputeBufferDescriptor>
        {
            public ManagedComputeBuffer(ComputeBufferDescriptor desc)
                : base(new ComputeBuffer(desc.count, desc.stride, desc.type), desc)
            { }

            ~ManagedComputeBuffer()
                => Dispose(disposing: false);

            public void Release()
                => Dispose();

            public override void Dispose()
                => Dispose(disposing: true);

            private void Dispose(bool disposing)
            {
                if (m_Disposed) return;
                m_Disposed = true;

                if (m_Buffer != null)
                {
                    m_Buffer.Release();
                    m_Buffer = null;
                    if (disposing)
                        GC.SuppressFinalize(this);
                }
            }

            public void SetData(Array data)
                => m_Buffer.SetData(data);

            public void SetData<T>(List<T> data) where T : struct
                => m_Buffer.SetData(data);

            public void SetData<T>(NativeArray<T> data) where T : struct
                => m_Buffer.SetData(data);

            public void GetData(Array data)
                => m_Buffer.GetData(data);

            public void SetCounterValue(uint counterValue)
                => m_Buffer.SetCounterValue(counterValue);
        }

        public class ManagedRenderTexture : ManagedGraphicsBuffer<RenderTexture, RenderTextureDescriptor>
        {
            public ManagedRenderTexture(RenderTextureDescriptor desc)
                : base(new RenderTexture(desc), desc)
            { }

            ~ManagedRenderTexture()
                => Dispose(disposing: false);

            public void Release()
                => Dispose();

            public override void Dispose()
                => Dispose(disposing: true);

            private void Dispose(bool disposing)
            {
                if (m_Disposed) return;
                m_Disposed = true;

                if (m_Buffer != null)
                {
                    m_Buffer.Release();
                    m_Buffer = null;
                    if (disposing)
                        GC.SuppressFinalize(this);
                }
            }
        }
    }
}