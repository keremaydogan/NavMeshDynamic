using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct NodeTri
{
    
    ChunkListIndex v0, v1, v2;


    public NodeTri(ChunkListIndex v0, ChunkListIndex v1, ChunkListIndex v2)
    {
        this.v0 = v0;
        this.v1 = v1;
        this.v2 = v2;
    }

}
