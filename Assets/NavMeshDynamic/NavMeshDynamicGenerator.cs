using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

using nmdJobs = NavMeshDynamicJobs.NavMeshDynamicJobs;


public class NavMeshDynamicGenerator : MonoBehaviour
{

    class MeshInfo
    {
        public Vector3[] vertices;
        public int[] triangles;

        public Bounds bounds;

        public Transform transform;

        public ChunkListIndex[] verticesCLInds;

        public ChunkListIndex[] trianglesCLInds;


        public int lastUsedLevel = int.MaxValue;
        public int lastUsedOrder = -1;


        public MeshInfo(MeshCollider col)
        {
            vertices = col.sharedMesh.vertices;
            triangles = col.sharedMesh.triangles;
            
            bounds = col.bounds;

            transform = col.transform;

            verticesCLInds = new ChunkListIndex[vertices.Length];
        }

        public MeshInfo(BoxCollider col)
        {
            GetBoxColliderInfo(col);

            bounds = col.bounds;

            transform = col.transform;

            verticesCLInds = new ChunkListIndex[vertices.Length];
        }


        public void GenerateTrianglesCLInds()
        {
            trianglesCLInds = new ChunkListIndex[triangles.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                trianglesCLInds[i] = verticesCLInds[triangles[i]];
            }
        }


        void GetBoxColliderInfo(BoxCollider boxCol)
        {
            vertices = new Vector3[8];
            Vector3 center = boxCol.center;
            Vector3 ext = boxCol.size / 2;
            vertices[0] = center + new Vector3(-ext.x, -ext.y, -ext.z);
            vertices[1] = center + new Vector3(-ext.x, -ext.y, ext.z);
            vertices[2] = center + new Vector3(-ext.x, ext.y, -ext.z);
            vertices[3] = center + new Vector3(-ext.x, ext.y, ext.z);
            vertices[4] = center + new Vector3(ext.x, -ext.y, -ext.z);
            vertices[5] = center + new Vector3(ext.x, -ext.y, ext.z);
            vertices[6] = center + new Vector3(ext.x, ext.y, -ext.z);
            vertices[7] = center + new Vector3(ext.x, ext.y, ext.z);

            triangles = new int[] { 
                0, 1, 2, 1, 3, 2, 4, 6, 7, 4, 7, 5,
                0, 2, 6, 0, 6, 4, 1, 5, 3, 3, 5, 7,
                2, 3, 7, 2, 7, 6, 0, 4, 1, 1, 4, 5
            };
            
        }

    }


    public struct NodeTri
    {
        Vector3 center;
        public Vector3 Center { get { return center; } }

        ChunkListIndex[] cornerIndices;
        public ChunkListIndex[] CornerIndices { get { return cornerIndices; } }

        List<ChunkListIndex> neighbors;
        public List<ChunkListIndex> Neighbors { get { return neighbors; } }

        public NodeTri(Vector3 center, ChunkListIndex corner1, ChunkListIndex corner2, ChunkListIndex corner3)
        {
            this.center = center;
            cornerIndices = new ChunkListIndex[] { corner1, corner2, corner3 };
            neighbors = new List<ChunkListIndex>();
        }

        public void AddNeighbor(ChunkListIndex cLIndex)
        {
            if(!neighbors.Contains(cLIndex))
            {
                neighbors.Add(cLIndex);
            }
        }

    }


    private delegate void WorkDlg(ChunkListIndex cLIndex, int level, int order);


    private delegate void WorkEnqueueDlg(Vector2Int areaElm, int level, int order);


    #region FIELDS

    [Header("Generator")]
    public ushort chunkSize;
    public LayerMask meshLayers;

    public bool IsMapExpanding;

    public float vertexMergeThreshold;

    public Transform follow;

    public ChunkTracker chunkTracker;

    [Range(4f, 10000f)]
    public float allowedWorkTimeMs;

    [Header("Agent")]
    public float agentWidth;
    public float agentHeight;
    public float maxSlopeAngle;

    ChunkListOcTree<int> meshOccupations; // MAKE CHUNK HASHSET FOR OCCUPATIONS
    Dictionary<int, MeshInfo> meshColInstIDToInfo;
    HashSet<int> meshColFoundInstanceIDs;

    ChunkListOcTree<Vector3> vertices;

    ChunkDictionary<Vector2Int, int> verticesMergeInfo;

    ChunkListOcTree<NodeTri> triangles;

    public ChunkListOcTree<NodeTri> Triangles { get { return triangles; } }

    ChunkDictionary<Vector2Int, List<ChunkListIndex>> cornerToTriangle;

    Queue<ChunkListIndex>[][] workQueues;

    WorkEnqueueDlg[][] workEnqueueFuncs;
    WorkDlg[][] workFuncs;

    bool[][] workDoneFlags;
    int[][] workCurrPoints;

    List<Vector2Int>[] areaNewElmBuffers;
    List<int> chunkAllYsByXZ;

    RaycastHit wayChecker;

    // GUI
    string guiStr;

    [Header("Gizmos")]
    public bool drawTris;
    public Vector3Int drawTrisChunkCenter;
    public bool followTarget;

    public Vector3Int[] drawTrisChunkOffsets;

    System.Diagnostics.Stopwatch stopwatch;

    #endregion FIELDS


    #region JOBS

    nmdJobs.ArrangeVerticesJob arrangeVerticesJob;
    nmdJobs.MarkInvalidTrianglesJobs markInvalidTrianglesJobs;

    #endregion JOBS


    #region POOL VARIABLES

    // GenerateVerticesMergeInfo
    List<Vector3> currLeaf;

    List<Vector3> verticesList;

    HashSet<Vector2Int> checkedCornerKeys;

    #endregion POOL VARIABLES


    private void Start()
    {
        Initializer();

        AddCollidersNewfound();
        chunkTracker.DumpAreaBuffers(ref areaNewElmBuffers, true);

        // GUI
        guiStr = "";
        
    }


    private void Update()
    {
        chunkTracker.Runner();

        ExternalDataExtractor();
        WorksController();
    }


    public void Initializer()
    {
        chunkTracker.Initializer(chunkSize, follow);

        meshOccupations = new ChunkListOcTree<int>(chunkSize, 0);
        meshColInstIDToInfo = new Dictionary<int, MeshInfo>();
        meshColFoundInstanceIDs = new HashSet<int>();

        workFuncs = new WorkDlg[chunkTracker.Areas.Length][];
        workFuncs[3] = new WorkDlg[] { EliminateInvalidTrianglesUsingJobs, ArrangeVerticesUsingJobs, AddVerticesFromMeshInfo };
        workFuncs[2] = new WorkDlg[] { GenerateVerticesMergeInfo };
        workFuncs[1] = new WorkDlg[] { MergeVertices, AddTriangles };
        workFuncs[0] = new WorkDlg[] { TrianglesAddNeighbors };


        workEnqueueFuncs = new WorkEnqueueDlg[chunkTracker.Areas.Length][];
        workEnqueueFuncs[3] = new WorkEnqueueDlg[] { WorkEnqueueMeshChunk, WorkEnqueueMeshChunk, WorkEnqueueMeshChunk };
        workEnqueueFuncs[2] = new WorkEnqueueDlg[] { WorkEnqueueVertices };
        workEnqueueFuncs[1] = new WorkEnqueueDlg[] { WorkEnqueueMeshChunk, WorkEnqueueMeshChunk };
        workEnqueueFuncs[0] = new WorkEnqueueDlg[] { WorkEnqueueTriangles };

        workQueues = new Queue<ChunkListIndex>[chunkTracker.Areas.Length][];
        areaNewElmBuffers = new List<Vector2Int>[chunkTracker.Areas.Length];

        workDoneFlags = new bool[chunkTracker.Areas.Length][];
        workCurrPoints = new int[chunkTracker.Areas.Length][];

        for (int level = 0; level < workQueues.Length; level++) 
        {
            workQueues[level] = new Queue<ChunkListIndex>[workFuncs[level].Length];

            workDoneFlags[level] = new bool[workFuncs[level].Length];
            workCurrPoints[level] = new int[workFuncs[level].Length];

            for (int order = 0; order < workQueues[level].Length; order++) {
                workQueues[level][order] = new Queue<ChunkListIndex>();

                workDoneFlags[level][order] = false;
                workCurrPoints[level][order] = 0;
            }

            areaNewElmBuffers[level] = new List<Vector2Int>();
        }


        vertices = new ChunkListOcTree<Vector3>(chunkSize, 3);

        verticesMergeInfo = new ChunkDictionary<Vector2Int, int>(chunkSize);

        triangles = new ChunkListOcTree<NodeTri>(chunkSize, 3);

        cornerToTriangle = new ChunkDictionary<Vector2Int, List<ChunkListIndex>>();

        chunkTracker.DumpAreaBuffers(ref areaNewElmBuffers, true);

        stopwatch = new System.Diagnostics.Stopwatch();

        // JOBS
        arrangeVerticesJob = new nmdJobs.ArrangeVerticesJob();
        markInvalidTrianglesJobs = new nmdJobs.MarkInvalidTrianglesJobs();

        // POOL
        verticesList = new List<Vector3>();
        checkedCornerKeys = new HashSet<Vector2Int>();

    }


    void ExternalDataExtractor()
    {
        if (chunkTracker.IsCurrChunkChangedXZ)
        {
            if (IsMapExpanding)
            {
                AddCollidersNewfound();
            }
            
            chunkTracker.DumpAreaBuffers(ref areaNewElmBuffers, false);
        }
    }



    void AddCollidersNewfound()
    {
        MeshCollider[] sceneMeshCols = FindObjectsOfType<MeshCollider>();


        foreach (MeshCollider meshCol in sceneMeshCols)
        {
            if (meshColFoundInstanceIDs.Contains(meshCol.GetInstanceID()) || (meshLayers != (meshLayers | (1 << meshCol.gameObject.layer))))
            {
                continue;
            }

            meshColFoundInstanceIDs.Add(meshCol.GetInstanceID());
            meshColInstIDToInfo.Add(meshCol.GetInstanceID(), new MeshInfo(meshCol));

            MeshOccupate(meshCol);
        }

        BoxCollider[] sceneBoxCols = FindObjectsOfType<BoxCollider>();

        foreach (BoxCollider boxCol in sceneBoxCols)
        {
            if (meshColFoundInstanceIDs.Contains(boxCol.GetInstanceID()) || (meshLayers != (meshLayers | (1 << boxCol.gameObject.layer))))
            {
                continue;
            }

            meshColFoundInstanceIDs.Add(boxCol.GetInstanceID());
            meshColInstIDToInfo.Add(boxCol.GetInstanceID(), new MeshInfo((BoxCollider)boxCol));

            MeshOccupate(boxCol);
        }

    }


    void AreaBufferToWorkQueue(int level)
    {
        Vector2Int areaNewElm;
        for (int order = 0; order < workFuncs[level].Length; order++)
        {
            for (int j = 0; j < areaNewElmBuffers[level].Count; j++)
            {
                areaNewElm = areaNewElmBuffers[level][j];
                workEnqueueFuncs[level][order](areaNewElm, level, order);
            }
        }
        areaNewElmBuffers[level].Clear();
    }


    void WorkEnqueueMeshChunk(Vector2Int areaElm, int level, int order)
    {
        try
        {
            chunkAllYsByXZ = meshOccupations.ChunksGroupedByXZ[areaElm];
        }
        catch (KeyNotFoundException) { return; }

        Vector3Int chunkInd;

        List<int> instIDs;

        ChunkListIndex chunkListIndex;

        for (int i = 0; i < chunkAllYsByXZ.Count; i++)
        {
            chunkInd = new Vector3Int(areaElm.x, chunkAllYsByXZ[i], areaElm.y);
            instIDs = meshOccupations.GetChunkUnified(chunkInd);

            foreach (int instID in instIDs)
            {

                if(meshColInstIDToInfo[instID].lastUsedLevel < level 
                    || (meshColInstIDToInfo[instID].lastUsedLevel == level && meshColInstIDToInfo[instID].lastUsedOrder >= order))
                {
                    //Debug.Log(instID + "last used l-o: " + meshColInstIDToInfo[instID].lastUsedLevel + " " + meshColInstIDToInfo[instID].lastUsedOrder + "   l-o: " + level + " " + order);
                    continue;
                }

                chunkListIndex = new ChunkListIndex(Vector3Int.zero, 0, instID);

                workQueues[level][order].Enqueue(chunkListIndex);

                meshColInstIDToInfo[instID].lastUsedLevel = level;
                meshColInstIDToInfo[instID].lastUsedOrder = order;

                if(level == 0 && order == workFuncs[level].Length - 1)
                {
                    MeshVacate(instID);
                }
            }

        }

    }


    void WorkEnqueueVertices(Vector2Int areaElm, int level, int order)
    {
        try
        {
            chunkAllYsByXZ = vertices.ChunksGroupedByXZ[areaElm];
        }
        catch (KeyNotFoundException) { return; }

        for (int i = 0; i < chunkAllYsByXZ.Count; i++)
        {
            workQueues[level][order].Enqueue(new ChunkListIndex(
                new Vector3Int(areaElm.x, chunkAllYsByXZ[i], areaElm.y),
                0,
                0)
                );
        }

    }


    void WorkEnqueueTriangles(Vector2Int areaElm, int level, int order)
    {
        try
        {
            chunkAllYsByXZ = triangles.ChunksGroupedByXZ[areaElm];
        }
        catch (KeyNotFoundException) { return; }

        for (int i = 0; i < chunkAllYsByXZ.Count; i++)
        {
            workQueues[level][order].Enqueue(new ChunkListIndex(
                new Vector3Int(areaElm.x, chunkAllYsByXZ[i], areaElm.y),
                0,
                0)
                );
        }

    }


    void WorksController()
    {

        for(int level = workQueues.Length - 1; level >= 0; level--)
        {
            if (areaNewElmBuffers[level].Count > 0)
            {
                AreaBufferToWorkQueue(level);
                return;
            }
            else if (IsWorkQueueLevelNotEmpty(level))
            {
                WorksRunner(level);
                return;
            }
        }

    }


    void WorksRunner(int level)
    {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        for (int order = 0; order < workFuncs[level].Length; order++)
        {
            if (workQueues[level][order].Count == 0)
            {
                continue;
            }

            stopwatch.Restart();
            workFuncs[level][order](workQueues[level][order].Peek(), level, order);
            stopwatch.Stop();

            if (workDoneFlags[level][order])
            {
                workQueues[level][order].Dequeue();
                workDoneFlags[level][order] = false;
                workCurrPoints[level][order] = 0;
            }

            float elapsed = stopwatch.ElapsedMilliseconds;

            if(elapsed > allowedWorkTimeMs)
            {
                Debug.LogWarning("l: " + level + " o: " + order + " : " + workFuncs[level][order].Method.Name + ": " + (elapsed).ToString() + "ms");
            }
            else
            {
                Debug.Log("l: " + level + " o: " + order + " : " + workFuncs[level][order].Method.Name + ": " + (elapsed).ToString() + "ms");
            }

            if(elapsed < allowedWorkTimeMs)
            {
                continue;
            }
            
            break;
        }
    }


    void MeshOccupate(Collider col)
    {
        int meshColInstId = col.GetInstanceID();

        Vector3 min = col.bounds.min, max = col.bounds.max;

        Vector3Int minChunk = new Vector3Int(Mathf.FloorToInt(min.x / chunkSize), Mathf.FloorToInt(min.y / chunkSize), Mathf.FloorToInt(min.z / chunkSize));
        Vector3Int maxChunk = new Vector3Int(Mathf.FloorToInt(max.x / chunkSize), Mathf.FloorToInt(max.y / chunkSize), Mathf.FloorToInt(max.z / chunkSize));

        for (int x = minChunk.x; x <= maxChunk.x; x++) {
            for (int y = minChunk.y; y <= maxChunk.y; y++) {
                for (int z = minChunk.z; z <= maxChunk.z; z++) {
                    meshOccupations.AddElement(meshColInstId, new Vector3(x * chunkSize, y * chunkSize, z * chunkSize));
                }
            }
        }

    }


    MeshInfo MeshVacate(int meshColInstId)
    {
        MeshInfo meshInfo = meshColInstIDToInfo[meshColInstId];

        Vector3 min = meshInfo.bounds.min, max = meshInfo.bounds.max;

        Vector3Int minChunk = new Vector3Int(Mathf.FloorToInt(min.x / chunkSize), Mathf.FloorToInt(min.y / chunkSize), Mathf.FloorToInt(min.z / chunkSize));
        Vector3Int maxChunk = new Vector3Int(Mathf.FloorToInt(max.x / chunkSize), Mathf.FloorToInt(max.y / chunkSize), Mathf.FloorToInt(max.z / chunkSize));

        for (int x = minChunk.x; x <= maxChunk.x; x++) {
            for (int y = minChunk.y; y <= maxChunk.y; y++) {
                for (int z = minChunk.z; z <= maxChunk.z; z++) {
                    meshOccupations.RemoveFromLeaf(new ChunkListIndex(new Vector3Int(x, y, z), 0, 0), meshColInstId);
                }
            }
        }

        return meshInfo;
    }


    void EliminateInvalidTriangles(ChunkListIndex cLIndex, int level, int order)
    {
        EliminateInvalidTriangles(cLIndex.ListIndex, level, order);
    }
    void EliminateInvalidTriangles(int meshColInstID, int level, int order)
    {
        MeshInfo meshInfo = meshColInstIDToInfo[meshColInstID];

        var vertices = meshInfo.vertices;
        var triangles = meshInfo.triangles;

        int[] newTriangles = new int[triangles.Length];

        int newIndsCounter = 0;

        for(int i = 0; i < triangles.Length; i+=3)
        {
            
            if(maxSlopeAngle < CalculateNormal(vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]))
            {
                continue;
            }

            newTriangles[newIndsCounter] = triangles[i];
            newTriangles[newIndsCounter + 1] = triangles[i + 1];
            newTriangles[newIndsCounter + 2] = triangles[i + 2];
            newIndsCounter += 3;

        }

        Array.Resize(ref newTriangles, newIndsCounter);

        meshInfo.triangles = newTriangles;

        workDoneFlags[level][order] = true;
    }


    void EliminateInvalidTrianglesUsingJobs(ChunkListIndex cLIndex, int level, int order)
    {
        EliminateInvalidTrianglesUsingJobs(cLIndex.ListIndex, level, order);
    }
    void EliminateInvalidTrianglesUsingJobs(int meshColInstID, int level, int order)
    {
        MeshInfo meshInfo = meshColInstIDToInfo[meshColInstID];

        markInvalidTrianglesJobs.vertices = new NativeArray<Vector3>(meshInfo.vertices, Allocator.Persistent);
        markInvalidTrianglesJobs.triangles = new NativeArray<int>(meshInfo.triangles, Allocator.Persistent);
        markInvalidTrianglesJobs.maxSlopeAngel = maxSlopeAngle;

        JobHandle jobHandle = markInvalidTrianglesJobs.Schedule(meshInfo.triangles.Length / 3, 64);

        jobHandle.Complete();

        markInvalidTrianglesJobs.triangles.CopyTo(meshInfo.triangles);

        markInvalidTrianglesJobs.vertices.Dispose();
        markInvalidTrianglesJobs.triangles.Dispose();

        int nextTriplet = 0;
        for(int i = 0; i < meshInfo.triangles.Length; i += 3)
        {
            if (meshInfo.triangles[i] != -1)
            {
                meshInfo.triangles[nextTriplet] = meshInfo.triangles[i];
                meshInfo.triangles[nextTriplet + 1] = meshInfo.triangles[i + 1];
                meshInfo.triangles[nextTriplet + 2] = meshInfo.triangles[i + 2];
                nextTriplet += 3;
            }
        }

        Array.Resize(ref meshInfo.triangles, nextTriplet);

        workDoneFlags[level][order] = true;
    }


    void ArrangeVertices(ChunkListIndex cLIndex, int level, int order)
    {
        ArrangeVertices(cLIndex.ListIndex, level, order);
    }
    void ArrangeVertices(int meshColInstID, int level, int order)
    {
        MeshInfo meshInfo = meshColInstIDToInfo[meshColInstID];

        for (int i = 0; i < meshInfo.vertices.Length; i++)
        {
            meshInfo.vertices[i] = RoundVector3XZ(meshInfo.transform.TransformPoint(meshInfo.vertices[i]), vertexMergeThreshold);
        }

        workDoneFlags[level][order] = true;
    }


    void ArrangeVerticesUsingJobs(ChunkListIndex cLIndex, int level, int order)
    {
        ArrangeVerticesUsingJobs(cLIndex.ListIndex, level, order);
    }
    void ArrangeVerticesUsingJobs(int meshColInstID, int level, int order)
    {
        MeshInfo meshInfo = meshColInstIDToInfo[meshColInstID];

        arrangeVerticesJob.vertices = new NativeArray<Vector3>(meshInfo.vertices, Allocator.Persistent);
        arrangeVerticesJob.localToWorldMatrix = meshInfo.transform.localToWorldMatrix;
        arrangeVerticesJob.vertexMergeThreshold = vertexMergeThreshold;

        JobHandle jobHandle = arrangeVerticesJob.Schedule(meshInfo.vertices.Length, 64);

        jobHandle.Complete();

        arrangeVerticesJob.vertices.CopyTo(meshInfo.vertices);

        arrangeVerticesJob.vertices.Dispose();

        workDoneFlags[level][order] = true;
    }


    void AddVerticesFromMeshInfo(ChunkListIndex cLIndex, int level, int order)
    {
        AddVerticesFromMeshInfo(cLIndex.ListIndex, level, order);
    }
    void AddVerticesFromMeshInfo(int meshColInstID, int level, int order)
    {
        MeshInfo meshInfo = meshColInstIDToInfo[meshColInstID];
        Vector3 vertex;

        stopwatch.Restart();
        for (int i = workCurrPoints[level][order]; i < meshInfo.vertices.Length; i++)
        {
            vertex = meshInfo.vertices[i];

            meshInfo.verticesCLInds[i] = vertices.AddElement(vertex, vertex);

            if(stopwatch.ElapsedMilliseconds > allowedWorkTimeMs)
            {
                stopwatch.Stop();
                workCurrPoints[level][order] = i;
                return;
            }
        }

        workDoneFlags[level][order] = true;
    }


    void GenerateVerticesMergeInfo(ChunkListIndex cLIndex, int level, int order)
    {

        List<Vector3>[] chunk;
        try
        {
            chunk = vertices.GetChunk(cLIndex);
        }
        catch (KeyNotFoundException) { return; }

        Vector3 vertex;

        ChunkListIndex vertexInd = cLIndex;

        for (int leafInd = 0; leafInd < chunk.Length; leafInd++)
        {
            vertexInd.LeafIndex = leafInd;
            currLeaf = chunk[leafInd];

            if (currLeaf == null) { continue; }

            for (int listInd = 0; listInd < currLeaf.Count; listInd++)
            {
                vertex = currLeaf[listInd];

                vertexInd.ListIndex = FindSameVertex(vertex, vertexInd, listInd);

                if (vertexInd.ListIndex != -1)
                {
                    verticesMergeInfo.AddElement(cLIndex.ChunkIndex, new Vector2Int(leafInd, listInd), vertexInd.ListIndex);
                    continue;
                }
            }
        }

        workDoneFlags[level][order] = true;
    }


    int FindSameVertex(Vector3 vertex, ChunkListIndex checkLeafInd, int checkUpTo = -1)
    {
        vertices.GetLeaf(checkLeafInd, ref verticesList);

        if (verticesList.Count == 0) { return -1; }

        if (checkUpTo == -1) { checkUpTo = verticesList.Count; }

        for (int i = 0; i < checkUpTo; i++)
        {
            if (IsVerticesNear(verticesList[i], vertex, vertexMergeThreshold))
            {
                return i;
            }
        }

        return -1;

    }


    void MergeVertices(ChunkListIndex cLIndex, int level, int order)
    {
        MergeVertices(cLIndex.ListIndex, level, order);
    }
    void MergeVertices(int meshColInstID, int level, int order)
    {
        MeshInfo meshInfo = meshColInstIDToInfo[meshColInstID];

        ChunkListIndex originalCLIndex;

        stopwatch.Restart();
        for(int i = workCurrPoints[level][order]; i < meshInfo.verticesCLInds.Length; i++)
        {
            originalCLIndex = meshInfo.verticesCLInds[i];

            if (verticesMergeInfo.ContainsKey(originalCLIndex.ChunkIndex, new Vector2Int(originalCLIndex.LeafIndex, originalCLIndex.ListIndex)))
            {
                meshInfo.verticesCLInds[i].ListIndex = verticesMergeInfo.GetElement(originalCLIndex.ChunkIndex, new Vector2Int(originalCLIndex.LeafIndex, originalCLIndex.ListIndex));

                verticesMergeInfo.RemoveKey(originalCLIndex.ChunkIndex, new Vector2Int(originalCLIndex.LeafIndex, originalCLIndex.ListIndex));
            }

            if(stopwatch.ElapsedMilliseconds > allowedWorkTimeMs)
            {
                stopwatch.Stop();
                workCurrPoints[level][order] = i;
                return;
            }

        }
        stopwatch.Stop();

        meshInfo.GenerateTrianglesCLInds();
        workDoneFlags[level][order] = true;
    }


    void AddTriangles(ChunkListIndex cLIndex, int level, int order)
    {
        AddTriangles(cLIndex.ListIndex, level, order);
    }
    void AddTriangles(int meshColInstID, int level, int order)
    {

        ChunkListIndex[] trianglesCLInds = meshColInstIDToInfo[meshColInstID].trianglesCLInds;

        ChunkListIndex cLIndex;

        Vector3 center;

        NodeTri nodeTri;

        stopwatch.Restart();
        for(int i = workCurrPoints[level][order]; i < trianglesCLInds.Length; i += 3)
        {
            center = vertices.GetElement(trianglesCLInds[i]) + vertices.GetElement(trianglesCLInds[i + 1]) + vertices.GetElement(trianglesCLInds[i + 2]);
            center /= 3;

            nodeTri = new NodeTri(center, trianglesCLInds[i], trianglesCLInds[i + 1], trianglesCLInds[i + 2]);

            cLIndex = triangles.AddElement(nodeTri, center);


            for (int j = 0; j < 3; j++)
            {
                try
                {
                    cornerToTriangle.GetElement(trianglesCLInds[i + j].ChunkIndex
                        , new Vector2Int(trianglesCLInds[i + j].LeafIndex
                        , trianglesCLInds[i + j].ListIndex)).Add(cLIndex);
                }
                catch (KeyNotFoundException)
                {
                    cornerToTriangle.AddElement(trianglesCLInds[i + j].ChunkIndex
                        , new Vector2Int(trianglesCLInds[i + j].LeafIndex
                        , trianglesCLInds[i + j].ListIndex), new List<ChunkListIndex> { cLIndex });
                }

            }
            
            if(stopwatch.ElapsedMilliseconds > allowedWorkTimeMs)
            {
                stopwatch.Stop();
                workCurrPoints[level][order] = i;
                return;
            }
        }

        workDoneFlags[level][order] = true;
    }

    
    void TrianglesAddNeighbors(ChunkListIndex cLIndex, int level, int order)
    {

        int leafCount = triangles.LeafCount;

        List<ChunkListIndex>[] cornerToTris = new List<ChunkListIndex>[3];

        List<NodeTri> triList;

        ChunkListIndex cornerCLI, triCLI;

        Vector3 triCenter1, triCenter2;
        Vector3 direction;
        Vector3 yOffset = Vector3.up * (agentHeight / 2);
        float radius = agentWidth / 2;

        if (workCurrPoints[level][order] == 0)
        {
            checkedCornerKeys.Clear();
        }

        stopwatch.Restart();
        for(int leafInd = workCurrPoints[level][order]; leafInd < leafCount; leafInd++)
        {
            triList = triangles.GetLeaf(new ChunkListIndex(cLIndex.ChunkIndex, leafInd, 0));
            if(triList == null) { continue; }

            for(int listInd = 0; listInd < triList.Count; listInd++)
            {

                for(int i = 0; i < triList[listInd].CornerIndices.Length; i++)
                {
                    cornerCLI = triList[listInd].CornerIndices[i];

                    if (!cornerToTriangle.ContainsKey(cornerCLI.ChunkIndex, new Vector2Int(cornerCLI.LeafIndex, cornerCLI.ListIndex)))
                    {
                        cornerToTris[i] = null;
                        continue;
                    }

                    checkedCornerKeys.Add(new Vector2Int(cornerCLI.LeafIndex, cornerCLI.ListIndex));
                    cornerToTris[i] = cornerToTriangle.GetElement(cornerCLI.ChunkIndex, new Vector2Int(cornerCLI.LeafIndex, cornerCLI.ListIndex));
                }

                /*
                //ONE CORNER
                for (int c = 0; c < 3; c++)
                {
                    if (cornerToTris[c] == null) { continue; }

                    for (int i = 0; i < cornerToTris[c].Count; i++)
                    {
                        triCLI = cornerToTris[c][i];

                        triCenter1 = triangles.GetElement(triCLI).Center;
                        triCenter2 = triangles.GetElement(new ChunkListIndex(cLIndex.ChunkIndex, leafInd, listInd)).Center;

                        direction = triCenter2 - triCenter1;

                        //if(!Physics.SphereCast(triCenter1 + yOffset, radius, direction, out wayChecker, direction.magnitude, meshLayers))
                        if(!Physics.Raycast(triCenter1 + yOffset, direction, out wayChecker, direction.magnitude, meshLayers))
                        {
                            triangles.GetLeaf(new ChunkListIndex(cLIndex.ChunkIndex, leafInd, 0))[listInd].AddNeighbor(triCLI);
                            triangles.GetLeaf(new ChunkListIndex(triCLI.ChunkIndex, triCLI.LeafIndex, 0))[triCLI.ListIndex].AddNeighbor(new ChunkListIndex(cLIndex.ChunkIndex, leafInd, listInd));
                        }

                        
                        
                    }
                }

                for (int i = 0; i < triList[listInd].CornerIndices.Length; i++)
                {
                    cornerCLI = triList[listInd].CornerIndices[i];
                    cornerToTriangle.RemoveElement(cornerCLI.ChunkIndex, new Vector2Int(cornerCLI.LeafIndex, cornerCLI.ListIndex));
                }
                */

            }

            if (stopwatch.ElapsedMilliseconds > allowedWorkTimeMs)
            {
                stopwatch.Stop();
                workCurrPoints[level][order] = leafInd;
                return;
            }

        }

        workDoneFlags[level][order] = true;
    }


    bool IsWorkQueueLevelNotEmpty(int level)
    {
        for (int order = 0; order < workQueues[level].Length; order++)
        {
            if (workQueues[level][order].Count > 0)
            {
                return true;
            }
        }
        return false;
    }


    static float CalculateNormal(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        Vector3 cross = Vector3.Cross(v1 - v0, v2 - v0);
        return Mathf.Abs(Vector3.SignedAngle(Vector3.up, cross, new Vector3(-cross.z, 0, cross.x)));
    }


    static Vector3 RoundVector3XZ(Vector3 v3, float roundTo)
    {
        return new Vector3(RoundToNearestFloat(v3.x, roundTo), v3.y, RoundToNearestFloat(v3.z, roundTo));
    }


    static bool IsVerticesNear(Vector3 v1, Vector3 v2, float threshold)
    {
        return (
            v1.x == v2.x &&
            v1.z == v2.z &&
            (Mathf.Abs(v1.y - v2.y) < threshold)
            );
    }


    static float RoundToNearestFloat(float f, float roundTo)
    {
        return Mathf.Round(f / roundTo) * roundTo;
    }


    private void OnGUI()
    {

        guiStr = "WORK QUEUES:\n";

        for (int level = workQueues.Length - 1; level >= 0; level--)
        {
            for(int order = 0; order < workQueues[level].Length; order++)
            {
                guiStr += level + "," + order + " ::= " + workQueues[level][order].Count + "\n";
            }
        }

        guiStr += "\nAREA BUFFERS:\n";

        for (int level = workQueues.Length - 1; level >= 0; level--)
        {
            guiStr += level + " ::= " + areaNewElmBuffers[level].Count + "\n";
        }

        GUI.TextField(new Rect(10, 10, 80, 240), guiStr);

    }

    private void OnDrawGizmos()
    {
        List<NodeTri>[] tris;
        Gizmos.color = Color.red;

        if (followTarget)
        {
            drawTrisChunkCenter = chunkTracker.CurrChunk;
        }

        Vector3 offset = Vector3.up * 0.1f;

        if(triangles == null)
        {
            return;
        }
        if (drawTris)
        {

            for(int n = 0; n < drawTrisChunkOffsets.Length; n++)
            {
                if (drawTris)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireCube(chunkSize * (drawTrisChunkCenter + drawTrisChunkOffsets[n] + (0.5f * Vector3.one)), chunkSize * Vector3.one);
                }

                try
                {
                    tris = triangles.GetChunk(new ChunkListIndex(drawTrisChunkCenter + drawTrisChunkOffsets[n], 0, 0));
                }
                catch (KeyNotFoundException)
                {
                    continue;
                }

                for (int i = 0; i < tris.Length; i++)
                {
                    if (tris[i] == null) { continue; }

                    for (int j = 0; j < tris[i].Count; j++)
                    {
                        Gizmos.color = Color.magenta;
                        for (int k = 0; k < 2; k++)
                        {
                            Gizmos.DrawLine(offset + vertices.GetElement(tris[i][j].CornerIndices[k]), offset + vertices.GetElement(tris[i][j].CornerIndices[k + 1]));
                        }
                        Gizmos.DrawLine(offset + vertices.GetElement(tris[i][j].CornerIndices[2]), offset + vertices.GetElement(tris[i][j].CornerIndices[0]));

                        Gizmos.color = new Color(0, 0, 1, 0.3f);

                        for (int k = 0; k < tris[i][j].Neighbors.Count; k++)
                        {
                            Gizmos.DrawLine(offset + tris[i][j].Center, offset + triangles.GetElement(tris[i][j].Neighbors[k]).Center);
                        }

                    }

                }
            }
        }
    }


}
