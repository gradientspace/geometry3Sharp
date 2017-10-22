using System;
using System.Collections.Generic;


namespace g3
{

    // [TODO] delete this file if nobody is using NormalOffset

    // collection of utility classes
    public static class SpatialFunctions
    {

        // various offset-surface functions, in class so the compute functions 
        // can be passed to other functions
        [System.Obsolete("NormalOffset is deprecated - is anybody using it? please lmk.")]
        public class NormalOffset
        {
            public DMesh3 Mesh;
            public ISpatial Spatial;

            public double Distance = 0.01;
            public bool UseFaceNormal = true;

            public Vector3d FindNearestAndOffset(Vector3d pos)
            {
                int tNearestID = Spatial.FindNearestTriangle(pos);
                DistPoint3Triangle3 q = MeshQueries.TriangleDistance(Mesh, tNearestID, pos);
                Vector3d vHitNormal = 
                    (UseFaceNormal == false && Mesh.HasVertexNormals) ?
                        Mesh.GetTriBaryNormal(tNearestID, q.TriangleBaryCoords.x, q.TriangleBaryCoords.y, q.TriangleBaryCoords.z) 
                        : Mesh.GetTriNormal(tNearestID);
                return q.TriangleClosest + Distance * vHitNormal;
            }

        }


    }



}
