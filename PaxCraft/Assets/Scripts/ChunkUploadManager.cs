using System.Collections.Generic;
using UnityEngine;

// MINECRAFT-STYLE FRAME BUDGET SYSTEM
// Limits GPU mesh uploads per frame to maintain smooth 60 FPS
public class ChunkUploadManager : MonoBehaviour
{
    // Frame budget settings (Minecraft-style)
    private const float TARGET_FRAME_TIME = 0.016f;  // 16ms = 60 FPS
    private const int MAX_UPLOADS_PER_FRAME = 2;     // Max mesh uploads per frame
    private const float UPLOAD_TIME_BUDGET = 0.008f; // 8ms budget for uploads
    
    // Queue of chunks waiting to be uploaded to GPU
    private Queue<MeshDataResult> pendingUploads = new Queue<MeshDataResult>();
    private Queue<MeshDataResult> priorityUploads = new Queue<MeshDataResult>(); // Near player
    
    // Statistics
    private int totalUploadsThisFrame = 0;
    private float frameStartTime = 0f;
    
    // Singleton
    public static ChunkUploadManager Instance { get; private set; }
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    void Update()
    {
        frameStartTime = Time.realtimeSinceStartup;
        totalUploadsThisFrame = 0;
        
        // Process uploads with frame budget
        ProcessPendingUploads();
    }
    
    void ProcessPendingUploads()
    {
        // Priority queue first (chunks near player)
        while (priorityUploads.Count > 0 && CanUploadMore())
        {
            MeshDataResult result = priorityUploads.Dequeue();
            UploadMeshData(result);
        }
        
        // Then regular queue
        while (pendingUploads.Count > 0 && CanUploadMore())
        {
            MeshDataResult result = pendingUploads.Dequeue();
            UploadMeshData(result);
        }
    }
    
    bool CanUploadMore()
    {
        // Check upload count limit
        if (totalUploadsThisFrame >= MAX_UPLOADS_PER_FRAME)
            return false;
        
        // Check time budget
        float elapsed = Time.realtimeSinceStartup - frameStartTime;
        if (elapsed > UPLOAD_TIME_BUDGET)
            return false;
        
        return true;
    }
    
    void UploadMeshData(MeshDataResult result)
    {
        // Create chunk and upload mesh data
        World world = FindObjectOfType<World>();
        if (world != null)
        {
            world.CreateChunkFromMeshDataImmediate(result);
            totalUploadsThisFrame++;
        }
    }
    
    // Queue mesh data for upload (called from threading system)
    public void QueueMeshUpload(MeshDataResult result, bool isPriority = false)
    {
        if (isPriority)
            priorityUploads.Enqueue(result);
        else
            pendingUploads.Enqueue(result);
    }
    
    // Get queue statistics
    public string GetStats()
    {
        int total = pendingUploads.Count + priorityUploads.Count;
        return $"Upload Queue: {total} ({priorityUploads.Count} priority)";
    }
    
    // Check if a specific chunk is near the player (priority)
    public static bool IsNearPlayer(ChunkCoord chunkCoord, Vector3 playerPos)
    {
        Vector3 chunkWorldPos = new Vector3(
            chunkCoord.x * VoxelData.ChunkWidth + VoxelData.ChunkWidth / 2,
            0,
            chunkCoord.z * VoxelData.ChunkWidth + VoxelData.ChunkWidth / 2
        );
        
        float distance = Vector3.Distance(new Vector3(playerPos.x, 0, playerPos.z), chunkWorldPos);
        return distance < VoxelData.ChunkWidth * 3; // Within 3 chunks = priority
    }
}

