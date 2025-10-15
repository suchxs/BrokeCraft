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

        for (int i = 0; i < 6; i++)
        {
            int triangleIndex = VoxelData.voxelTris[0, i];
            vertices.Add (VoxelData.voxelVerts[triangleIndex]);
            triangles.Add (vertexIndex);

            vertexIndex++;

        }


        Mesh mesh = new Mesh ();
        mesh.vertices = vertices.ToArray ();
        mesh.triangles = triangles.ToArray ();

        mesh.RecalculateNormals ();
    }
}
