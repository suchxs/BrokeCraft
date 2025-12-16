using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles creative-style block breaking: left click deletes the highlighted block instantly.
/// Relies on BlockHighlighter for target selection.
/// </summary>
[DisallowMultipleComponent]
public class BlockBreaker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private World world;
    [SerializeField] private BlockHighlighter highlighter;
    [SerializeField] private BlockInventory inventory;
    [SerializeField] private InputActionReference attackAction;

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
        attackAction?.action?.Enable();
    }

    private void OnDisable()
    {
        attackAction?.action?.Disable();
    }

    private void Update()
    {
        if (world == null || highlighter == null)
        {
            return;
        }

        if (!IsAttackTriggered())
        {
            return;
        }

        if (!highlighter.TryGetHighlightedBlock(out int3 blockPos))
        {
            return;
        }

        world.SetBlockAtPosition(blockPos, BlockType.Air);
        world.NotifyNeighborsToRegenerate(World.WorldPosToChunkPos(new float3(blockPos.x, blockPos.y, blockPos.z)));
    }

    private bool IsAttackTriggered()
    {
        if (attackAction?.action != null && attackAction.action.triggered)
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }
#endif

        return false;
    }
}
