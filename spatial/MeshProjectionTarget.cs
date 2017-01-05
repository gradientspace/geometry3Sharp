using System;
using System.Collections.Generic;

namespace g3
{
    public class MeshProjectionTarget : IProjectionTarget
    {
        public DMesh3 Mesh { get; set; }
        public ISpatial Spatial { get; set; }

        public Vector3d Project(Vector3d vPoint, int identifier = -1)
        {
            int tNearestID = Spatial.FindNearestTriangle(vPoint);
            DistPoint3Triangle3 q = MeshQueries.TriangleDistance(Mesh, tNearestID, vPoint);
            return q.TriangleClosest;
        }
    }
}
