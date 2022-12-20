using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// MeshProjectionTarget provides an IProjectionTarget interface to a mesh + spatial data structure.
    /// Use to project points to mesh surface.
    /// </summary>
    public class MeshProjectionTarget : IOrientedProjectionTarget
    {
        public DMesh3 Mesh { get; set; }
        public ISpatial Spatial { get; set; }

        public MeshProjectionTarget() { }
        public MeshProjectionTarget(DMesh3 mesh, ISpatial spatial)
        {
            Mesh = mesh;
            Spatial = spatial;
            if ( Spatial == null )
                Spatial = new DMeshAABBTree3(mesh, true);
        }

        public MeshProjectionTarget(DMesh3 mesh)
        {
            Mesh = mesh;
            Spatial = new DMeshAABBTree3(mesh, true);
        }

        public virtual Vector3d Project(Vector3d vPoint, int identifier = -1)
        {
            int tNearestID = Spatial.FindNearestTriangle(vPoint);
            Triangle3d triangle = new Triangle3d();
            Mesh.GetTriVertices(tNearestID, ref triangle.V0, ref triangle.V1, ref triangle.V2);
            Vector3d nearPt, bary;
            DistPoint3Triangle3.DistanceSqr(ref vPoint, ref triangle, out nearPt, out bary);
            return nearPt;
        }

        public virtual Vector3d Project(Vector3d vPoint, out Vector3d vProjectNormal, int identifier = -1)
        {
            int tNearestID = Spatial.FindNearestTriangle(vPoint);
            Triangle3d triangle = new Triangle3d();
            Mesh.GetTriVertices(tNearestID, ref triangle.V0, ref triangle.V1, ref triangle.V2);
            Vector3d nearPt, bary;
            DistPoint3Triangle3.DistanceSqr(ref vPoint, ref triangle, out nearPt, out bary);
            vProjectNormal = triangle.Normal;
            return nearPt;
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


        /// <summary>
        /// Automatically construct fastest projection target for region of mesh
        /// </summary>
        public static MeshProjectionTarget Auto(DMesh3 mesh, IEnumerable<int> triangles, int nExpandRings = 5)
        {
            MeshFaceSelection targetRegion = new MeshFaceSelection(mesh);
            targetRegion.Select(triangles);
            targetRegion.ExpandToOneRingNeighbours(nExpandRings);
            DSubmesh3 submesh = new DSubmesh3(mesh, targetRegion);
            return new MeshProjectionTarget(submesh.SubMesh); 
        }
    }




    /// <summary>
    /// Extension of MeshProjectionTarget that allows the target to have a transformation
    /// relative to it's internal space. Call SetTransform(), or initialize the transforms yourself
    /// </summary>
    public class TransformedMeshProjectionTarget : MeshProjectionTarget
    {
        public TransformSequence SourceToTargetXForm;
        public TransformSequence TargetToSourceXForm;

        public TransformedMeshProjectionTarget() { }
        public TransformedMeshProjectionTarget(DMesh3 mesh, ISpatial spatial) : base(mesh, spatial)
        {
        }
        public TransformedMeshProjectionTarget(DMesh3 mesh) : base(mesh)
        {
        }

        public void SetTransform(TransformSequence sourceToTargetX)
        {
            SourceToTargetXForm = sourceToTargetX;
            TargetToSourceXForm = SourceToTargetXForm.MakeInverse();
        }

        public override Vector3d Project(Vector3d vPoint, int identifier = -1)
        {
            Vector3d vTargetPt = SourceToTargetXForm.TransformP(vPoint);
            Vector3d vTargetProj = base.Project(vTargetPt, identifier);
            return TargetToSourceXForm.TransformP(vTargetProj);
        }


        public override Vector3d Project(Vector3d vPoint, out Vector3d vProjectNormal, int identifier = -1)
        {
            Vector3d vTargetPt = SourceToTargetXForm.TransformP(vPoint);
            Vector3d vTargetProjNormal;
            Vector3d vTargetProj = base.Project(vTargetPt, out vTargetProjNormal, identifier);
            vProjectNormal = TargetToSourceXForm.TransformV(vTargetProjNormal).Normalized;
            return TargetToSourceXForm.TransformP(vTargetProj);
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
