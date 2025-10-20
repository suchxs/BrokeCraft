using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Generates a kilometre-scale distant horizon using Burst jobs and splits the mesh into four
/// cardinal segments so back-facing sections can be culled cheaply.
/// </summary>
[DisallowMultipleComponent]
public class DistantTerrainRenderer : MonoBehaviour
{
    private const int SegmentNorth = 0;
    private const int SegmentSouth = 1;
    private const int SegmentEast = 2;
    private const int SegmentWest = 3;
    private const int SegmentCount = 4;

    private static readonly string[] SegmentNames = { "North", "South", "East", "West" };
    private static readonly float2[] SegmentDirections =
    {
        new float2(0f, 1f),
        new float2(0f, -1f),
        new float2(1f, 0f),
        new float2(-1f, 0f)
    };

    [Header("References")]
    [SerializeField] private World world;
    [SerializeField] private Material distantMaterial;

    [Header("Distance Settings")]
    [SerializeField] [Range(1000f, 16000f)] private float renderDistanceMeters = 8000f;
    [SerializeField] [Range(16f, 256f)] private float sampleSpacing = 64f;
    [SerializeField] private float innerHoleRadius = 256f;
    [SerializeField] private float innerFadeMargin = 96f;
    [SerializeField] private float viewerSnapDistance = 48f;

    private MeshFilter[] segmentFilters;
    private MeshRenderer[] segmentRenderers;
    private Mesh[] segmentMeshes;

    private NativeArray<float> heights;
    private NativeArray<float3> sampleNormals;

    private NativeList<float3>[] segmentVertices = new NativeList<float3>[SegmentCount];
    private NativeList<float3>[] segmentNormals = new NativeList<float3>[SegmentCount];
    private NativeList<float2>[] segmentUVs = new NativeList<float2>[SegmentCount];
    private NativeList<int>[] segmentTriangles = new NativeList<int>[SegmentCount];
    private readonly bool[] segmentHasGeometry = new bool[SegmentCount];

    private JobHandle pipelineHandle;
    private bool pipelineRunning;

    private Transform viewerTransform;
    private float2 currentCenterXZ;
    private bool hasCenter;

    private TerrainNoiseSettings cachedNoiseSettings;
    private bool noiseSettingsCached;

    private int samplesPerAxis;
    private int sampleCount;
    private int allocatedSampleCount = -1;

    private float effectiveInnerRadius;
    private float effectiveFadeMargin;
    private float lastCenterHeight;

    private void Awake()
    {
        EnsureSegments();
    }

    private void OnEnable()
    {
        TryInitializeBuffers();
        ApplyMaterialIfNeeded();
        ForceRefresh();
    }

    private void OnDisable()
    {
        CompleteJobs();
    }

    private void OnDestroy()
    {
        CompleteJobs();
        DisposeBuffers();
        DisposeSegments();
    }

    private void Update()
    {
        if (world == null)
        {
            return;
        }

        TerrainNoiseSettings latestSettings = world.GetTerrainGenerationSettings().ToNoiseSettings();
        if (!noiseSettingsCached || !latestSettings.Equals(cachedNoiseSettings))
        {
            cachedNoiseSettings = latestSettings;
            noiseSettingsCached = true;
            ForceRefresh();
        }

        if (viewerTransform != null)
        {
            TrackViewer();
        }

        if (pipelineRunning && pipelineHandle.IsCompleted)
        {
            pipelineHandle.Complete();
            pipelineRunning = false;
            ApplyMeshes();
        }

        UpdateSegmentVisibility();
    }

    public void Initialize(World worldRef, Material fallbackMaterial)
    {
        world = worldRef;
        if (distantMaterial == null && fallbackMaterial != null)
        {
            distantMaterial = fallbackMaterial;
        }

        EnsureSegments();
        ApplyMaterialIfNeeded();
        TryInitializeBuffers();
        ForceRefresh();
    }

    public void SetViewer(Transform viewer, bool forceRefresh = false)
    {
        viewerTransform = viewer;
        if (forceRefresh && viewerTransform != null)
        {
            hasCenter = false;
            TrackViewer(true);
        }
    }

    public void ConfigureNearField(float nearFieldRadiusMeters)
    {
        float safeRadius = math.max(sampleSpacing, nearFieldRadiusMeters);
        effectiveInnerRadius = safeRadius;
        effectiveFadeMargin = math.max(sampleSpacing * 2f, innerFadeMargin);
        ForceRefresh();
    }

    private void EnsureSegments()
    {
        if (segmentFilters != null && segmentFilters.Length == SegmentCount)
        {
            return;
        }

        segmentFilters = new MeshFilter[SegmentCount];
        segmentRenderers = new MeshRenderer[SegmentCount];
        segmentMeshes = new Mesh[SegmentCount];

        for (int i = 0; i < SegmentCount; i++)
        {
            string name = $"DistantTerrain_{SegmentNames[i]}";
            Transform existing = transform.Find(name);
            GameObject child = existing != null ? existing.gameObject : new GameObject(name);
            child.transform.SetParent(transform, false);

            MeshFilter filter = child.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = child.AddComponent<MeshFilter>();
            }

            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = child.AddComponent<MeshRenderer>();
            }

            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                mesh = new Mesh
                {
                    name = $"{name}_Mesh",
                    indexFormat = IndexFormat.UInt32
                };
                mesh.MarkDynamic();
                filter.sharedMesh = mesh;
            }

            segmentFilters[i] = filter;
            segmentRenderers[i] = renderer;
            segmentMeshes[i] = mesh;
        }

        ApplyMaterialIfNeeded();
    }

    private void DisposeSegments()
    {
        if (segmentMeshes == null)
        {
            return;
        }

        for (int i = 0; i < segmentMeshes.Length; i++)
        {
            if (segmentMeshes[i] != null)
            {
                Mesh mesh = segmentMeshes[i];
                mesh.Clear();
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(mesh);
                }
                else
#endif
                {
                    Destroy(mesh);
                }
            }
        }
    }

    private void TrackViewer(bool force = false)
    {
        if (viewerTransform == null)
        {
            return;
        }

        float2 viewerXZ = new float2(viewerTransform.position.x, viewerTransform.position.z);
        float snap = math.max(sampleSpacing, viewerSnapDistance);
        float2 snapped = math.round(viewerXZ / snap) * snap;

        if (!hasCenter || force || math.distancesq(snapped, currentCenterXZ) > 0.001f)
        {
            UpdateCenter(snapped);
        }
    }

    private void UpdateCenter(float2 newCenterXZ)
    {
        TryInitializeBuffers();

        currentCenterXZ = newCenterXZ;
        hasCenter = true;

        float centerHeight = SampleHeightImmediate(newCenterXZ);
        lastCenterHeight = centerHeight;

        transform.position = new Vector3(newCenterXZ.x, 0f, newCenterXZ.y);

        SchedulePipeline(centerHeight);
    }

    private void ForceRefresh()
    {
        if (viewerTransform != null)
        {
            hasCenter = false;
            TrackViewer(true);
        }
        else if (world != null)
        {
            UpdateCenter(float2.zero);
        }
    }

    private void TryInitializeBuffers()
    {
        if (sampleSpacing < 1f)
        {
            sampleSpacing = 1f;
        }

        samplesPerAxis = math.max(9, ComputeSampleResolution(renderDistanceMeters, sampleSpacing));
        sampleCount = samplesPerAxis * samplesPerAxis;

        if (!heights.IsCreated || allocatedSampleCount != sampleCount)
        {
            DisposeBuffers();

            heights = new NativeArray<float>(sampleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            sampleNormals = new NativeArray<float3>(sampleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            int quadCount = math.max(1, (samplesPerAxis - 1) * (samplesPerAxis - 1));
            int estimatedVertices = quadCount * 4;
            int estimatedIndices = quadCount * 6;
            AllocateSegmentLists(estimatedVertices, estimatedIndices);

            allocatedSampleCount = sampleCount;
        }
        else if (!segmentVertices[SegmentNorth].IsCreated)
        {
            int quadCount = math.max(1, (samplesPerAxis - 1) * (samplesPerAxis - 1));
            int estimatedVertices = quadCount * 4;
            int estimatedIndices = quadCount * 6;
            AllocateSegmentLists(estimatedVertices, estimatedIndices);
        }

        if (effectiveInnerRadius <= 0f)
        {
            effectiveInnerRadius = innerHoleRadius;
            effectiveFadeMargin = math.max(sampleSpacing * 2f, innerFadeMargin);
        }
    }

    private void AllocateSegmentLists(int vertexCapacity, int indexCapacity)
    {
        DisposeSegmentLists();

        int perSegmentVertices = math.max(64, vertexCapacity / SegmentCount);
        int perSegmentIndices = math.max(96, indexCapacity / SegmentCount);

        for (int i = 0; i < SegmentCount; i++)
        {
            segmentVertices[i] = new NativeList<float3>(perSegmentVertices, Allocator.Persistent);
            segmentNormals[i] = new NativeList<float3>(perSegmentVertices, Allocator.Persistent);
            segmentUVs[i] = new NativeList<float2>(perSegmentVertices, Allocator.Persistent);
            segmentTriangles[i] = new NativeList<int>(perSegmentIndices, Allocator.Persistent);
        }
    }

    private void ClearSegmentLists()
    {
        for (int i = 0; i < SegmentCount; i++)
        {
            if (segmentVertices[i].IsCreated) segmentVertices[i].Clear();
            if (segmentNormals[i].IsCreated) segmentNormals[i].Clear();
            if (segmentUVs[i].IsCreated) segmentUVs[i].Clear();
            if (segmentTriangles[i].IsCreated) segmentTriangles[i].Clear();
            segmentHasGeometry[i] = false;
        }
    }

    private void DisposeSegmentLists()
    {
        for (int i = 0; i < SegmentCount; i++)
        {
            if (segmentVertices[i].IsCreated) segmentVertices[i].Dispose();
            if (segmentNormals[i].IsCreated) segmentNormals[i].Dispose();
            if (segmentUVs[i].IsCreated) segmentUVs[i].Dispose();
            if (segmentTriangles[i].IsCreated) segmentTriangles[i].Dispose();
        }
    }

    private void DisposeBuffers()
    {
        if (heights.IsCreated) heights.Dispose();
        if (sampleNormals.IsCreated) sampleNormals.Dispose();
        DisposeSegmentLists();
        allocatedSampleCount = -1;
    }

    private static int ComputeSampleResolution(float distance, float spacing)
    {
        float diameter = distance * 2f;
        int raw = (int)math.ceil(diameter / math.max(1f, spacing)) + 1;
        if ((raw & 1) == 0)
        {
            raw += 1;
        }
        return math.max(raw, 9);
    }

    private void CompleteJobs()
    {
        if (pipelineRunning)
        {
            pipelineHandle.Complete();
            pipelineRunning = false;
        }
    }

    private void SchedulePipeline(float centerHeight)
    {
        if (!heights.IsCreated)
        {
            return;
        }

        CompleteJobs();
        ClearSegmentLists();

        float2 gridStart = currentCenterXZ - new float2(renderDistanceMeters, renderDistanceMeters);

        var heightJob = new DistantTerrainHeightJob
        {
            Heights = heights,
            GridStartXZ = gridStart,
            SampleSpacing = sampleSpacing,
            SamplesPerAxis = samplesPerAxis,
            NoiseSettings = cachedNoiseSettings
        };

        int batchSize = math.max(32, samplesPerAxis);
        JobHandle heightHandle = heightJob.ScheduleParallel(sampleCount, batchSize, default);

        var normalJob = new DistantTerrainNormalJob
        {
            Heights = heights,
            SampleSpacing = sampleSpacing,
            SamplesPerAxis = samplesPerAxis,
            Normals = sampleNormals
        };

        JobHandle normalHandle = normalJob.ScheduleParallel(sampleCount, batchSize, heightHandle);

        float2 viewerXZ = viewerTransform != null
            ? new float2(viewerTransform.position.x, viewerTransform.position.z)
            : currentCenterXZ;

        var meshJob = new DistantTerrainMeshJob
        {
            Heights = heights,
            SampleNormals = sampleNormals,
            SamplesPerAxis = samplesPerAxis,
            SampleSpacing = sampleSpacing,
            CenterXZ = currentCenterXZ,
            GridStartXZ = gridStart,
            InnerHoleRadius = effectiveInnerRadius,
            FadeMargin = effectiveFadeMargin,
            CenterHeight = centerHeight,
            ViewerXZ = viewerXZ,
            VerticesNorth = segmentVertices[SegmentNorth],
            VerticesSouth = segmentVertices[SegmentSouth],
            VerticesEast = segmentVertices[SegmentEast],
            VerticesWest = segmentVertices[SegmentWest],
            NormalsNorth = segmentNormals[SegmentNorth],
            NormalsSouth = segmentNormals[SegmentSouth],
            NormalsEast = segmentNormals[SegmentEast],
            NormalsWest = segmentNormals[SegmentWest],
            UVsNorth = segmentUVs[SegmentNorth],
            UVsSouth = segmentUVs[SegmentSouth],
            UVsEast = segmentUVs[SegmentEast],
            UVsWest = segmentUVs[SegmentWest],
            TrianglesNorth = segmentTriangles[SegmentNorth],
            TrianglesSouth = segmentTriangles[SegmentSouth],
            TrianglesEast = segmentTriangles[SegmentEast],
            TrianglesWest = segmentTriangles[SegmentWest]
        };

        pipelineHandle = meshJob.Schedule(normalHandle);
        pipelineRunning = true;
    }

    private void ApplyMaterialIfNeeded()
    {
        if (segmentRenderers == null)
        {
            return;
        }

        for (int i = 0; i < segmentRenderers.Length; i++)
        {
            MeshRenderer renderer = segmentRenderers[i];
            if (renderer != null && renderer.sharedMaterial == null && distantMaterial != null)
            {
                renderer.sharedMaterial = distantMaterial;
            }
        }
    }

    private float SampleHeightImmediate(float2 worldXZ)
    {
        if (!noiseSettingsCached)
        {
            cachedNoiseSettings = world != null
                ? world.GetTerrainGenerationSettings().ToNoiseSettings()
                : TerrainGenerationSettings.CreateDefault().ToNoiseSettings();
            noiseSettingsCached = true;
        }

        TerrainHeightSample sample = TerrainNoise.SampleHeight(worldXZ, cachedNoiseSettings);
        return sample.Height;
    }

    private void ApplyMeshes()
    {
        if (segmentMeshes == null)
        {
            return;
        }

        float terrainHeightSpan = math.max(256f, cachedNoiseSettings.maxHeight - cachedNoiseSettings.minHeight + math.abs(lastCenterHeight));
        Vector3 boundsSize = new Vector3(renderDistanceMeters * 2f, terrainHeightSpan, renderDistanceMeters * 2f);

        for (int i = 0; i < SegmentCount; i++)
        {
            Mesh mesh = segmentMeshes[i];
            if (mesh == null)
            {
                continue;
            }

            NativeList<float3> verts = segmentVertices[i];
            NativeList<float3> norms = segmentNormals[i];
            NativeList<float2> uv = segmentUVs[i];
            NativeList<int> tris = segmentTriangles[i];

            if (!verts.IsCreated || verts.Length == 0 || !tris.IsCreated || tris.Length == 0)
            {
                mesh.Clear();
                segmentHasGeometry[i] = false;
                if (segmentRenderers[i] != null)
                {
                    segmentRenderers[i].enabled = false;
                }
                continue;
            }

            mesh.Clear();
            mesh.SetVertices(verts.AsArray().Reinterpret<Vector3>());
            mesh.SetNormals(norms.AsArray().Reinterpret<Vector3>());
            mesh.SetUVs(0, uv.AsArray().Reinterpret<Vector2>());
            mesh.SetTriangles(tris.AsArray().ToArray(), 0, true);
            mesh.bounds = new Bounds(Vector3.zero, boundsSize);

            segmentHasGeometry[i] = true;
            if (segmentRenderers[i] != null)
            {
                segmentRenderers[i].enabled = true;
            }
        }
    }

    private void UpdateSegmentVisibility()
    {
        if (segmentRenderers == null)
        {
            return;
        }

        Transform orientation = null;
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            orientation = mainCamera.transform;
        }
        else if (viewerTransform != null)
        {
            orientation = viewerTransform;
        }

        float2 forwardXZ = new float2(0f, 1f);
        bool hasForward = false;

        if (orientation != null)
        {
            Vector3 forward = orientation.forward;
            forwardXZ = new float2(forward.x, forward.z);
            float magnitudeSq = math.lengthsq(forwardXZ);
            if (magnitudeSq > 0.0001f)
            {
                forwardXZ /= math.sqrt(magnitudeSq);
                hasForward = true;
            }
        }

        for (int i = 0; i < SegmentCount; i++)
        {
            MeshRenderer renderer = segmentRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!segmentHasGeometry[i])
            {
                renderer.enabled = false;
                continue;
            }

            if (!hasForward)
            {
                renderer.enabled = true;
                continue;
            }

            float dot = math.dot(forwardXZ, SegmentDirections[i]);
            renderer.enabled = dot >= 0f;
        }
    }
}

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
internal struct DistantTerrainHeightJob : IJobFor
{
    [WriteOnly] public NativeArray<float> Heights;
    public float2 GridStartXZ;
    public float SampleSpacing;
    public int SamplesPerAxis;
    [ReadOnly] public TerrainNoiseSettings NoiseSettings;

    public void Execute(int index)
    {
        int x = index % SamplesPerAxis;
        int z = index / SamplesPerAxis;

        float worldX = GridStartXZ.x + x * SampleSpacing;
        float worldZ = GridStartXZ.y + z * SampleSpacing;

        TerrainHeightSample sample = TerrainNoise.SampleHeight(new float2(worldX, worldZ), NoiseSettings);
        Heights[index] = sample.Height;
    }
}

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
internal struct DistantTerrainNormalJob : IJobFor
{
    [ReadOnly] public NativeArray<float> Heights;
    [WriteOnly] public NativeArray<float3> Normals;
    public int SamplesPerAxis;
    public float SampleSpacing;

    public void Execute(int index)
    {
        int x = index % SamplesPerAxis;
        int z = index / SamplesPerAxis;
        int width = SamplesPerAxis;

        int xl = math.max(0, x - 1);
        int xr = math.min(width - 1, x + 1);
        int zd = math.max(0, z - 1);
        int zu = math.min(width - 1, z + 1);

        float heightL = Heights[xl + z * width];
        float heightR = Heights[xr + z * width];
        float heightD = Heights[x + zd * width];
        float heightU = Heights[x + zu * width];

        float dx = (heightR - heightL) / (2f * SampleSpacing);
        float dz = (heightU - heightD) / (2f * SampleSpacing);

        float3 normal = math.normalize(new float3(-dx, 1f, -dz));
        Normals[index] = normal;
    }
}

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
internal struct DistantTerrainMeshJob : IJob
{
    private const int North = 0;
    private const int South = 1;
    private const int East = 2;
    private const int West = 3;

    [ReadOnly] public NativeArray<float> Heights;
    [ReadOnly] public NativeArray<float3> SampleNormals;
    public int SamplesPerAxis;
    public float SampleSpacing;
    public float2 CenterXZ;
    public float2 GridStartXZ;
    public float InnerHoleRadius;
    public float FadeMargin;
    public float CenterHeight;
    public float2 ViewerXZ;

    [NativeDisableContainerSafetyRestriction] public NativeList<float3> VerticesNorth;
    [NativeDisableContainerSafetyRestriction] public NativeList<float3> VerticesSouth;
    [NativeDisableContainerSafetyRestriction] public NativeList<float3> VerticesEast;
    [NativeDisableContainerSafetyRestriction] public NativeList<float3> VerticesWest;

    [NativeDisableContainerSafetyRestriction] public NativeList<float3> NormalsNorth;
    [NativeDisableContainerSafetyRestriction] public NativeList<float3> NormalsSouth;
    [NativeDisableContainerSafetyRestriction] public NativeList<float3> NormalsEast;
    [NativeDisableContainerSafetyRestriction] public NativeList<float3> NormalsWest;

    [NativeDisableContainerSafetyRestriction] public NativeList<float2> UVsNorth;
    [NativeDisableContainerSafetyRestriction] public NativeList<float2> UVsSouth;
    [NativeDisableContainerSafetyRestriction] public NativeList<float2> UVsEast;
    [NativeDisableContainerSafetyRestriction] public NativeList<float2> UVsWest;

    [NativeDisableContainerSafetyRestriction] public NativeList<int> TrianglesNorth;
    [NativeDisableContainerSafetyRestriction] public NativeList<int> TrianglesSouth;
    [NativeDisableContainerSafetyRestriction] public NativeList<int> TrianglesEast;
    [NativeDisableContainerSafetyRestriction] public NativeList<int> TrianglesWest;

    public void Execute()
    {
        int quadWidth = SamplesPerAxis - 1;
        float fadeRadius = InnerHoleRadius + math.max(0f, FadeMargin);

        for (int z = 0; z < quadWidth; z++)
        {
            for (int x = 0; x < quadWidth; x++)
            {
                float2 corner00 = new float2(GridStartXZ.x + x * SampleSpacing, GridStartXZ.y + z * SampleSpacing);
                float2 corner10 = new float2(corner00.x + SampleSpacing, corner00.y);
                float2 corner01 = new float2(corner00.x, corner00.y + SampleSpacing);
                float2 corner11 = new float2(corner10.x, corner01.y);

                float2 quadCenter = (corner00 + corner11) * 0.5f;
                float distanceToViewer = math.distance(quadCenter, ViewerXZ);

                if (distanceToViewer < InnerHoleRadius)
                {
                    continue;
                }

                float blendWeight = 1f;
                if (FadeMargin > 0.001f && distanceToViewer < fadeRadius)
                {
                    blendWeight = math.saturate((distanceToViewer - InnerHoleRadius) / math.max(FadeMargin, 0.001f));
                }

                int segment = DetermineSegment(quadCenter - CenterXZ);
                AddQuadToSegment(segment, x, z, corner00, corner10, corner01, corner11, blendWeight);
            }
        }
    }

    private int DetermineSegment(float2 relative)
    {
        float absX = math.abs(relative.x);
        float absZ = math.abs(relative.y);

        if (absZ >= absX)
        {
            return relative.y >= 0f ? North : South;
        }

        return relative.x >= 0f ? East : West;
    }

    private void AddQuadToSegment(int segment, int sampleX, int sampleZ, float2 corner00, float2 corner10, float2 corner01, float2 corner11, float blendWeight)
    {
        switch (segment)
        {
            case North:
                AppendQuad(ref VerticesNorth, ref NormalsNorth, ref UVsNorth, ref TrianglesNorth, sampleX, sampleZ, corner00, corner10, corner01, corner11, blendWeight);
                break;
            case South:
                AppendQuad(ref VerticesSouth, ref NormalsSouth, ref UVsSouth, ref TrianglesSouth, sampleX, sampleZ, corner00, corner10, corner01, corner11, blendWeight);
                break;
            case East:
                AppendQuad(ref VerticesEast, ref NormalsEast, ref UVsEast, ref TrianglesEast, sampleX, sampleZ, corner00, corner10, corner01, corner11, blendWeight);
                break;
            case West:
                AppendQuad(ref VerticesWest, ref NormalsWest, ref UVsWest, ref TrianglesWest, sampleX, sampleZ, corner00, corner10, corner01, corner11, blendWeight);
                break;
        }
    }

    private void AppendQuad(ref NativeList<float3> verts, ref NativeList<float3> normals, ref NativeList<float2> uvs, ref NativeList<int> tris,
        int sampleX, int sampleZ, float2 corner00, float2 corner10, float2 corner01, float2 corner11, float blendWeight)
    {
        int baseIndex = verts.Length;

        AppendVertex(ref verts, ref normals, ref uvs, sampleX, sampleZ, corner00, blendWeight);
        AppendVertex(ref verts, ref normals, ref uvs, sampleX + 1, sampleZ, corner10, blendWeight);
        AppendVertex(ref verts, ref normals, ref uvs, sampleX, sampleZ + 1, corner01, blendWeight);
        AppendVertex(ref verts, ref normals, ref uvs, sampleX + 1, sampleZ + 1, corner11, blendWeight);

        tris.Add(baseIndex + 0);
        tris.Add(baseIndex + 2);
        tris.Add(baseIndex + 1);

        tris.Add(baseIndex + 1);
        tris.Add(baseIndex + 2);
        tris.Add(baseIndex + 3);
    }

    private void AppendVertex(ref NativeList<float3> verts, ref NativeList<float3> normals, ref NativeList<float2> uvs,
        int sampleX, int sampleZ, float2 worldXZ, float blendWeight)
    {
        int index = sampleX + sampleZ * SamplesPerAxis;
        float height = math.lerp(CenterHeight, Heights[index], blendWeight);
        float3 normal = SampleNormals[index];

        float2 relative = worldXZ - CenterXZ;
        verts.Add(new float3(relative.x, height, relative.y));
        normals.Add(normal);

        float u = (float)sampleX / (SamplesPerAxis - 1);
        float v = (float)sampleZ / (SamplesPerAxis - 1);
        uvs.Add(new float2(u, v));
    }
}
