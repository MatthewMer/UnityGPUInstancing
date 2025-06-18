using System.Collections.Generic;
using UnityEngine;

namespace Custom
{
    public static class MyUtils
    {
        public static Mesh ExtractSubmesh(Renderer renderer, int submeshIndex)
        {
            if (renderer == null)
            {
                Debug.LogError("Renderer is null");
                return null;
            }

            Mesh mesh = null;

            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    Debug.LogError("");
                    return null;
                }
                mesh = filter.sharedMesh;
            }
            else if (renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                if (skinnedRenderer.sharedMesh == null)
                {
                    Debug.LogError($"{renderer.gameObject.name} SkinnedMeshRenderer has no mesh");
                    return null;
                }
                mesh = skinnedRenderer.sharedMesh;
            }

            if (mesh == null)
            {
                Debug.LogError($"{renderer.gameObject.name} no valid mesh found");
                return null;
            }

            if (submeshIndex < 0 || submeshIndex >= mesh.subMeshCount)
            {
                Debug.LogError($"{renderer.gameObject.name} invalid submesh index {submeshIndex}");
                return null;
            }

            int[] submeshTriangles = mesh.GetTriangles(submeshIndex);
            HashSet<int> usedVertexIndices = new HashSet<int>(submeshTriangles);

            Vector3[] allVertices = mesh.vertices;
            Vector3[] allNormals = mesh.normals;
            Vector2[] allUVs = mesh.uv;

            List<Vector3> newVertices = new List<Vector3>();
            List<Vector3> newNormals = new List<Vector3>();
            List<Vector2> newUVs = new List<Vector2>();
            Dictionary<int, int> vertexMap = new Dictionary<int, int>();

            int newIndex = 0;
            foreach (int oldIndex in usedVertexIndices)
            {
                vertexMap[oldIndex] = newIndex++;
                newVertices.Add(allVertices[oldIndex]);
                if (allNormals.Length > 0) newNormals.Add(allNormals[oldIndex]);
                if (allUVs.Length > 0) newUVs.Add(allUVs[oldIndex]);
            }

            List<int> newTriangles = new List<int>();
            foreach (int oldIndex in submeshTriangles)
            {
                newTriangles.Add(vertexMap[oldIndex]);
            }

            Mesh submesh = new Mesh
            {
                vertices = newVertices.ToArray(),
                normals = newNormals.Count > 0 ? newNormals.ToArray() : null,
                uv = newUVs.Count > 0 ? newUVs.ToArray() : null
            };

            submesh.SetTriangles(newTriangles, 0);
            submesh.RecalculateBounds();

            return submesh;
        }
    }
}