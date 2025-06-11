using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Rendering;
using System.Linq;
using Custom.GraphicsBuffer;
using Custom.GraphicsBufferPool;
using Custom.ObjectAtlas;

namespace Custom
{
    namespace Rendering { 

        public interface IInstanceProvider<TexternKey, Ttype, TinstanceKey>
            where TinstanceKey : IInstanceKey<TinstanceKey>
        {
            public TexternKey Key { get; }
            public Ttype Type { get; }
            public uint LOD { get; }
            public bool Draw { get; }

            public IReadOnlyList<Matrix4x4> Transforms(TinstanceKey key);
            public IEnumerable<TinstanceKey> InstanceKeys();
            public IEnumerable<(TinstanceKey, IReadOnlyList<Matrix4x4>)> KvTransforms();

            public event Action<IInstanceProvider<TexternKey, Ttype, TinstanceKey>, uint, uint> OnLODChanged;
            public event Action<IInstanceProvider<TexternKey, Ttype, TinstanceKey>, bool> OnDrawChanged;
            public event Action<IInstanceProvider<TexternKey, Ttype, TinstanceKey>, IEnumerable<TinstanceKey>> OnTransformsChanged;
        }

        public abstract class Instance : IDisposable
        {
            private uint m_UniqueId;
            public uint UniqueId => m_UniqueId;
            private Matrix4x4 m_Transform;
            public Matrix4x4 Transform => m_Transform;

            public Instance(uint uniqueId, Matrix4x4 transform)
            {
                m_UniqueId = uniqueId;
                m_Transform = transform;
            }

            ~Instance()
            {
                Dispose();
            }

            public abstract void Dispose();
        }

        public abstract class InstanceProvider<TexternKey, Ttype, TinstanceKey, Tinstance> : IInstanceProvider<TexternKey, Ttype, TinstanceKey>, IDisposable
            where TinstanceKey : IInstanceKey<TinstanceKey>
            where Tinstance : Instance
        {
            private readonly Dictionary<TinstanceKey, Dictionary<uint, Tinstance>> m_InstanceCollection = new();
            public IReadOnlyDictionary<TinstanceKey, Dictionary<uint, Tinstance>> InstanceCollection => m_InstanceCollection;

            uint m_NextUniqueId = 0;

            private readonly Ttype m_Type;
            public Ttype Type => m_Type;

            private readonly TexternKey m_Key;
            public TexternKey Key => m_Key;

            private readonly object m_Lock = new object();

            private readonly Queue<Func<TinstanceKey>> m_InstanceChangeQueue = new();

            public InstanceProvider(TexternKey key, Ttype type)
            {
                m_Key = key;
                m_Type = type;
            }

            ~InstanceProvider()
            {
                Dispose();
            }

            private uint m_LOD = 0;
            private uint m_LODNew = 0;
            public uint LOD
            {
                get
                {
                    lock (m_Lock) return m_LOD;
                }
                set
                {
                    lock (m_Lock) m_LODNew = value;
                }
            }

            private bool m_Draw = false;
            private bool m_DrawNew = false;
            public bool Draw
            {
                get
                {
                    lock (m_Lock) return m_Draw;
                }
                set
                {
                    lock (m_Lock) m_DrawNew = value;
                }
            }

            public event Action<IInstanceProvider<TexternKey, Ttype, TinstanceKey>, uint, uint> OnLODChanged;
            private void RaiseLODChanged(uint oldLOD, uint newLOD) => OnLODChanged?.Invoke(this, oldLOD, newLOD);

            public event Action<IInstanceProvider<TexternKey, Ttype, TinstanceKey>, bool> OnDrawChanged;
            private void RaiseDrawChanged(bool draw) => OnDrawChanged?.Invoke(this, draw);

            public event Action<IInstanceProvider<TexternKey, Ttype, TinstanceKey>, IEnumerable<TinstanceKey>> OnTransformsChanged;
            private void RaiseTransformsChanged(IEnumerable<TinstanceKey> instanceKeys) => OnTransformsChanged?.Invoke(this, instanceKeys);

            protected void Add(TinstanceKey key, Tinstance instance)
            {
                lock (m_Lock)
                {
                    m_InstanceChangeQueue.Enqueue(() =>
                    {
                        if (!m_InstanceCollection.TryGetValue(key, out var dict))
                        {
                            dict = new Dictionary<uint, Tinstance>();
                        }
                        dict[instance.UniqueId] = instance;
                        m_InstanceCollection[key] = dict;

                        return key;
                    });
                }
            }

            public void Apply()
            {
                lock (m_Lock)
                {
                    if (m_LODNew != m_LOD)
                    {
                        var oldLod = m_LOD;
                        m_LOD = m_LODNew;
                        RaiseLODChanged(oldLod, m_LOD);
                    }
                    if (m_Draw != m_DrawNew)
                    {
                        m_Draw = m_DrawNew;
                        RaiseDrawChanged(m_Draw);
                    }
                    if (m_InstanceChangeQueue.Count > 0)
                    {
                        HashSet<TinstanceKey> keys = new();

                        while (m_InstanceChangeQueue.TryDequeue(out var action))
                        {
                            keys.Add(action.Invoke());
                        }

                        RaiseTransformsChanged(keys.ToList());
                    }
                }
            }

            protected uint GetUniqueId()
            {
                lock (m_Lock)
                {
                    return m_NextUniqueId++;
                }
            }

            public IReadOnlyList<Matrix4x4> Transforms(TinstanceKey instanceKey)
            {
                lock (m_Lock) return m_InstanceCollection[instanceKey].Select(i => i.Value.Transform).ToList();
            }

            public IEnumerable<TinstanceKey> InstanceKeys()
            {
                lock (m_Lock) return m_InstanceCollection.Keys.ToList();
            }

            public IEnumerable<(TinstanceKey, IReadOnlyList<Matrix4x4>)> KvTransforms()
            {
                lock (m_Lock)
                    return m_InstanceCollection
                        .Select(kv => (kv.Key, (IReadOnlyList<Matrix4x4>)kv.Value.Select(i => i.Value.Transform).ToList()))
                        .ToList();
            }

            public abstract void Dispose();
        }

        public class InstanceGroup<Tkey>
        {
            private readonly HashSet<Tkey> m_Keys = new();
            public IReadOnlyCollection<Tkey> Keys => m_Keys;

            public bool Add(Tkey key)
            {
                return m_Keys.Add(key);
            }

            public bool Remove(Tkey key)
            {
                return m_Keys.Remove(key);
            }
        }

        public class ShaderPropertySet
        {
            private readonly Dictionary<uint, Dictionary<int, object>> m_Blocks = new();

            public void Set<T>(uint first, string propertyName, T value)
            {
                int propertyId = Shader.PropertyToID(propertyName);
                Set(first, propertyId, value);
            }

            public void Set<T>(uint first, int propertyId, T value)
            {
                if (!m_Blocks.TryGetValue(first, out var properties))
                {
                    properties = new Dictionary<int, object>();
                    m_Blocks[first] = properties;
                }

                properties[propertyId] = value;
            }

            public IEnumerable<(int propertyId, object value)> this[uint first] =>
                m_Blocks.TryGetValue(first, out var props)
                ? props.Select(x => (x.Key, x.Value))
                : Enumerable.Empty<(int propertyId, object value)>();

            public MaterialPropertyBlock AsPropertyBlock(uint first)
            {
                var block = new MaterialPropertyBlock();

                if (m_Blocks.TryGetValue(first, out var properties))
                {
                    foreach (var (propertyId, value) in properties)
                    {
                        SetValue(block, propertyId, value);
                    }
                }

                return block;
            }

            public static void SetValue(MaterialPropertyBlock block, int propId, object value)
            {
                switch (value)
                {
                    case float f: block.SetFloat(propId, f); break;
                    case int i: block.SetInt(propId, i); break;
                    case Vector4 v4: block.SetVector(propId, v4); break;
                    case Color c: block.SetColor(propId, c); break;
                    case Texture t: block.SetTexture(propId, t); break;
                    default:
                        Debug.LogWarning($"Unsupported property type: {value.GetType()}");
                        break;
                }
            }
        }

        public class CustomRenderer<Tkey, Ttype, TinstanceKey> : IDisposable where TinstanceKey : IInstanceKey<TinstanceKey>
        {
            private readonly CommandQueueAutoFlush m_CommandQueue;

            private readonly Dictionary<Tkey, IInstanceProvider<Tkey, Ttype, TinstanceKey>> m_InstanceProviders = new();
            private readonly Dictionary<TinstanceKey, Dictionary<uint, CustomRenderData<Tkey>>> m_RenderDataDict = new();

            private readonly HashSet<(TinstanceKey, uint)> m_DirtyFlags = new();
            private List<CustomRenderData<Tkey>> m_RenderData = new();

            private Ttype m_Type;
            private ICustomObjectAtlas<Ttype, TinstanceKey> m_Atlas;

            private readonly ShaderPropertySet m_ShaderPropertySet;
            private ComputeShader m_CustomCulling;

            private const int k_ArgsStride = sizeof(uint) * 5;
            private const ComputeBufferType k_ArgsBufferType = ComputeBufferType.IndirectArguments;

            private const int k_InstanceStride = sizeof(float) * 16; // Matrix4x4 = 16 floats
            private const ComputeBufferType k_InstanceBufferType = ComputeBufferType.Structured;
            private const ComputeBufferType k_CullingBufferType = ComputeBufferType.Append;

            private readonly ComputeBufferPool m_ArgsBufferPool;
            private readonly ComputeBufferPool m_InstanceBufferPool;
            private readonly ComputeBufferPool m_CullingBufferPool;

            public CustomRenderer(Ttype type, ICustomObjectAtlas<Ttype, TinstanceKey> objectAtlas, int baseSize, BufferMode mode, int batchSize, ShaderPropertySet propertySet = null)
            {
                m_Type = type;
                m_Atlas = objectAtlas;
                m_ShaderPropertySet = propertySet;

                m_ArgsBufferPool = new ComputeBufferPool(new ComputeBufferPoolParams()
                {
                    baseSize = 1,
                    stride = k_ArgsStride,
                    type = k_ArgsBufferType
                });
                m_InstanceBufferPool = new ComputeBufferPool(new ComputeBufferPoolParams()
                {
                    baseSize = baseSize,
                    stride = k_InstanceStride,
                    type = k_InstanceBufferType,
                    mode = mode,
                    batchSize = batchSize
                });
                m_CullingBufferPool = new ComputeBufferPool(new ComputeBufferPoolParams()
                {
                    baseSize = baseSize,
                    stride = k_InstanceStride,
                    type = k_CullingBufferType,
                    mode = mode,
                    batchSize = batchSize
                });

                m_CommandQueue = new CommandQueueAutoFlush(CommandQueue.FlushMode.Background, UpdateRenderData);
                m_CustomCulling = Resources.Load<ComputeShader>("Custom/CustomCulling");
            }

            public void Dispose()
            {
                foreach (var (instanceKey, lodRenderData) in m_RenderDataDict)
                {
                    foreach (var (lod, renderData) in lodRenderData)
                    {
                        renderData.Dispose();
                    }
                }

                m_ArgsBufferPool.Dispose();
                m_InstanceBufferPool.Dispose();
                m_CullingBufferPool.Dispose();
            }

            public void RegisterInstanceProvider(Tkey key, IInstanceProvider<Tkey, Ttype, TinstanceKey> instanceProvider)
            {
                m_CommandQueue.Enqueue(() =>
                {
                    try
                    {
                        if (!m_InstanceProviders.ContainsKey(key))
                        {
                            instanceProvider.OnLODChanged += HandleLodChanged;
                            instanceProvider.OnDrawChanged += HandleDrawChanged;
                            instanceProvider.OnTransformsChanged += HandleTransformsChanged;
                            m_InstanceProviders[key] = instanceProvider;

                            var instanceKeys = instanceProvider.InstanceKeys();
                            if (instanceKeys.Count() > 0)
                                UpdateTransforms(instanceProvider, instanceKeys);
                        }
                        else
                        {
                            Debug.LogError($"InstanceProvider {key} already registered");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Register InstanceProvider {key}: {ex.ToString()}");
                    }
                });
            }

            public void UnregisterInstanceProvider(Tkey key)
            {
                m_CommandQueue.Enqueue(() =>
                {
                    try
                    {
                        if (m_InstanceProviders.Remove(key, out var instanceProvider))
                        {
                            instanceProvider.OnLODChanged -= HandleLodChanged;
                            instanceProvider.OnDrawChanged -= HandleDrawChanged;
                            instanceProvider.OnTransformsChanged -= HandleTransformsChanged;

                            var instanceKeys = instanceProvider.InstanceKeys();
                            if (instanceKeys.Count() > 0)
                                UpdateTransforms(instanceProvider, instanceKeys);
                        }
                        else
                        {
                            Debug.LogError($"InstanceProvider {key} is not registered");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Unregister InstanceProvider {key}: {ex.ToString()}");
                    }
                });
            }

            private void HandleLodChanged(IInstanceProvider<Tkey, Ttype, TinstanceKey> instanceProvider, uint oldLOD, uint newLOD)
                => UpdateLod(instanceProvider, oldLOD, newLOD);

            private void HandleDrawChanged(IInstanceProvider<Tkey, Ttype, TinstanceKey> instanceProvider, bool draw)
                => UpdateDraw(instanceProvider, draw);

            private void HandleTransformsChanged(IInstanceProvider<Tkey, Ttype, TinstanceKey> instanceProvider, IEnumerable<TinstanceKey> instanceKeys)
                => UpdateTransforms(instanceProvider, instanceKeys);

            public void UpdateLod(IInstanceProvider<Tkey, Ttype, TinstanceKey> instanceProvider, uint oldLOD, uint newLOD)
            {
                m_CommandQueue.Enqueue(() =>
                {
                    try
                    {
                        var providerKey = instanceProvider.Key;

                        foreach (var instanceKey in instanceProvider.InstanceKeys())
                        {
                            Remove(providerKey, instanceKey, oldLOD);
                            Add(providerKey, instanceKey, newLOD);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"CustomRenderer LOD Update: {oldLOD} -> {newLOD}: {ex.ToString()}");
                    }
                });
            }

            public void UpdateDraw(IInstanceProvider<Tkey, Ttype, TinstanceKey> instanceProvider, bool draw)
            {
                m_CommandQueue.Enqueue(() =>
                {
                    try
                    {
                        var key = instanceProvider.Key;
                        var lod = instanceProvider.LOD;

                        var instanceKeys = instanceProvider.InstanceKeys();

                        if (draw)
                        {
                            AddAll(key, instanceKeys, lod);
                        }
                        else
                        {
                            RemoveAll(key, instanceKeys, lod);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"CustomRenderer Draw Update: {draw}: {ex.ToString()}");
                    }
                });
            }

            public void UpdateTransforms(IInstanceProvider<Tkey, Ttype, TinstanceKey> instanceProvider, IEnumerable<TinstanceKey> instanceKeys)
            {
                m_CommandQueue.Enqueue(() =>
                {
                    try
                    {
                        var key = instanceProvider.Key;
                        var lod = instanceProvider.LOD;

                        UpdateAll(key, instanceKeys, lod);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"CustomRenderer Transform Update: {instanceKeys.Count()}: {ex.ToString()}");
                    }
                });
            }

            // ***** RenderData Dictionary access helpers *****
            private void Add(Tkey key, TinstanceKey instanceKey, uint lod)
            {
                if (!m_RenderDataDict.TryGetValue(instanceKey, out var lodRenderData))
                {
                    lodRenderData = new Dictionary<uint, CustomRenderData<Tkey>>();
                    m_RenderDataDict[instanceKey] = lodRenderData;
                }

                var objectData = m_Atlas.GetObjectData(m_Type, instanceKey);
                var renderer = objectData.GetLodRenderer(ref lod);

                if (!lodRenderData.TryGetValue(lod, out var renderData))
                {
                    var boundingBox = objectData.UnpackedPrefab.BoundingBox;

                    renderData = new CustomRenderData<Tkey>();
                    renderData.Initialize(renderer, boundingBox, m_ArgsBufferPool, m_InstanceBufferPool, m_CullingBufferPool, m_ShaderPropertySet[instanceKey.First]);
                    lodRenderData[lod] = renderData;
                }

                if (renderData.InstanceGroup.Add(key))
                {
                    m_DirtyFlags.Add((instanceKey, lod));
                }
            }

            private void AddAll(Tkey key, IEnumerable<TinstanceKey> instanceKeys, uint lod)
            {
                foreach (var instanceKey in instanceKeys)
                    Add(key, instanceKey, lod);
            }

            private void Remove(Tkey key, TinstanceKey instanceKey, uint lod)
            {
                if (m_RenderDataDict.TryGetValue(instanceKey, out var lodRenderData))
                {
                    if (lodRenderData.TryGetValue(lod, out var renderData))
                    {
                        if (renderData.InstanceGroup.Remove(key))
                        {
                            m_DirtyFlags.Add((instanceKey, lod));
                        }

                        if (renderData.InstanceGroup.Keys.Count() == 0)
                        {
                            lodRenderData.Remove(lod);
                            UnityMainThreadDispatcher.EnqueueLateUpdate(renderData.Dispose);

                            if (lodRenderData.Count() == 0)
                            {
                                m_RenderDataDict.Remove(instanceKey);
                            }
                        }
                    }
                }
            }

            private void RemoveAll(Tkey key, IEnumerable<TinstanceKey> instanceKeys, uint lod)
            {
                foreach (var instanceKey in instanceKeys)
                    Remove(key, instanceKey, lod);
            }

            private void Update(Tkey key, TinstanceKey instanceKey, uint lod)
            {
                Remove(key, instanceKey, lod);
                Add(key, instanceKey, lod);
            }

            private void UpdateAll(Tkey key, IEnumerable<TinstanceKey> instanceKeys, uint lod)
            {
                foreach (var instanceKey in instanceKeys)
                {
                    Update(key, instanceKey, lod);
                }
            }

            private void UpdateRenderData()
            {
                List<CustomRenderData<Tkey>> newRenderData = new();

                foreach (var (instanceKey, lod) in m_DirtyFlags)
                {
                    if (m_RenderDataDict.TryGetValue(instanceKey, out var lodRenderData))
                    {
                        if (lodRenderData.TryGetValue(lod, out var renderData))
                        {
                            List<Matrix4x4> transforms = new List<Matrix4x4>();

                            foreach (var providerKey in renderData.InstanceGroup.Keys)
                            {
                                if (m_InstanceProviders.TryGetValue(providerKey, out var instanceProvider))
                                {
                                    transforms.AddRange(instanceProvider.Transforms(instanceKey));
                                }
                            }

                            renderData.SetTransforms(transforms);

                            newRenderData.Add(renderData);
                        }
                    }
                }

                UnityMainThreadDispatcher.EnqueueLateUpdate(() =>
                {
                    m_RenderData = newRenderData;
                });
            }

            public void Render()
            {
                foreach (var renderData in m_RenderData)
                    renderData.Render(m_CustomCulling);
            }

            private class CustomRenderData<TkeyInternal> : IDisposable
            {
                private readonly InstanceGroup<TkeyInternal> m_InstanceGroup = new();
                public InstanceGroup<TkeyInternal> InstanceGroup => m_InstanceGroup;
                private int m_Count;
                public int Count => m_Count;
                private int m_NumGroups;

                private BoundingBox m_InstanceBounds;
                public BoundingBox InstanceBounds => m_InstanceBounds;
                private Mesh m_SharedMesh;
                private Material[] m_Materials;

                private MaterialPropertyBlock m_MaterialPropertyBlock;
                private ManagedComputeBuffer[] m_ArgsBuffers;
                private ManagedComputeBuffer m_InstanceBuffer;
                private ManagedComputeBuffer m_CullingBuffer;

                private ComputeBufferPool m_ArgsBufferPool;
                private ComputeBufferPool m_InstanceBufferPool;
                private ComputeBufferPool m_CullingBufferPool;

                private Bounds m_GlobalBounds;

                private static readonly int k_MinBoundsId = Shader.PropertyToID("_MinBounds");
                private static readonly int k_MaxBoundsId = Shader.PropertyToID("_MaxBounds");

                private static readonly int k_CullingBufferId = Shader.PropertyToID("_CullingBuffer");
                private static readonly int k_InstanceBufferId = Shader.PropertyToID("_InstanceBuffer");

                private static readonly int k_TransformCountId = Shader.PropertyToID("_TransformCount");
                private static readonly int k_GlobalBoundsCentreId = Shader.PropertyToID("_GlobalBoundsCentre");

                public void Dispose()
                {
                    foreach (var args in m_ArgsBuffers)
                    {
                        m_ArgsBufferPool.Return(args);
                    }
                    m_InstanceBufferPool.Return(m_InstanceBuffer);
                    m_CullingBufferPool.Return(m_CullingBuffer);
                }

                public void Initialize(Renderer renderer, BoundingBox instanceBounds, ComputeBufferPool argsPool, ComputeBufferPool instancePool, ComputeBufferPool cullingPool, IEnumerable<(int propertyId, object value)> properties = null)
                {
                    m_InstanceBounds = instanceBounds;

                    m_ArgsBufferPool = argsPool;
                    m_InstanceBufferPool = instancePool;
                    m_CullingBufferPool = cullingPool;

                    UnityMainThreadDispatcher.EnqueueLateUpdate(() =>
                    {
                        m_SharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        m_Materials = renderer.sharedMaterials;

                        m_MaterialPropertyBlock = new MaterialPropertyBlock();

                        foreach (var property in properties)
                            ShaderPropertySet.SetValue(m_MaterialPropertyBlock, property.propertyId, property.value);

                        m_MaterialPropertyBlock.SetVector(k_MinBoundsId, m_InstanceBounds.Min);
                        m_MaterialPropertyBlock.SetVector(k_MaxBoundsId, m_InstanceBounds.Max);

                        m_ArgsBuffers = new ManagedComputeBuffer[m_SharedMesh.subMeshCount];
                        for (int i = 0; i < m_SharedMesh.subMeshCount; i++)
                        {
                            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
                            args[0] = m_SharedMesh.GetIndexCount(i);
                            args[1] = 0;                                // counter set by copycount -> number of instances
                            args[2] = m_SharedMesh.GetIndexStart(i);
                            args[3] = m_SharedMesh.GetBaseVertex(i);
                            args[4] = 0;

                            m_ArgsBuffers[i] = m_ArgsBufferPool.Rent(1);
                            m_ArgsBuffers[i].SetData(args);
                        }
                    });
                }

                public void SetTransforms(List<Matrix4x4> transforms)
                {
                    var newBounds = CalculateBounds(transforms);
                    var numGroups = Mathf.CeilToInt(transforms.Count / 64.0f);

                    int newSize = transforms.Count;

                    UnityMainThreadDispatcher.EnqueueLateUpdate(() =>
                    {
                        if (m_InstanceBuffer == null || m_InstanceBufferPool.NeedsReallocation(m_InstanceBuffer, newSize))
                        {
                            var instanceBuffer = m_InstanceBufferPool.Rent(newSize);
                            var cullingBuffer = m_CullingBufferPool.Rent(newSize);

                            instanceBuffer.SetData(transforms);

                            UnityMainThreadDispatcher.EnqueueUpdate(() =>
                            {
                                m_GlobalBounds = newBounds;
                                m_Count = transforms.Count;
                                m_NumGroups = numGroups;

                                if (m_InstanceBuffer != null)
                                {
                                    m_InstanceBufferPool.Return(m_InstanceBuffer);
                                    m_CullingBufferPool.Return(m_CullingBuffer);
                                }

                                m_InstanceBuffer = instanceBuffer;
                                m_CullingBuffer = cullingBuffer;

                                m_MaterialPropertyBlock.SetBuffer(k_CullingBufferId, m_CullingBuffer.Buffer);
                            });
                        }
                        else
                        {
                            UnityMainThreadDispatcher.EnqueueUpdate(() =>
                            {
                                m_GlobalBounds = newBounds;
                                m_Count = transforms.Count;
                                m_NumGroups = numGroups;

                                m_InstanceBuffer.SetData(transforms);
                            });
                        }
                    });
                }

                public void Render(ComputeShader cullingShader)
                {
                    // set current buffers
                    cullingShader.SetBuffer(0, k_InstanceBufferId, m_InstanceBuffer.Buffer);
                    cullingShader.SetBuffer(0, k_CullingBufferId, m_CullingBuffer.Buffer);
                    cullingShader.SetInt(k_TransformCountId, m_Count);
                    cullingShader.SetVector(k_GlobalBoundsCentreId, m_GlobalBounds.center);

                    // reset counter
                    m_CullingBuffer.SetCounterValue(0);

                    // dispatch culling async
                    using (CommandBuffer commandBuffer = new CommandBuffer { name = "GPU Culling" })
                    {
                        commandBuffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
                        commandBuffer.DispatchCompute(cullingShader, 0, m_NumGroups, 1, 1);
                        Graphics.ExecuteCommandBufferAsync(commandBuffer, ComputeQueueType.Background);
                    }

                    for (int i = 0; i < m_ArgsBuffers.Length; ++i)
                    {
                        ComputeBuffer.CopyCount(m_CullingBuffer.Buffer, m_ArgsBuffers[i].Buffer, sizeof(uint));
                    }

                    DrawInstancedIndirect();
                }

                private void DrawInstancedIndirect()
                {
                    for (int i = 0; i < m_SharedMesh.subMeshCount; ++i)
                    {
                        Graphics.DrawMeshInstancedIndirect(
                            m_SharedMesh,
                            i,
                            m_Materials[i],
                            m_GlobalBounds,
                            m_ArgsBuffers[i].Buffer,
                            0,
                            m_MaterialPropertyBlock);
                    }
                }

                private Bounds CalculateBounds(List<Matrix4x4> transforms)
                {
                    if (transforms.Count == 0)
                        return new Bounds(Vector3.zero, Vector3.one);

                    Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                    foreach (var matrix in transforms)
                    {
                        Vector3 position = matrix.GetColumn(3);

                        min = Vector3.Min(min, position);
                        max = Vector3.Max(max, position);
                    }

                    Vector3 centre = (min + max) * 0.5f;
                    Vector3 size = max - min;
                    size.y = Mathf.Max(size.y, 10f);

                    return new Bounds(centre, size);
                }
            }
        }
    }
}