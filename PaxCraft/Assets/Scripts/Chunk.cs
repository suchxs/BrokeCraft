using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

public class Chunk : MonoBehaviour
{
    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;

    int vertexIndex = 0;
    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Vector2> uvs = new List<Vector2>();

    private void Start()
    {
        for (int y = 0; y < VoxelData.ChunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkHeight; z++)
                {
                    AddVoxelDataToChunk(new Vector3(x, y, z));
                }
            }
        }
        CreateMesh();

    }

    void AddVoxelDataToChunk (Vector3 pos)
    {
        for (int face = 0; face < 6; face++)
        {
            for (int i = 0; i < 6; i++)
            {
                int triIndex = VoxelData.voxelTris[face][i];
                vertices.Add(VoxelData.voxelVerts[triIndex] + pos);
                triangles.Add(vertexIndex);
                uvs.Add(VoxelData.voxelUvs[triIndex % 4]); // map 0-3 repeatedly
                vertexIndex++;
            }
        }
    }

    void CreateMesh ()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();

        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
    }
}
