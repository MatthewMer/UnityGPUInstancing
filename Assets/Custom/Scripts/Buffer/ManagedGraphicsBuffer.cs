using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Custom
{
    namespace Buffer
    {
        public struct ComputeBufferDescriptor : IBufferDescriptor<ComputeBufferDescriptor>
        {
            public int count;
            public int stride;
            public ComputeBufferType type;

            public bool Equals(ComputeBufferDescriptor other)
                => (count, stride, type).Equals((other.count, other.stride, other.type));

            public override int GetHashCode()
                => (count, stride, type).GetHashCode();
        }

        public abstract class ManagedGraphicsBuffer<Tbuffer, Tdesc> : ManagedBuffer<Tbuffer, Tdesc>
            where Tbuffer : class
        {
            protected ManagedGraphicsBuffer(Tbuffer buffer, Tdesc descriptor) 
                : base(buffer, descriptor)
            { }

            public override bool Equals(ManagedBuffer<Tbuffer, Tdesc> other)
            {
                if (other == null) return false;
                if (ReferenceEquals(this, other)) return true;

                return ReferenceEquals(m_Buffer, other.Buffer);
            }
        }

        public class ManagedComputeBuffer : ManagedGraphicsBuffer<ComputeBuffer, ComputeBufferDescriptor>
        {
            private readonly int m_Count;
            public int Count => m_Count;

            public ManagedComputeBuffer(ComputeBufferDescriptor desc)
                : base(new ComputeBuffer(desc.count, desc.stride, desc.type), desc)
            {
                m_Count = desc.count;
            }

            protected override void Dispose(bool disposing)
            {
                if (m_Buffer != null)
                {
                    m_Buffer.Release();
                    m_Buffer = null;
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

        public struct RenderTextureDescriptorWrapper : IBufferDescriptor<RenderTextureDescriptor>
        {
            public RenderTextureDescriptor descriptor;

            public bool Equals(RenderTextureDescriptor other)
            {
                return
                    other.width == descriptor.width &&
                    other.height == descriptor.height &&
                    other.colorFormat == descriptor.colorFormat &&
                    other.depthBufferBits == descriptor.depthBufferBits &&
                    other.dimension == descriptor.dimension &&
                    other.volumeDepth == descriptor.volumeDepth &&
                    other.msaaSamples == descriptor.msaaSamples &&
                    other.useMipMap == descriptor.useMipMap &&
                    other.autoGenerateMips == descriptor.autoGenerateMips &&
                    other.enableRandomWrite == descriptor.enableRandomWrite &&
                    other.useDynamicScale == descriptor.useDynamicScale &&
                    other.sRGB == descriptor.sRGB &&
                    other.bindMS == descriptor.bindMS;
            }

            // the buffer pool only accepts textures with same parameters, so reduce hash to size
            public override int GetHashCode()
                => (
                    descriptor.width,
                    descriptor.height,
                    descriptor.colorFormat,
                    descriptor.depthBufferBits,
                    descriptor.dimension,
                    descriptor.volumeDepth,
                    descriptor.msaaSamples,
                    descriptor.useMipMap,
                    descriptor.autoGenerateMips,
                    descriptor.enableRandomWrite,
                    descriptor.useDynamicScale,
                    descriptor.sRGB,
                    descriptor.bindMS
                ).GetHashCode();
        }

        public class ManagedRenderTexture : ManagedGraphicsBuffer<RenderTexture, RenderTextureDescriptorWrapper>
        {
            private readonly int m_Width;
            public int Width => m_Width;
            private readonly int m_Height;
            public int Height => m_Height;

            public ManagedRenderTexture(RenderTextureDescriptorWrapper desc, FilterMode filterMode, TextureWrapMode wrapMode)
                : base(CreateAndConfigureTexture(desc, filterMode, wrapMode), desc)
            { 
                m_Width = Buffer.width;
                m_Height = Buffer.height;
            }

            protected override void Dispose(bool disposing)
            {
                if (m_Buffer != null)
                {
                    m_Buffer.Release();
                    m_Buffer = null;
                }
            }

            private static RenderTexture CreateAndConfigureTexture(RenderTextureDescriptorWrapper desc, FilterMode filterMode, TextureWrapMode wrapMode)
            {
                var texture = new RenderTexture(desc.descriptor)
                {
                    filterMode = filterMode,
                    wrapMode = wrapMode
                };
                texture.Create();
                return texture;
            }
        }
    }
}