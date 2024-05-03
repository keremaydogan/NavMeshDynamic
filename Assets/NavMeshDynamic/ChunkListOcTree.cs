using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

using GeneralHelper;


public struct ChunkListIndex
{

    //  LEAF NUMBERS EXAMPLE (3 depth in 2D space):
    //  CHUNK:
    //  [ 0 ]  [ 1 ]  [ 2 ]  [ 3 ] 
    //  [ 4 ]  [ 5 ]  [ 6 ]  [ 7 ] 
    //  [ 8 ]  [ 9 ]  [ 10 ] [ 11 ] 
    //  [ 12 ] [ 13 ] [ 14 ] [ 15 ] 


    Vector3Int chunkIndex;
    public Vector3Int ChunkIndex { get { return chunkIndex; } set { chunkIndex = value; } }

    int leafIndex;
    public int LeafIndex { get { return leafIndex; } set { leafIndex = value; } }

    int listIndex;
    public int ListIndex { get { return listIndex; } set { listIndex = value; } }

    public ChunkListIndex(Vector3Int chunkIndex, int leafIndex, int listIndex)
    {
        this.chunkIndex = chunkIndex;
        this.leafIndex = leafIndex;
        this.listIndex = listIndex;
    }

    public override string ToString()
    {
        return "ChunkIndex: " + chunkIndex + " LeafIndex: " + leafIndex + " ListIndex: " + listIndex;
    }

}


public class ChunkListOcTree<T>
{

    public delegate ChunkListIndex AddElementGeneral(T element, Vector3 position);

    ushort chunkSize;

    ushort depth;

    int dimension;

    int dimensionSqr;

    public int LeafCount { get { return dimension * dimensionSqr; } }

    Dictionary<Vector3Int, List<T>[]> chunks;

    public AddElementGeneral AddElement;

    Dictionary<Vector2Int, List<int>> chunksGroupedByXZ;

    Vector3Int leafIndexV3;
    Vector3Int newLeafIndV3;
    Vector3Int newChunkIndex;

    public Dictionary<Vector2Int, List<int>> ChunksGroupedByXZ { get { return chunksGroupedByXZ; } }


    public ChunkListOcTree(ushort chunkSize = 64, ushort maxDepth = 1)
    {
        this.chunkSize = chunkSize;

        this.depth = maxDepth;

        chunks = new Dictionary<Vector3Int, List<T>[]>();

        chunksGroupedByXZ = new Dictionary<Vector2Int, List<int>>();

        if(maxDepth == 0)
        {
            dimension = 1;

            AddElement = AddElementNoDepth;
        }
        else
        {
            dimension = 1 << maxDepth;

            AddElement = AddElementWithDepth;
        }

        dimensionSqr = dimension * dimension;

    }


    ChunkListIndex AddElementWithDepth(T element, Vector3 position)
    {
        Vector3Int chunkIndex;
        int listIndex;
        Vector3 posInChunk;
        Vector3Int leafIndexV3 = Vector3Int.zero;
        int leafIndex;

        chunkIndex = GetChunkIndex(position, out posInChunk);

        int chunkSizeVar = chunkSize;
        int dimOffset = dimension;

        for (int i = 0; i < depth; i++)
        {
            chunkSizeVar = chunkSizeVar >> 1;
            dimOffset = dimOffset >> 1;

            if (posInChunk.x >= chunkSizeVar)
            {
                posInChunk.x -= chunkSizeVar;
                leafIndexV3.x += dimOffset;
            }

            if (posInChunk.y >= chunkSizeVar)
            {
                posInChunk.y -= chunkSizeVar;
                leafIndexV3.y += dimOffset;
            }

            if (posInChunk.z >= chunkSizeVar)
            {
                posInChunk.z -= chunkSizeVar;
                leafIndexV3.z += dimOffset;
            }

        }

        leafIndex = leafIndexV3.x * dimensionSqr + leafIndexV3.y * dimension + leafIndexV3.z;

        try
        {
            chunks[chunkIndex][leafIndex].Add(element);
        }
        catch (KeyNotFoundException)
        {
            chunks[chunkIndex] = new List<T>[dimensionSqr * dimension];
            chunks[chunkIndex][leafIndex] = new List<T>() { element };

            Vector2Int chunkIndexXZ = VectorTools.v3ToXZ(chunkIndex);
            try
            {
                chunksGroupedByXZ[chunkIndexXZ].Add(chunkIndex.y);
            }
            catch (KeyNotFoundException)
            {
                chunksGroupedByXZ[chunkIndexXZ] = new List<int> { chunkIndex.y };
            }
        }
        catch (NullReferenceException)
        {
            chunks[chunkIndex][leafIndex] = new List<T>() { element };
        }

        listIndex = chunks[chunkIndex][leafIndex].Count - 1;

        return new ChunkListIndex(chunkIndex, leafIndex, listIndex);

    }


    ChunkListIndex AddElementNoDepth(T element, Vector3 position)
    {

        Vector3Int chunkIndex = GetChunkIndex(position);
        int listIndex;

        try
        {
            chunks[chunkIndex][0].Add(element);
        }
        catch (KeyNotFoundException)
        {

            Vector2Int chunkIndexXZ = VectorTools.v3ToXZ(chunkIndex);
            try
            {
                chunksGroupedByXZ[chunkIndexXZ].Add(chunkIndex.y);
            }
            catch (KeyNotFoundException)
            {
                chunksGroupedByXZ[chunkIndexXZ] = new List<int> { chunkIndex.y };
            }

            chunks[chunkIndex] = new List<T>[1];
            chunks[chunkIndex][0] = new List<T>() { element };
        }

        

        listIndex = chunks[chunkIndex][0].Count - 1;

        return new ChunkListIndex(chunkIndex, 0, listIndex);

    }


    public T GetElement(ChunkListIndex index)
    {
        try
        {
            return chunks[index.ChunkIndex][index.LeafIndex][index.ListIndex];
        }
        catch (ArgumentOutOfRangeException)
        {
            Debug.Log("CHUNK EXISTS: " + chunks.ContainsKey(index.ChunkIndex));
            Debug.Log("LEAF EXISTS: " + (chunks[index.ChunkIndex][index.LeafIndex] != null));
            Debug.Log("LIST ITEM EXISTS: " + (index.ListIndex < chunks[index.ChunkIndex][index.LeafIndex].Count) + " -> " + index.ListIndex + " < " + chunks[index.ChunkIndex][index.LeafIndex].Count);
        }
        return chunks[index.ChunkIndex][index.LeafIndex][index.ListIndex];
    }


    public List<T> GetLeaf(ChunkListIndex index)
    {
        return chunks[index.ChunkIndex][index.LeafIndex];
    }

    public void GetLeaf(ChunkListIndex index, ref List<T> refList)
    {
        refList.Clear();
        refList.AddRange(chunks[index.ChunkIndex][index.LeafIndex]);
    }


    public List<T>[] GetChunk(ChunkListIndex index)
    {
        return chunks[index.ChunkIndex];
    }


    public void RemoveChunk(Vector3Int chunkIndex)
    {
        chunks[chunkIndex] = null;
        chunks.Remove(chunkIndex);
    }


    public void RemoveFromLeaf(ChunkListIndex chunkListInd, T elm)
    {
        try
        {
            chunks[chunkListInd.ChunkIndex][chunkListInd.LeafIndex].Remove(elm);
        }
        catch(KeyNotFoundException) { return; }
    }


    public List<Vector3Int> GetAllChunkIndices()
    {
        return chunks.Keys.ToList();
    }


    public List<T> GetChunkUnified(Vector3Int chunkIndex)
    {
        List<T> unified = new List<T>();

        for(int i = 0; i < dimensionSqr * dimension; i++) {
            if (chunks[chunkIndex][i] != null)
            {
                unified.AddRange(chunks[chunkIndex][i]);
            }
        }

        return unified;

    }

    public void GetChunkUnified(Vector3Int chunkIndex, ref List<T> refList)
    {
        refList.Clear();

        for (int i = 0; i < dimensionSqr * dimension; i++)
        {
            if (chunks[chunkIndex][i] != null)
            {
                refList.AddRange(chunks[chunkIndex][i]);
            }
        }

    }


    public int LeafIndV3ToLeafInd(Vector3Int leafIndexV3)
    {
        return leafIndexV3.x * dimensionSqr + leafIndexV3.y * dimension + leafIndexV3.z;
    }


    public Vector3Int LeafIndToLeafIndV3(int leafIndex)
    {
        int x, y, z;
        x = Math.DivRem(leafIndex, dimensionSqr, out y);
        y = Math.DivRem(y, dimension, out z);
        return new Vector3Int(x, y, z);
    }


    Vector3Int GetChunkIndex(Vector3 position)
    {
        return new Vector3Int(Mathf.FloorToInt(position.x / chunkSize), Mathf.FloorToInt(position.y / chunkSize), Mathf.FloorToInt(position.z / chunkSize));
    }


    Vector3Int GetChunkIndex(Vector3 position, out Vector3 remPos)
    {
        Vector3Int chunkIndex = new Vector3Int(Mathf.FloorToInt(position.x / chunkSize), Mathf.FloorToInt(position.y / chunkSize), Mathf.FloorToInt(position.z / chunkSize));
        remPos = position - (chunkSize * chunkIndex);
        return chunkIndex;
    }


}
