using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CastTest : MonoBehaviour
{
    public Transform target;

    public bool raycastTrigger;
    public bool sphereCastTrigger;

    Vector3 dif;

    public LayerMask layerMask;

    private void Update()
    {
        
        if(raycastTrigger)
        {
            Debug.Log("RAYCAST TEST: " + DoRaycast());
            raycastTrigger = false;
        }

        if (sphereCastTrigger)
        {
            Debug.Log("SPHERE TEST: " + DoSphereCast());
            sphereCastTrigger = false;
        }

    }

    bool DoRaycast()
    {
        dif = target.position - transform.position;
        return Physics.Raycast(transform.position, dif, dif.magnitude, layerMask);
    }

    bool DoSphereCast()
    {
        dif = target.position - transform.position;
        return Physics.SphereCast(new Ray(transform.position, dif), 1f, dif.magnitude, layerMask);
    }


    private void OnDrawGizmos()
    {
        if(target == null)
        {
            return;
        }
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        Gizmos.DrawLine(transform.position, target.position);

    }

}
