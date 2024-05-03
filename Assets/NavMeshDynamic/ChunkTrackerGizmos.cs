using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class ChunkTrackerGizmos : MonoBehaviour
{
    ChunkTracker chunkTracker;
    int chunkSize;
    Vector3 chunkCenterOffset;

    List<Vector2Int>[] chunkReachAreas = null;

    public Color[] areaColors;

    private void Awake()
    {
        chunkTracker = GetComponent<NavMeshDynamic>().chunkTracker;
        chunkSize = GetComponent<NavMeshDynamic>().chunkSize;
        chunkCenterOffset = new Vector3(chunkSize/2, 0, chunkSize/2);
        chunkReachAreas = new List<Vector2Int>[0];
    }

    private void Start()
    {
        chunkReachAreas = chunkTracker.ChunkReachAreasArrOfLists();
    }

    private void Update()
    {
        if (chunkTracker.IsCurrChunkChangedXZ) {
            chunkReachAreas = chunkTracker.ChunkReachAreasArrOfLists();
        
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        
        if (chunkTracker != null && chunkReachAreas != null)
        {
            Vector2Int curr;

            for(int i = 0; i < chunkReachAreas.Length; i++)
            {

                if (areaColors.Length == 0)
                {
                    Gizmos.color = Color.red;
                }
                else
                {
                    Gizmos.color = areaColors[math.clamp(i, 0, areaColors.Length-1)];
                }

                for(int j = 0; j < chunkReachAreas[i].Count; j++)
                {
                    
                    curr = chunkReachAreas[i][j];
                    Gizmos.DrawSphere(new Vector3(curr.x, 0, curr.y) * chunkSize + Vector3.up * 200 + chunkCenterOffset, chunkSize/8);

                }
            }
        }
    }


}
