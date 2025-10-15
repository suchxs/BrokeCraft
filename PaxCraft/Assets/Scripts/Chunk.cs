using NUnit.Framework;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Chunk : MonoBehaviour
{
    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;


    private void Start()
    {
        int vertexIndex = 0;
        List<Vector3> vertices = new List<Vector3> ();
        List<int> triangles = new List<int> ();
        List<Vector2> uvs = new List<Vector2> ();


        for (int face = 0; face < 6; face++)
        {
            for (int i = 0; i < 6; i++)
            {
                int triIndex = VoxelData.voxelTris[face][i];
                vertices.Add(VoxelData.voxelVerts[triIndex]);
                triangles.Add(vertexIndex);
                uvs.Add(VoxelData.voxelUvs[triIndex % 4]); // map 0-3 repeatedly
                vertexIndex++;
            }
        }


        Mesh mesh = new Mesh ();
        mesh.vertices = vertices.ToArray ();
        mesh.triangles = triangles.ToArray ();
        mesh.uv = uvs.ToArray ();

        mesh.RecalculateNormals ();
        meshFilter.mesh = mesh;
    }
}
