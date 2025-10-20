using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Handles player spawning at a safe location above terrain.
/// Automatically positions player after world generation completes.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private World world;
    [SerializeField] private GameObject playerPrefab;
    
    [Header("Spawn Settings")]
    [Tooltip("World coordinates to spawn at (XZ)")]
    [SerializeField] private Vector2Int spawnCoordinates = new Vector2Int(0, 0);
    
    [Tooltip("Delay before spawning (seconds) - allows chunks to generate")]
    [SerializeField] private float spawnDelay = 0.5f;
    
    [Tooltip("If true, automatically spawn player on start")]
    [SerializeField] private bool autoSpawn = true;
    
    private GameObject spawnedPlayer;
    private bool hasSpawned = false;
    
    private void Start()
    {
        if (world == null)
        {
            world = FindObjectOfType<World>();
            if (world == null)
            {
                Debug.LogError("PlayerSpawner: No World found in scene!");
                enabled = false;
                return;
            }
        }
        
        if (autoSpawn)
        {
            Invoke(nameof(SpawnPlayer), spawnDelay);
        }
    }
    
    /// <summary>
    /// Spawn the player at a safe location above terrain
    /// </summary>
    public void SpawnPlayer()
    {
        if (hasSpawned)
        {
            Debug.LogWarning("PlayerSpawner: Player already spawned!");
            return;
        }
        
        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawner: No player prefab assigned!");
            return;
        }
        
        // Get safe spawn position above terrain
        Vector3 spawnPos = world.GetSpawnPosition(spawnCoordinates.x, spawnCoordinates.y);
        
        // Instantiate player
        spawnedPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        spawnedPlayer.name = "Player";
        
        // Assign world reference to player controller if it has one
        PlayerController playerController = spawnedPlayer.GetComponent<PlayerController>();
        if (playerController != null && playerController.GetType().GetField("world", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null)
        {
            // Use reflection to set private world field if needed
            // Note: PlayerController already finds World via FindObjectOfType, but we can force it here
        }
        
        hasSpawned = true;
        
        Debug.Log($"[PlayerSpawner] Spawned player at {spawnPos}");
        Debug.Log($"[PlayerSpawner] Chunk position: {CubicChunkHelper.WorldFloatPosToChunkPos(spawnPos)}");
    }
    
    /// <summary>
    /// Get the spawned player GameObject
    /// </summary>
    public GameObject GetPlayer()
    {
        return spawnedPlayer;
    }
    
    /// <summary>
    /// Respawn player at spawn coordinates
    /// </summary>
    public void RespawnPlayer()
    {
        if (spawnedPlayer != null)
        {
            Destroy(spawnedPlayer);
            hasSpawned = false;
        }
        
        SpawnPlayer();
    }
}
