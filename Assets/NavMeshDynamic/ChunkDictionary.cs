using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

using GeneralHelper;


public class ChunkDictionary<K,V>
{

    ushort chunkSize;

    Dictionary<Vector3Int, Dictionary<K,V>> chunks;

    Dictionary<Vector2Int, List<int>> chunksGroupedByXZ;

    public Dictionary<Vector2Int, List<int>> ChunksGroupedByXZ { get { return chunksGroupedByXZ; } }


    public ChunkDictionary(ushort chunkSize = 64)
    {
        this.chunkSize = chunkSize;

        chunks = new Dictionary<Vector3Int, Dictionary<K, V>>();

        chunksGroupedByXZ = new Dictionary<Vector2Int, List<int>>();
    }


    public Vector3Int AddElement(K key, V value, Vector3 position)
    {
        return AddElement(GetChunkIndex(position), key, value);
    }

    
    public Vector3Int AddElement(Vector3Int chunkIndex, K key, V value)
    {

        try
        {
            chunks[chunkIndex].Add(key, value);
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

            chunks[chunkIndex] = new Dictionary<K, V> { { key, value } };
        }

        return chunkIndex;


    }

    public bool ContainsKey(Vector3Int chunkIndex, K key)
    {
        try
        {
            return chunks[chunkIndex].ContainsKey(key);
        }
        catch(KeyNotFoundException)
        {
            return false;
        }
        
    }

    public void RemoveKey(Vector3Int chunkIndex, K key)
    {
        try
        {
            chunks[chunkIndex].Remove(key);
        }
        catch (KeyNotFoundException)
        {
            return;
        }

    }

    public V GetElement(Vector3Int chunkIndex, K key)
    {
        try
        {
            return chunks[chunkIndex][key];
        }
        catch(KeyNotFoundException)
        {
            throw new KeyNotFoundException();
        }
    }

    public void RemoveElement(Vector3Int chunkIndex, K key)
    {
        chunks[chunkIndex].Remove(key);
    }

    public void RemoveChunk(Vector3Int chunkIndex)
    {
        chunks[chunkIndex] = null;
        chunks.Remove(chunkIndex);
    }


    public bool IsChunkExists(Vector3Int chunkIndex)
    {
        return chunks.ContainsKey(chunkIndex);
    }


    public List<Vector3Int> GetAllChunkIndices()
    {
        return chunks.Keys.ToList();
    }


    public List<K> GetAllKeysFromChunk(Vector3Int chunkIndex)
    {
        return chunks[chunkIndex].Keys.ToList();
    }


    Vector3Int GetChunkIndex(Vector3 position)
    {
        return new Vector3Int(Mathf.FloorToInt(position.x / chunkSize), Mathf.FloorToInt(position.y / chunkSize), Mathf.FloorToInt(position.z / chunkSize));
    }


    Vector3Int GetChunkIndex(Vector3 position, out Vector3 inChunkPos)
    {
        Vector3Int chunkIndex = new Vector3Int(Mathf.FloorToInt(position.x / chunkSize), Mathf.FloorToInt(position.y / chunkSize), Mathf.FloorToInt(position.z / chunkSize));
        inChunkPos = position - (chunkSize * chunkIndex);
        return chunkIndex;
    }


}
