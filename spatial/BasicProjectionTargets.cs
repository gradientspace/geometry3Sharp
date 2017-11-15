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

        public MeshProjectionTarget(DMesh3 mesh)
        {
            Mesh = mesh;
            Spatial = new DMeshAABBTree3(mesh, true);
        }

        public Vector3d Project(Vector3d vPoint, int identifier = -1)
        {
            int tNearestID = Spatial.FindNearestTriangle(vPoint);
            DistPoint3Triangle3 q = MeshQueries.TriangleDistance(Mesh, tNearestID, vPoint);
            return q.TriangleClosest;
        }

        /// <summary>
        /// Automatically construct fastest projection target for mesh
        /// </summary>
        public static MeshProjectionTarget Auto(DMesh3 mesh, bool bForceCopy = true)
        {
            if ( bForceCopy )
                return new MeshProjectionTarget(new DMesh3(mesh, false, MeshComponents.None));
            else
                return new MeshProjectionTarget(mesh);
        }
    }



    public class PlaneProjectionTarget : IProjectionTarget
    {
        public Vector3d Origin;
        public Vector3d Normal;

        public Vector3d Project(Vector3d vPoint, int identifier = -1)
        {
            Vector3d d = vPoint - Origin;
            return Origin + (d - d.Dot(Normal) * Normal);
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




    public class SequentialProjectionTarget : IProjectionTarget
    {
        public IProjectionTarget[] Targets { get; set; }

        public SequentialProjectionTarget() { }
        public SequentialProjectionTarget(params IProjectionTarget[] targets)
        {
            Targets = targets;
        }

        public Vector3d Project(Vector3d vPoint, int identifier = -1)
        {
            Vector3d vCur = vPoint;
            for ( int i = 0; i < Targets.Length; ++i ) {
                vCur = Targets[i].Project(vCur, identifier);
            }
            return vCur;
        }
    }

}
