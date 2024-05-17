using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCIntelligence : MonoBehaviour
{

    NavMeshDynamicAgent nMDAgent;

    public Transform destination;

    public bool isPathExists;

    List<Vector3> path;


    private void Awake()
    {
        path = new List<Vector3>();
        nMDAgent = GetComponent<NavMeshDynamicAgent>();
    }


    private void Update()
    {
        CalculatePath();
    }

    void CalculatePath()
    {
        path.Clear();

        nMDAgent.SetDestination(destination.position);
        nMDAgent.CalculatePath();
        isPathExists = nMDAgent.IsDestinationReached;

        if (isPathExists)
        {
            path.AddRange(nMDAgent.Path);
        }
        else
        {
            path.Add(transform.position);
            path.Add(destination.position);
        }

    }


    private void OnDrawGizmos()
    {

        if (path == null || path.Count == 0) { return; }

        if (isPathExists)
        {
            Gizmos.color = Color.cyan;
        }
        else
        {
            Gizmos.color = Color.white;
        }

        for (int i = 1; i < path.Count; i++)
        {
            Gizmos.DrawLine(path[i - 1], path[i]);
        }

    }


}
