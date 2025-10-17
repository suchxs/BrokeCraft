using System.Collections.Generic;
using UnityEngine;

// MINECRAFT-STYLE FRAME BUDGET SYSTEM
// Limits GPU mesh uploads per frame to maintain smooth 60 FPS
public class ChunkUploadManager : MonoBehaviour
{
    // Frame budget settings (Minecraft-style)
    private const float TARGET_FRAME_TIME = 0.016f;  // 16ms = 60 FPS
    private const int MAX_UPLOADS_PER_FRAME = 8;     // AGGRESSIVE FIX: Increased to 8 for 4x faster chunk loading!
    private const float UPLOAD_TIME_BUDGET = 0.012f; // AGGRESSIVE FIX: Increased to 12ms budget (75% of frame)
    
    // OPTIMIZATION: Dynamic frame budget based on actual frame times
    private const bool USE_DYNAMIC_BUDGET = true;
    private float lastFrameTime = 0f;
    
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
        
        // Track last frame time for dynamic budgeting
        lastFrameTime = Time.deltaTime;
        
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
        // AGGRESSIVE FIX: Dynamic budgeting - if FPS is good, upload more!
        int maxUploads = MAX_UPLOADS_PER_FRAME;
        
        if (USE_DYNAMIC_BUDGET && lastFrameTime > 0)
        {
            // If last frame was fast (< 13ms), allow more uploads
            if (lastFrameTime < 0.013f)
            {
                maxUploads = MAX_UPLOADS_PER_FRAME + 4; // Boost to 12 when FPS is good!
            }
            // If frame is slow (> 20ms), reduce uploads
            else if (lastFrameTime > 0.020f)
            {
                maxUploads = Mathf.Max(2, MAX_UPLOADS_PER_FRAME / 2); // Reduce to 4 when struggling
            }
        }
        
        // Check upload count limit
        if (totalUploadsThisFrame >= maxUploads)
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

