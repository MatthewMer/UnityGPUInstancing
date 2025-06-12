using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

public static class MyUtils
{
    public static Mesh ExtractSubmesh(Renderer renderer, int submeshIndex)
    {
        if (renderer == null)
        {
            Debug.LogError("Renderer ist null!");
            return null;
        }

        Mesh mesh = null;

        if (renderer is MeshRenderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogError("MeshFilter oder Mesh fehlt!");
                return null;
            }
            mesh = filter.sharedMesh;
        }
        else if (renderer is SkinnedMeshRenderer skinnedRenderer)
        {
            if (skinnedRenderer.sharedMesh == null)
            {
                Debug.LogError("SkinnedMeshRenderer hat kein Mesh!");
                return null;
            }
            mesh = skinnedRenderer.sharedMesh;
        }

        if (mesh == null)
        {
            Debug.LogError("Kein gültiges Mesh gefunden!");
            return null;
        }

        if (submeshIndex < 0 || submeshIndex >= mesh.subMeshCount)
        {
            Debug.LogError($"Ungültiger Submesh-Index {submeshIndex} für {renderer.gameObject.name}");
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

    public static Mesh GetMeshFromRenderer(Renderer renderer)
    {
        if (renderer is MeshRenderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }
        else if (renderer is SkinnedMeshRenderer skinnedRenderer)
        {
            return skinnedRenderer.sharedMesh;
        }
        return null;
    }

    public static Mesh GenerateCombinedMesh(Mesh mesh, List<Matrix4x4> transforms)
    {
        if (mesh == null || transforms == null || transforms.Count == 0)
        {
            Debug.LogWarning("GenerateCombinedMesh: Invalid input data!");
            return null;
        }

        List<CombineInstance> combineInstances = new List<CombineInstance>();

        for (int i = 0; i < transforms.Count; i++)
        {
            combineInstances.Add(new CombineInstance()
            {
                mesh = mesh,
                transform = transforms[i],
                subMeshIndex = 0
            });
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);
        combinedMesh.RecalculateBounds();
        combinedMesh.Optimize();

        return combinedMesh;
    }

    public static Mesh GenerateCombinedMesh(List<Mesh> meshes, List<Matrix4x4> transforms)
    {
        if (meshes == null || meshes.Count == 0 || transforms == null || transforms.Count != meshes.Count)
        {
            Debug.LogWarning("GenerateCombinedMesh: Invalid input data!");
            return null;
        }

        List<CombineInstance> combineInstances = new List<CombineInstance>();

        for (int i = 0; i < meshes.Count; i++)
        {
            combineInstances.Add(new CombineInstance()
            {
                mesh = meshes[i],
                transform = transforms[i],
                subMeshIndex = 0
            });
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);
        combinedMesh.RecalculateBounds();
        combinedMesh.Optimize();

        return combinedMesh;
    }

    public static Mesh GenerateCombinedMesh(List<Mesh> meshes, Matrix4x4 transform)
    {
        if (meshes == null || meshes.Count == 0 || transform == null)
        {
            Debug.LogWarning("GenerateCombinedMesh: Invalid input data!");
            return null;
        }

        List<CombineInstance> combineInstances = new List<CombineInstance>();

        for (int i = 0; i < meshes.Count; i++)
        {
            combineInstances.Add(new CombineInstance()
            {
                mesh = meshes[i],
                transform = transform,
                subMeshIndex = 0
            });
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);
        combinedMesh.RecalculateBounds();
        combinedMesh.Optimize();

        return combinedMesh;
    }

    public static Mesh GenerateCombinedSubmeshes(List<Mesh> meshes)
    {
        if (meshes == null || meshes.Count == 0)
        {
            Debug.LogWarning("GenerateCombinedSubmeshes: No meshes provided!");
            return null;
        }

        Mesh combinedMesh = new Mesh();
        List<CombineInstance> combineInstances = new List<CombineInstance>();

        for (int i = 0; i < meshes.Count; i++)
        {
            if (meshes[i] == null)
            {
                Debug.LogError($"{meshes[i].name} is null, continuing anyway.");
                continue;
            }

            combineInstances.Add(new CombineInstance()
            {
                mesh = meshes[i],
                transform = Matrix4x4.identity,
                subMeshIndex = 0
            });
        }

        combinedMesh.subMeshCount = combineInstances.Count;
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combinedMesh.CombineMeshes(combineInstances.ToArray(), false, false);
        combinedMesh.RecalculateBounds();
        combinedMesh.Optimize();

        return combinedMesh;
    }
}
