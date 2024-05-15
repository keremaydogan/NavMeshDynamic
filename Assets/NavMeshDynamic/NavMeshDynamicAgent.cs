using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

using NodeTri = NavMeshDynamicGenerator.NodeTri;

public class NavMeshDynamicAgent : MonoBehaviour
{
    struct NodeAStar
    {
        public int prev;
        public ChunkListIndex cli;
        public Vector3 pos;
        public float G;
        public float H;
        public float F { get { return G + H; } }

        public NodeAStar(ChunkListIndex cli, Vector3 pos, int prev = -1, float g = 0, float h = 0)
        {
            this.cli = cli;
            this.pos = pos;
            this.prev = prev;
            this.G = g;
            this.H = h;
        }

    }

    public NavMeshDynamicGenerator nmdGen;

    public float agentWidth;
    public float agentHeight;

    ChunkListOcTree<NodeTri> triangles;

    System.Diagnostics.Stopwatch stopwatch;

    #region AStar

    List<NodeAStar> openList;
    List<NodeAStar> closedList;
    HashSet<int> checkedNodes;
    HashSet<ChunkListIndex> checkedTris;
    bool isDestinationReached;
    RaycastHit wayChecker;

    public LayerMask obstacleLayers;

    List<Vector3> path;

    #endregion AStar


    private void Awake()
    {
        triangles = nmdGen.Triangles;

        stopwatch = new System.Diagnostics.Stopwatch();

        #region AStar

        openList = new List<NodeAStar>();
        closedList = new List<NodeAStar>();
        checkedNodes = new HashSet<int>();
        checkedTris = new HashSet<ChunkListIndex>();

        #endregion AStar
    }

    ChunkListIndex FindClosestPointUnder(Vector3 position)
    {
        ChunkListIndex closestPointCLI = triangles.CalculateChunkLeafIndex(position);

        List<NodeTri> leaf = nmdGen.Triangles.GetLeaf(closestPointCLI);

        float dist;
        float minDist = float.MaxValue;
        int minDistInd = -1;

        for(int i = 0; i < leaf.Count; i++)
        {
            closestPointCLI.ListIndex = i;
            dist = Vector3.Distance(position, triangles.GetElement(closestPointCLI).Center);
            if (dist < minDist)
            {
                minDist = dist;
                minDistInd = i;
            }
        }

        closestPointCLI.ListIndex = minDistInd;
        return closestPointCLI;
    }


    void AStar(ChunkListIndex startCLI, ChunkListIndex destinationCLI)
    {
        openList.Clear();
        closedList.Clear();
        checkedTris.Clear();
        checkedNodes.Clear();

        stopwatch.Start();

        int lowestFIndex;

        Vector3 destinationPos = triangles.GetElement(destinationCLI).Center;

        NodeAStar startNode = new NodeAStar(startCLI, triangles.GetElement(startCLI).Center, -1, 0, Vector3.Distance(triangles.GetElement(startCLI).Center, destinationPos));
        openList.Add(startNode);

        while (!isDestinationReached)
        {
            lowestFIndex = GetLowestFIndex();

            AddAdjacentNodes(lowestFIndex, destinationCLI, destinationPos);

            closedList.Add(openList[lowestFIndex]);
            openList.RemoveAt(lowestFIndex);

            if(stopwatch.ElapsedMilliseconds > 10000)
            {
                break;
            }
        }

        if(isDestinationReached) {
            GeneratePath();
        }

    }


    void AddAdjacentNodes(int index, ChunkListIndex destinationCLI, Vector3 destinationPos)
    {

        NodeAStar node = openList[index];
        NodeTri nodeTri = triangles.GetElement(node.cli);
        Vector3 neighborPos;

        foreach(ChunkListIndex neighborCLI in nodeTri.Neighbors)
        {
            neighborPos = triangles.GetElement(neighborCLI).Center;

            if (checkedTris.Contains(neighborCLI)) {
                continue;
            }
            else {
                checkedTris.Add(neighborCLI);
            }

            openList.Add(new NodeAStar(neighborCLI, neighborPos, index, node.G + Vector3.Distance(node.pos, neighborPos), Vector3.Distance(neighborPos, destinationPos)));

            if (neighborCLI.Equals(destinationCLI))
            {
                isDestinationReached = true;
                return;
            }
        }

    }


    int GetLowestFIndex()
    {
        int minInd = 0;
        float minVal = openList[0].F;
        
        for(int i = 1; i < openList.Count; i++) {

            if (openList[i].F < minVal) {

                minVal = openList[i].F;
                minInd = i;
            }
        }

        return minInd;
    }

    void GeneratePath()
    {
        path.Clear();

        Vector3 origin = closedList[0].pos;
        Vector3 direction;

        Vector3 yOffset = Vector3.up * (agentHeight / 2);
        float radius = agentWidth / 2;

        path.Add(origin);

        for (int i = 1; i < closedList.Count; i++)
        {
            direction = closedList[i].pos - origin;
            
            if (!Physics.SphereCast(origin + yOffset, radius, direction, out wayChecker, direction.magnitude, obstacleLayers)) {
               
                origin = closedList[i - 1].pos;
                path.Add(origin);
            }

        }

        path.Add(closedList[closedList.Count - 1].pos);

    }


}
