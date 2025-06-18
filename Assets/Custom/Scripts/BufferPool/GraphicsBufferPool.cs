using System;
using UnityEngine;
using UnityEngine.Rendering;
using Custom.Buffer;
using Custom.Threading;
using Custom.BufferPool.BufferPoolHelper;

namespace Custom
{
    namespace BufferPool
    {
        public abstract class GraphicsBufferPool<Tbuffer, Tdesc> : BufferPool<Tbuffer, Tdesc> where Tbuffer : IManagedBuffer<Tdesc>
        {
            public GraphicsBufferPool(int ttl)
                : base(ttl) 
            { }

            protected abstract Tbuffer Create(Tdesc desc);

            protected override Tbuffer AllocateBuffer(Tdesc desc)
            {
                if (UnityMainThreadDispatcher.IsMainThread)
                {
                    return Create(desc);
                }
                else
                {
                    return UnityMainThreadDispatcher.AwaitLateUpdate(() => Create(desc)).Result;
                }
            }
        }

        public struct ComputeBufferPoolParams : IBatchParams
        {
            public int baseSize;
            public int stride;
            public int batchSize;
            public ComputeBufferType type;
            public BufferMode mode;
            public int ttl;

            public BufferMode Mode => mode;
            public int BaseSize => baseSize;
            public int BatchSize => batchSize;
        }

        public class ComputeBufferPool : GraphicsBufferPool<ManagedComputeBuffer, ComputeBufferDescriptor>
        {
            private readonly ComputeBufferPoolParams m_Params;
            public ComputeBufferPoolParams Parameters => m_Params;

            public ComputeBufferPool(ComputeBufferPoolParams param)
                : base(param.ttl)
            {
                m_Params = param;
            }

            protected override ManagedComputeBuffer Create(ComputeBufferDescriptor desc)
            {
                return new ManagedComputeBuffer(desc);
            }

            public ManagedComputeBuffer Rent(int count)
            {
                if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "count must be greater than 0");
                count = BatchHelper.CeilToBaseSize(m_Params, count);

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
                        desc.count = BatchHelper.CeilToBatch(m_Params, count);
                        return RentInternal(desc);

                    default:
                        throw new NotSupportedException();
                }
            }

            public bool Return(ManagedComputeBuffer buffer)
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

            public bool NeedsReallocation(ManagedComputeBuffer buffer, int newCount)
            {
                var desc = buffer.Descriptor;

                if (!IsCompatible(desc))
                {
                    return true;
                }

                return BatchHelper.NeedsReallocation(m_Params, desc.count, newCount);
            }

            public bool IsCompatible(ComputeBufferDescriptor desc)
            {
                return
                    0 < desc.count &&
                    m_Params.baseSize <= desc.count &&
                    m_Params.stride == desc.stride &&
                    m_Params.type == desc.type &&
                    BatchHelper.IsCompatible(m_Params, desc.count);
            }
        }

        public struct RenderTexturePoolParams
        {
            public RenderTextureFormat colorFormat;
            public int depthBufferBits;
            public TextureDimension dimension;
            public int volumeDepth;
            public FilterMode filterMode;
            public TextureWrapMode wrapMode;
            public RenderTextureReadWrite readWrite;
            public bool useMipMap;
            public bool autoGenerateMips;
            public bool enableRandomWrite;
            public int msaaSamples;
            public bool useDynamicScale;
            public bool bindMS;
            public int ttl;
        }

        public class RenderTexturePool : GraphicsBufferPool<ManagedRenderTexture, RenderTextureDescriptorWrapper>
        {
            private readonly RenderTexturePoolParams m_Params;
            public RenderTexturePoolParams Parameters => m_Params;

            public RenderTexturePool(RenderTexturePoolParams param)
                : base(param.ttl)
            {
                m_Params = param;
            }

            protected override ManagedRenderTexture Create(RenderTextureDescriptorWrapper desc)
            {
                return new ManagedRenderTexture(desc, m_Params.filterMode, m_Params.wrapMode);
            }

            public ManagedRenderTexture Rent(int width, int height)
            {
                if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "width must be greater than 0");
                if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "width must be greater than 0");

                var desc = new RenderTextureDescriptorWrapper()
                {
                    descriptor = GetDescriptor(width, height)
                };
                return RentInternal(desc);
            }

            public bool Return(ManagedRenderTexture texture)
            {
                if (IsCompatible(texture.Buffer))
                {
                    return ReturnInternal(texture);
                }
                else
                {
                    return false;
                }
            }

            public RenderTextureDescriptor GetDescriptor(int width, int height)
            {
                return new RenderTextureDescriptor(width, height)
                {
                    colorFormat = m_Params.colorFormat,
                    depthBufferBits = m_Params.depthBufferBits,
                    dimension = m_Params.dimension,
                    volumeDepth = m_Params.volumeDepth > 1 ? m_Params.volumeDepth : 1,
                    msaaSamples = m_Params.msaaSamples > 1 ? m_Params.msaaSamples : 1,
                    useMipMap = m_Params.useMipMap,
                    autoGenerateMips = m_Params.autoGenerateMips,
                    enableRandomWrite = m_Params.enableRandomWrite,
                    useDynamicScale = m_Params.useDynamicScale,
                    sRGB = m_Params.readWrite != RenderTextureReadWrite.Linear,
                    bindMS = m_Params.bindMS
                };
            }

            public bool IsCompatible(RenderTexture tex)
            {
                return
                    tex != null &&
                    tex.IsCreated() &&
                    0 < tex.width &&
                    0 < tex.height &&
                    tex.filterMode == m_Params.filterMode &&
                    tex.wrapMode == m_Params.wrapMode &&
                    IsCompatible(tex.descriptor);
            }

            private bool IsCompatible(RenderTextureDescriptor other)
            {
                var desc = new RenderTextureDescriptorWrapper()
                {
                    descriptor = GetDescriptor(other.width, other.height)
                };
                return desc.Equals(other);
            }
        }
    }
}