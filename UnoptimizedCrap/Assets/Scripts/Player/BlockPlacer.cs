using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles creative-style block placement using the current inventory selection.
/// Right click to place on the face of the highlighted block.
/// </summary>
[DisallowMultipleComponent]
public class BlockPlacer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private World world;
    [SerializeField] private BlockHighlighter highlighter;
    [SerializeField] private BlockInventory inventory;
    [SerializeField] private InputActionReference placeAction;

    [Header("Placement")]
    [SerializeField] private LayerMask collisionMask = Physics.DefaultRaycastLayers;
    [SerializeField] private float boundsShrink = 0.05f;

    private void Awake()
    {
        if (world == null)
        {
            world = FindFirstObjectByType<World>();
        }

        if (highlighter == null)
        {
            highlighter = GetComponentInChildren<BlockHighlighter>();
        }

        if (inventory == null)
        {
            inventory = GetComponent<BlockInventory>();
        }
    }

    private void OnEnable()
    {
        placeAction?.action?.Enable();
    }

    private void OnDisable()
    {
        placeAction?.action?.Disable();
    }

    private void Update()
    {
        if (world == null || highlighter == null || inventory == null)
        {
            return;
        }

        if (!IsPlaceTriggered())
        {
            return;
        }

        if (!highlighter.TryGetTarget(out int3 blockPos, out int3 normal))
        {
            return;
        }

        int3 placePos = blockPos + normal;
        Vector3 center = new Vector3(placePos.x + 0.5f, placePos.y + 0.5f, placePos.z + 0.5f);
        Vector3 halfExtents = Vector3.one * 0.5f - Vector3.one * boundsShrink;

        if (OverlapsPlayer(center, halfExtents))
        {
            return;
        }

        BlockType targetBlock = inventory.SelectedBlock;
        if (targetBlock == BlockType.Air)
        {
            return;
        }

        world.SetBlockAtPosition(placePos, targetBlock);
        world.NotifyNeighborsToRegenerate(World.WorldPosToChunkPos(new float3(placePos.x, placePos.y, placePos.z)));
    }

    private bool IsPlaceTriggered()
    {
        if (placeAction?.action != null && placeAction.action.triggered)
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            return true;
        }
#else
        if (Input.GetMouseButtonDown(1))
        {
            return true;
        }
#endif

        return false;
    }

    private bool OverlapsPlayer(Vector3 center, Vector3 halfExtents)
    {
        Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, collisionMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Transform root = transform.root;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null)
            {
                continue;
            }

            // Ignore chunk colliders and other world geometry; only block placement blocked by player
            if (col.transform.root == root)
            {
                return true;
            }
        }

        return false;
    }
}
