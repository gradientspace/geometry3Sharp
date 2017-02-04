using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class MeshProjectionTarget : IProjectionTarget
    {
        public DMesh3 Mesh { get; set; }
        public ISpatial Spatial { get; set; }

        public MeshProjectionTarget() { }
        public MeshProjectionTarget(DMesh3 mesh, ISpatial spatial)
        {
            Mesh = mesh;
            Spatial = spatial;
        }

        public Vector3d Project(Vector3d vPoint, int identifier = -1)
        {
            int tNearestID = Spatial.FindNearestTriangle(vPoint);
            DistPoint3Triangle3 q = MeshQueries.TriangleDistance(Mesh, tNearestID, vPoint);
            return q.TriangleClosest;
        }
    }



    public class CircleProjectionTarget : IProjectionTarget
    {
        public Circle3d Circle;

        public Vector3d Project(Vector3d vPoint, int identifier = -1)
        {
            DistPoint3Circle3 d = new DistPoint3Circle3(vPoint, Circle);
            d.GetSquared();
            return d.CircleClosest;
        }
    }



    public class CylinderProjectionTarget : IProjectionTarget
    {
        public Cylinder3d Cylinder;

        public Vector3d Project(Vector3d vPoint, int identifer = -1)
        {
            DistPoint3Cylinder3 d = new DistPoint3Cylinder3(vPoint, Cylinder);
            d.GetSquared();
            return d.CylinderClosest;
        }
    }

}
