using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Highlights the block under the crosshair within Minecraft-like reach distance.
/// Uses a lightweight line outline to avoid aggressive glow.
/// </summary>
[DisallowMultipleComponent]
public class BlockHighlighter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private World world;
    [SerializeField] private Transform cameraTransform;

    [Header("Highlight Settings")]
    [SerializeField] private float reachDistance = VoxelData.PlayerBlockReach;
    [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private float outlinePadding = 0.004f;
    [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;

    private Material lineMaterial;
    private bool hasTarget;
    private int3 targetBlock;
    
    public bool TryGetHighlightedBlock(out int3 blockPos)
    {
        blockPos = targetBlock;
        return hasTarget;
    }
    private void Awake()
    {
        if (world == null)
        {
            world = FindFirstObjectByType<World>();
        }

        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cameraTransform = cam.transform;
            }
            else if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

    }

    private void Update()
    {
        hasTarget = TryFindTarget(out targetBlock);
    }

    private void OnRenderObject()
    {
        if (!hasTarget || cameraTransform == null)
        {
            return;
        }

        EnsureLineMaterial();
        lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);
        GL.Color(outlineColor);
        DrawCubeEdges(GetBounds(targetBlock));
        GL.End();
        GL.PopMatrix();
    }

    private void OnDestroy()
    {
        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
        }

    }

    private bool TryFindTarget(out int3 blockPos)
    {
        blockPos = default;
        if (cameraTransform == null || world == null)
        {
            return false;
        }

        Vector3 origin = cameraTransform.position;
        Vector3 direction = cameraTransform.forward;
        Vector3 up = cameraTransform.up;

        const float forwardOffset = 0.1f;
        origin += direction * forwardOffset;

        bool found = false;
        float bestDistance = float.MaxValue;
        RaycastHit bestHit = default;

        // Central and offset rays using RaycastAll to skip self hits and catch near surfaces
        const float offset = 0.08f;
        TryRaycast(origin, direction, ref bestHit, ref bestDistance, ref found);
        TryRaycast(origin - up * offset, direction, ref bestHit, ref bestDistance, ref found);
        TryRaycast(origin + up * offset, direction, ref bestHit, ref bestDistance, ref found);

        if (!found)
        {
            return false;
        }

        Vector3 adjusted = bestHit.point - bestHit.normal * 0.01f;
        int x = Mathf.FloorToInt(adjusted.x);
        int y = Mathf.FloorToInt(adjusted.y);
        int z = Mathf.FloorToInt(adjusted.z);
        blockPos = new int3(x, y, z);
        return world.GetBlockAtPosition(blockPos) != BlockType.Air;
    }

    private void TryRaycast(Vector3 origin, Vector3 direction, ref RaycastHit bestHit, ref float bestDistance, ref bool found)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, reachDistance, hitMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.distance <= 0f || hit.distance >= bestDistance)
            {
                continue;
            }

            if (IsSelfHit(hit.collider))
            {
                continue;
            }

            bestHit = hit;
            bestDistance = hit.distance;
            found = true;
            break;
        }
    }

    private bool IsSelfHit(Collider collider)
    {
        if (collider == null)
        {
            return true;
        }

        return collider.transform.root == transform.root;
    }

    private Bounds GetBounds(int3 blockPos)
    {
        Vector3 center = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
        Vector3 size = Vector3.one + Vector3.one * (outlinePadding * 2f);
        return new Bounds(center, size);
    }

    private void DrawCubeEdges(Bounds bounds)
    {
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;

        Vector3[] corners = new Vector3[8];
        corners[0] = c + new Vector3(-e.x, -e.y, -e.z);
        corners[1] = c + new Vector3(e.x, -e.y, -e.z);
        corners[2] = c + new Vector3(e.x, e.y, -e.z);
        corners[3] = c + new Vector3(-e.x, e.y, -e.z);
        corners[4] = c + new Vector3(-e.x, -e.y, e.z);
        corners[5] = c + new Vector3(e.x, -e.y, e.z);
        corners[6] = c + new Vector3(e.x, e.y, e.z);
        corners[7] = c + new Vector3(-e.x, e.y, e.z);

        // Bottom square
        Line(corners[0], corners[1]);
        Line(corners[1], corners[2]);
        Line(corners[2], corners[3]);
        Line(corners[3], corners[0]);

        // Top square
        Line(corners[4], corners[5]);
        Line(corners[5], corners[6]);
        Line(corners[6], corners[7]);
        Line(corners[7], corners[4]);

        // Vertical edges
        Line(corners[0], corners[4]);
        Line(corners[1], corners[5]);
        Line(corners[2], corners[6]);
        Line(corners[3], corners[7]);

        // Slightly larger pass to strengthen the outline
        float pad = outlinePadding * 0.5f;
        if (pad > 0f)
        {
            Bounds outer = new Bounds(bounds.center, bounds.size + Vector3.one * pad * 2f);
            Vector3 oc = outer.center;
            Vector3 oe = outer.extents;
            Vector3[] ocorners = new Vector3[8];
            ocorners[0] = oc + new Vector3(-oe.x, -oe.y, -oe.z);
            ocorners[1] = oc + new Vector3(oe.x, -oe.y, -oe.z);
            ocorners[2] = oc + new Vector3(oe.x, oe.y, -oe.z);
            ocorners[3] = oc + new Vector3(-oe.x, oe.y, -oe.z);
            ocorners[4] = oc + new Vector3(-oe.x, -oe.y, oe.z);
            ocorners[5] = oc + new Vector3(oe.x, -oe.y, oe.z);
            ocorners[6] = oc + new Vector3(oe.x, oe.y, oe.z);
            ocorners[7] = oc + new Vector3(-oe.x, oe.y, oe.z);

            Line(ocorners[0], ocorners[1]);
            Line(ocorners[1], ocorners[2]);
            Line(ocorners[2], ocorners[3]);
            Line(ocorners[3], ocorners[0]);

            Line(ocorners[4], ocorners[5]);
            Line(ocorners[5], ocorners[6]);
            Line(ocorners[6], ocorners[7]);
            Line(ocorners[7], ocorners[4]);

            Line(ocorners[0], ocorners[4]);
            Line(ocorners[1], ocorners[5]);
            Line(ocorners[2], ocorners[6]);
            Line(ocorners[3], ocorners[7]);
        }
    }

    private static void Line(Vector3 a, Vector3 b)
    {
        GL.Vertex(a);
        GL.Vertex(b);
    }

    private void EnsureLineMaterial()
    {
        if (lineMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

}
