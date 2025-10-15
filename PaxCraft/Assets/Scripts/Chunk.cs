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

    bool[,,] voxelMap = new bool[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];

    private void Start()
    {
        PopulateVoxelMap();
        CreateMeshData();
        CreateMesh();

    }

    void PopulateVoxelMap()
    {
        for (int y = 0; y < VoxelData.ChunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkHeight; z++)
                {
                    voxelMap[x, y, z] = true;
                }
            }
        }
    }

    void CreateMeshData()
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
    }

    bool CheckVoxel(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        if (x < 0 || x >= VoxelData.ChunkWidth ||
            y < 0 || y >= VoxelData.ChunkHeight ||
            z < 0 || z >= VoxelData.ChunkWidth)
        {
            return false;
        }
        else
        {
            return voxelMap[x, y, z];
        }
    }

    //void AddVoxelDataToChunk (Vector3 pos)
    //{
    //    for (int face = 0; face < 6; face++)
    //    {
    //        if (!CheckVoxel(pos + VoxelData.faceChecks[face])) // Draw Outside Voxels
    //        {
    //            //vertices.Add(pos + VoxelData.voxelVerts [VoxelData.voxelTris[face, 0]]);
    //            //vertices.Add(pos + VoxelData.voxelVerts [VoxelData.voxelTris[face, 1]]);
    //            //vertices.Add(pos + VoxelData.voxelVerts [VoxelData.voxelTris[face, 2]]);
    //            //vertices.Add(pos + VoxelData.voxelVerts [VoxelData.voxelTris[face, 3]]);

    //            //uvs.Add (VoxelData.voxelUvs [0]);
    //            //uvs.Add (VoxelData.voxelUvs [1]);
    //            //uvs.Add (VoxelData.voxelUvs [2]);
    //            //uvs.Add (VoxelData.voxelUvs [3]);
    //            //triangles.Add (vertexIndex);
    //            //triangles.Add (vertexIndex);
    //            //triangles.Add (vertexIndex);
    //            //triangles.Add (vertexIndex);
    //            //triangles.Add (vertexIndex);
    //            //triangles.Add (vertexIndex);
    //            //vertexIndex +=4;
    //        }
    //    }
    //}

    void AddVoxelDataToChunk(Vector3 pos)
    {
        for (int face = 0; face < 6; face++)
        {
            // Only draw this face if the adjacent voxel is *not* solid
            if (!CheckVoxel(pos + VoxelData.faceChecks[face]))
            {
                // Add the 4 vertices that make up this face
                for (int i = 0; i < 4; i++)
                {
                    vertices.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[face][i]]);
                    uvs.Add(VoxelData.voxelUvs[i]);
                }

                // Now add two triangles (indices) for this quad
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 3);

                vertexIndex += 4;
            }
        }
    }

    void CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();

        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
    }
}
