using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class OcTreeNode<T>
{

}

public class OcTreeNodeParent<T> : OcTreeNode<T>
{
    public OcTreeNode<T>[,,] nodes;

    public OcTreeNodeParent(int depth, int maxDepth)
    {

        if(depth == maxDepth)
        {
            nodes = new OcTreeNodeLeaf<T>[2, 2, 2];
            return;
        }

        nodes = new OcTreeNodeParent<T>[2, 2, 2];

    }

}

public class OcTreeNodeLeaf<T> : OcTreeNode<T>
{
    public List<T> list;
}

public class OcTree<T>
{

    [Range(1, int.MaxValue)]
    public ushort maxDepth;
    public OcTreeNodeParent<T> root;

    public OcTree(ushort maxDepth)
    {
        this.maxDepth = maxDepth;
    }

    public List<T> GetList(BitArray keys)
    {
        int remainingDepth = maxDepth;
        int keysInd = 0;

        OcTreeNode<T> prevNode = root;
        OcTreeNode<T> currNode = root;
        
        while(remainingDepth-- > 0)
        {
            currNode = ((OcTreeNodeParent<T>)prevNode).nodes[Convert.ToInt16(keys[keysInd]), Convert.ToInt16(keys[keysInd]), Convert.ToInt16(keys[keysInd])];
            prevNode = currNode;
            keysInd += 3;
        }

        return ((OcTreeNodeLeaf<T>)currNode).list;

    }


}
