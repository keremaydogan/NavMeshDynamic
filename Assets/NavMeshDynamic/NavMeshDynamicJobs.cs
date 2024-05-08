using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;


namespace NavMeshDynamicJobs
{
    public static class NavMeshDynamicJobs
    {

        public struct ArrangeVerticesJob : IJobParallelFor
        {
            public NativeArray<Vector3> vertices;

            public Matrix4x4 localToWorldMatrix;

            public float vertexMergeThreshold;

            public void Execute(int index)
            {
                vertices[index] = RoundVector3XZ(localToWorldMatrix.MultiplyPoint3x4(vertices[index]), vertexMergeThreshold);
            }
        }


        // NOTE: I apply this very bad practice becasue if I do it as:
        // https://prnt.sc/RSEsx6oKzYRE
        // naturally I get:
        // https://prnt.sc/UT4Nzcbc4o5L
        public struct MarkInvalidTrianglesJobs : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Vector3> vertices;

            [NativeDisableParallelForRestriction]
            public NativeArray<int> triangles;

            public float maxSlopeAngel;

            public void Execute(int index)
            {
                int triInd = index * 3;
                if (CalculateNormal(vertices[triangles[triInd]], vertices[triangles[triInd + 1]], vertices[triangles[triInd + 2]]) > maxSlopeAngel)
                {
                    triangles[triInd] = -1;
                }

            }
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

        static float RoundToNearestFloat(float f, float roundTo)
        {
            return Mathf.Round(f / roundTo) * roundTo;
        }

    }
}


