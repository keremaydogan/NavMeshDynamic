using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class ChunkTracker
{

    int chunkSize;

    public int[] chunkReachWidths;

    // Holds only XZ plane
    HashSet<Vector2Int>[] areas;
    public HashSet<Vector2Int>[] Areas { get { return areas; } }

    Transform follow;

    [SerializeField]
    Vector3Int currChunk;
    public Vector3Int CurrChunk { get { return currChunk; } }
    Vector3Int currChunkPrev;

    bool isCurrChunkChangedXZ;
    public bool IsCurrChunkChangedXZ { get { return isCurrChunkChangedXZ; } }

    List<Vector2Int>[] areaNewElmBuffers;

    public void Initializer(int chunkSize, Transform follow)
    {

        this.chunkSize = chunkSize;
        this.follow = follow;

        UpdateCurrChunk();

        areas = new HashSet<Vector2Int>[chunkReachWidths.Length];
        areaNewElmBuffers = new List<Vector2Int>[chunkReachWidths.Length];

        for (int i = 0; i < chunkReachWidths.Length; i++)
        {
            areas[i] = new HashSet<Vector2Int>();
            areaNewElmBuffers[i] = new List<Vector2Int>();
        }

        UpdateChunkReachFull();

    }

    public void Runner()
    {
        UpdateCurrChunk();
        IsCurrChunkChangedXZRunner();

        if (isCurrChunkChangedXZ)
        {
            UpdateChunkReachFull();
        }
    }
    
    void UpdateCurrChunk()
    {
        currChunk.x = Mathf.FloorToInt(follow.position.x / chunkSize);
        currChunk.y = Mathf.FloorToInt(follow.position.y / chunkSize);
        currChunk.z = Mathf.FloorToInt(follow.position.z / chunkSize);
    }

    void IsCurrChunkChangedRunner()
    {
        if (currChunk != currChunkPrev)
        {
            currChunkPrev = currChunk;
            //isCurrChunkChanged = true;
        }
        //else if (isCurrChunkChangedXZ)
        //{
        //    //isCurrChunkChanged = false;
        //}
    }

    void IsCurrChunkChangedXZRunner()
    {
        if (currChunk.x != currChunkPrev.x || currChunk.z != currChunkPrev.z)
        {
            currChunkPrev = currChunk;
            isCurrChunkChangedXZ = true;
        }
        else if (isCurrChunkChangedXZ)
        {
            isCurrChunkChangedXZ = false;
        }
    }

    void UpdateChunkReachStep(Vector2Int dir)
    {
        int i, cummWidth, x, z;
        Vector2Int pos;

        if(dir.x != 0)
        {
            cummWidth = 0;
            for(i = 0; i < chunkReachWidths.Length; i++)
            {
                cummWidth += chunkReachWidths[i];
                x = (cummWidth + 1) * (int)Mathf.Sign(dir.x);
                for(z = currChunk.z - cummWidth; z <= currChunk.z + cummWidth; z++)
                {
                    pos = new Vector2Int(x, z);
                    areas[i].Add(pos);
                    if(i != chunkReachWidths.Length - 1) {
                        areas[i + 1].Remove(pos);
                    }
                }
            }
        }

        if (dir.y != 0)
        {
            cummWidth = 0;
            for (i = 0; i < chunkReachWidths.Length; i++)
            {
                cummWidth += chunkReachWidths[i];
                z = (cummWidth + 1) * (int)Mathf.Sign(dir.y);
                for (x = currChunk.x - cummWidth; x <= currChunk.x + cummWidth; x++)
                {
                    pos = new Vector2Int(x, z);
                    areas[i].Add(pos);
                    if (i != chunkReachWidths.Length - 1) {
                        areas[i + 1].Remove(pos);
                    }
                }
            }
        }
    }

    void UpdateChunkReachFull()
    {
        Vector2Int areaMin, areaMax, areaMinPrev = Vector2Int.one * int.MaxValue, areaMaxPrev = Vector2Int.one * int.MaxValue;
        int cummWidth = 0;
        Vector2Int pos;

        bool isUpperLayerContains;

        bool isAdded;

        for (int i = 0; i < chunkReachWidths.Length; i++)
        {
            cummWidth += chunkReachWidths[i];

            areaMin = new Vector2Int(currChunk.x - cummWidth, currChunk.z - cummWidth);
            areaMax = new Vector2Int(currChunk.x + cummWidth, currChunk.z + cummWidth);

            for (int z = areaMin.y; z <= areaMax.y; z++)
            {
                for (int x = areaMin.x; x <= areaMax.x; x++)
                {

                    if (z > areaMinPrev.y && z < areaMaxPrev.y && x == areaMinPrev.x)
                    {
                        x = areaMaxPrev.x + 1;
                    }

                    pos = new Vector2Int(x, z);

                    isUpperLayerContains = false;
                    for(int j = 0; j < i; j++)
                    {
                        if (areas[j].Contains(pos))
                        {
                            isUpperLayerContains = true;
                            break;
                        }
                    }

                    if (isUpperLayerContains)
                    {
                        continue;
                    }

                    for (int k = i+1; k < chunkReachWidths.Length; k++)
                    {
                        areas[k].Remove(pos);
                    }
                    isAdded = areas[i].Add(pos);


                    if(isAdded) // NOT SURE ABOUT THE BOOLEAN EQUATION
                    {
                        areaNewElmBuffers[i].Add(pos);
                    }

                }
            }

            areaMinPrev = areaMin;
            areaMaxPrev = areaMax;

        }

    }

    public void DumpAreaBuffers(ref List<Vector2Int>[] dumpBuffers, bool initial = false)
    {
        for (int level = 0; level < areaNewElmBuffers.Length; level++)
        {

            if (initial && level != 0)
            {
                areaNewElmBuffers[level].AddRange(areaNewElmBuffers[level - 1]);
            }

            dumpBuffers[level].AddRange(areaNewElmBuffers[level]);

        }

        for(int i = 0; i < areaNewElmBuffers.Length; i++)
        {
            areaNewElmBuffers[i].Clear();
        }

    }

    public List<Vector2Int>[] ChunkReachAreasArrOfLists()
    {

        List<Vector2Int>[] r = new List<Vector2Int>[chunkReachWidths.Length];

        try
        {
            for (int i = 0; i < this.areas.Length; i++)
            {
                r[i] = new List<Vector2Int>(areas[i]);
            }

        }
        catch (NullReferenceException)
        {
            for (int i = 0; i < r.Length; i++)
            {
                r[i] = new List<Vector2Int>();
            }
        }

        return r;

    }

}
