using System.Collections.Generic;
using UnityEngine;
using System;
using UnityMeshSimplifier;

namespace Custom
{
    namespace ObjectAtlas
    {
        public interface IInstanceKey<Tderived> : IEquatable<Tderived>
        {
            uint First { get; }
            uint Second { get; }
        }

        public interface ICustomObjectAtlas<Ttype, TinstanceKey> where TinstanceKey : IInstanceKey<TinstanceKey>
        {
            public ObjectData GetObjectData(Ttype objectType, TinstanceKey key);
            public int GetInstanceCount(Ttype objectType, uint first);
            public IEnumerable<Ttype> GetInstanceTypes();
        }

        public class CustomObjectAtlas<Ttype, TinstanceKey> : ICustomObjectAtlas<Ttype, TinstanceKey> where TinstanceKey : IInstanceKey<TinstanceKey>
        {
            protected readonly Dictionary<Ttype, ObjectData[][]> m_Atlas = new();

            public ObjectData GetObjectData(Ttype objectType, TinstanceKey key) => m_Atlas[objectType][key.First][key.Second];
            public int GetInstanceCount(Ttype objectType, uint first) => m_Atlas[objectType][first].Length;
            public IEnumerable<Ttype> GetInstanceTypes() => m_Atlas.Keys;
        }

        public class ObjectData
        {
            private UnpackedPrefab m_UnpackedPrefab;
            public UnpackedPrefab UnpackedPrefab => m_UnpackedPrefab;

            public ObjectData(UnpackedPrefab prefabData)
            {
                m_UnpackedPrefab = prefabData;
            }

            public Renderer GetLodRenderer(ref uint lod) => m_UnpackedPrefab.GetLodRenderer(ref lod);
        }

        public class CollisionData
        {
            private Mesh m_mesh;
            public Mesh Mesh => m_mesh;

            public CollisionData(Renderer renderer, bool tryExtraction)
            {
                if (renderer == null)
                {
                    Debug.LogError("Renderer is null, can't create collision mesh");
                    return;
                }

                var gameObject = renderer.gameObject;

                if (tryExtraction)
                {
                    MeshCollider meshCollider = gameObject.GetComponent<MeshCollider>();
                    if (meshCollider != null && meshCollider.sharedMesh != null)
                    {
                        SetMesh(meshCollider.sharedMesh, false);
                        return;
                    }

                    CapsuleCollider capsuleCollider = gameObject.GetComponent<CapsuleCollider>();
                    if (capsuleCollider != null)
                    {
                        SetMesh(GenerateCapsuleMesh(capsuleCollider), false);
                        return;
                    }

                    BoxCollider boxCollider = gameObject.GetComponent<BoxCollider>();
                    if (boxCollider != null)
                    {
                        SetMesh(GenerateBoxMesh(boxCollider), false);
                        return;
                    }
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null && filter.sharedMesh.subMeshCount > 0)
                {
                    var desc = filter.sharedMesh.GetSubMesh(0);
                    var colliderMesh = MyUtils.ExtractSubmesh(renderer, 0);
                    SetMesh(colliderMesh, true);
                    return;
                }

                Debug.LogWarning($"Could not extract mesh collider from {gameObject.name}");
            }

            private void SetMesh(Mesh colliderMesh, bool simplify)
            {
                const float quality = 0.2f;

                if (simplify)
                {
                    MeshSimplifier simplifier = new MeshSimplifier();
                    simplifier.Initialize(colliderMesh);
                    simplifier.SimplifyMesh(quality);
                    m_mesh = simplifier.ToMesh();
                }
                else
                {
                    m_mesh = colliderMesh;
                }

                m_mesh.Optimize();
            }

            private Mesh GenerateCapsuleMesh(CapsuleCollider capsule)
            {
                Mesh mesh = new Mesh();

                const int segments = 12;
                const int heightSegments = 2;

                List<Vector3> vertices = new List<Vector3>();
                List<int> triangles = new List<int>();

                float radius = capsule.radius;
                float height = capsule.height - (2 * radius);
                int axis = (int)capsule.direction;

                for (int i = 0; i <= heightSegments; i++)
                {
                    float y = (i / (float)heightSegments - 0.5f) * height;
                    for (int j = 0; j < segments; j++)
                    {
                        float angle = (j / (float)segments) * Mathf.PI * 2;
                        float x = Mathf.Cos(angle) * radius;
                        float z = Mathf.Sin(angle) * radius;
                        Vector3 v = new Vector3(x, y, z);

                        if (axis == 0) v = new Vector3(y, x, z);        // x
                        else if (axis == 2) v = new Vector3(x, z, y);   // z

                        vertices.Add(v);
                    }
                }

                for (int i = 0; i < heightSegments; i++)
                {
                    for (int j = 0; j < segments; j++)
                    {
                        int nextJ = (j + 1) % segments;
                        int current = i * segments + j;
                        int next = i * segments + nextJ;
                        int above = (i + 1) * segments + j;
                        int aboveNext = (i + 1) * segments + nextJ;

                        triangles.Add(current);
                        triangles.Add(above);
                        triangles.Add(next);

                        triangles.Add(next);
                        triangles.Add(above);
                        triangles.Add(aboveNext);
                    }
                }

                mesh.vertices = vertices.ToArray();
                mesh.triangles = triangles.ToArray();
                mesh.RecalculateNormals();

                return mesh;
            }

            private Mesh GenerateBoxMesh(BoxCollider box)
            {
                Mesh mesh = new Mesh();

                Vector3 center = box.center;
                Vector3 size = box.size * 0.5f;

                Vector3[] vertices = new Vector3[]
                {
            new Vector3(-size.x, -size.y, -size.z) + center,
            new Vector3(size.x, -size.y, -size.z) + center,
            new Vector3(size.x, -size.y, size.z) + center,
            new Vector3(-size.x, -size.y, size.z) + center,
            new Vector3(-size.x, size.y, -size.z) + center,
            new Vector3(size.x, size.y, -size.z) + center,
            new Vector3(size.x, size.y, size.z) + center,
            new Vector3(-size.x, size.y, size.z) + center
                };

                int[] triangles = new int[]
                {
            0, 2, 1, 0, 3, 2,
            1, 6, 5, 1, 2, 6,
            5, 7, 4, 5, 6, 7,
            4, 3, 0, 4, 7, 3,
            3, 6, 2, 3, 7, 6,
            4, 1, 5, 4, 0, 1
                };

                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();

                return mesh;
            }
        }

        public class BoundingBox
        {
            private Vector3 m_min;
            private Vector3 m_max;

            public Vector3 Min => m_min;
            public Vector3 Max => m_max;

            public BoundingBox(Renderer renderer)
            {
                if (renderer == null)
                {
                    Debug.LogError($"Renderer {renderer.name} is null. Can't create AABB bounding box");
                    return;
                }

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null)
                {
                    Debug.LogError($"MeshFilter {filter.name} is null. Can't create AABB bounding box");
                    return;
                }

                m_min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                m_max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                for (int i = 0; i < mesh.subMeshCount; ++i)
                {
                    var subMesh = MyUtils.ExtractSubmesh(renderer, i);
                    var vertices = subMesh.vertices;

                    foreach (var vertex in vertices)
                    {
                        if (vertex.x < m_min.x) m_min.x = vertex.x;
                        if (vertex.y < m_min.y) m_min.y = vertex.y;
                        if (vertex.z < m_min.z) m_min.z = vertex.z;

                        if (vertex.x > m_max.x) m_max.x = vertex.x;
                        if (vertex.y > m_max.y) m_max.y = vertex.y;
                        if (vertex.z > m_max.z) m_max.z = vertex.z;
                    }
                }
            }
        }

        public class UnpackedPrefab
        {
            private List<Renderer> m_lodRenderers = new List<Renderer>();
            public List<Renderer> LodRenderers => m_lodRenderers;
            private CollisionData m_CollisionData;
            public CollisionData CollisionData => m_CollisionData;
            private bool m_HasCollider = false;
            public bool HasCollider => m_HasCollider;
            private float m_Steepness = 1.0f;
            public float Steepness => m_Steepness;
            private BoundingBox m_BoundingBox;
            public BoundingBox BoundingBox => m_BoundingBox;

            public UnpackedPrefab(GameObject prefab, bool hasCollider, float steepness)
            {
                m_HasCollider = hasCollider;
                m_Steepness = steepness;

                UnpackLods(prefab);
            }

            private void UnpackLods(GameObject prefab)
            {
                LODGroup lodGroup = prefab.GetComponent<LODGroup>();

                if (lodGroup == null)
                {
                    Renderer renderer = prefab.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        m_lodRenderers.Add(renderer);
                    }
                    else
                    {
                        Debug.LogError($"Renderer missing: {prefab.name}");
                    }
                }
                else
                {
                    LOD[] lods = lodGroup.GetLODs();
                    foreach (var lod in lods)
                    {
                        if (lod.renderers.Length > 0)
                        {
                            m_lodRenderers.Add(lod.renderers[0]);
                        }
                        else
                        {
                            Debug.LogError($"LOD renderer mismatch: {prefab.name}");
                        }
                    }
                }

                if (m_lodRenderers.Count == 0)
                {
                    Debug.LogError($"No valid LOD renderers found for {prefab.name}!");
                    UnityEngine.Object.Destroy(prefab);
                    return;
                }

                GenerateCollisionData();
                GenerateAABBLocal();
            }

            public Renderer GetLodRenderer(ref uint lod)
            {
                var clamped = Mathf.Clamp((int)lod, 0, m_lodRenderers.Count - 1);
                lod = (uint)clamped;
                return m_lodRenderers[clamped];
            }

            private void GenerateCollisionData()
            {
                if (m_lodRenderers.Count == 0 || m_lodRenderers[0] == null)
                {
                    Debug.LogError("LOD 0 is empty or renderer is null");
                    return;
                }
                m_CollisionData = new CollisionData(m_lodRenderers[0], true);
            }

            private void GenerateAABBLocal()
            {
                if (m_lodRenderers[0] == null)
                {
                    Debug.LogError("Lod renderer 0 is null. Can't create bounding box");
                    return;
                }

                m_BoundingBox = new BoundingBox(m_lodRenderers[0]);
            }
        }
    }
}