using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{

    public class TransformedIntersectionTarget : IIntersectionTarget
    {
        DMeshIntersectionTarget BaseTarget = null;

        public Func<Ray3d, Ray3d> MapToBaseF = null;
        public Func<Vector3d, Vector3d> MapFromBasePosF = null;
        public Func<Vector3d, Vector3d> MapFromBaseNormalF = null;


        public bool HasNormal { get { return BaseTarget.HasNormal; } }
        public bool RayIntersect(Ray3d ray, out Vector3d vHit, out Vector3d vHitNormal)
        {
            Ray3d baseRay = MapToBaseF(ray);
            if ( BaseTarget.RayIntersect(baseRay, out vHit, out vHitNormal) ) {
                vHit = MapFromBasePosF(vHit);
                vHitNormal = MapFromBasePosF(vHitNormal);
                return true;
            }
            return false;
        }
    }





    public class DMeshIntersectionTarget : IIntersectionTarget
    {
        public DMesh3 Mesh { get; set; }
        public ISpatial Spatial { get; set; }
        public bool UseFaceNormal = true;

        public DMeshIntersectionTarget() { }
        public DMeshIntersectionTarget(DMesh3 mesh, ISpatial spatial)
        {
            Mesh = mesh;
            Spatial = spatial;
        }


        public bool HasNormal { get { return true; } }
        public bool RayIntersect(Ray3d ray, out Vector3d vHit, out Vector3d vHitNormal)
        {
            vHit = Vector3d.Zero;
            vHitNormal = Vector3d.AxisX;
            int tHitID = Spatial.FindNearestHitTriangle(ray);
            if (tHitID == DMesh3.InvalidID)
                return false;
            IntrRay3Triangle3 t = MeshQueries.TriangleIntersection(Mesh, tHitID, ray);
            vHit = ray.PointAt(t.RayParameter);
            if ( UseFaceNormal == false && Mesh.HasVertexNormals)
                vHitNormal = Mesh.GetTriBaryNormal(tHitID, t.TriangleBaryCoords.x, t.TriangleBaryCoords.y, t.TriangleBaryCoords.z);
            else
                vHitNormal = Mesh.GetTriNormal(tHitID);
            return true;
        }
    }



    /// <summary>
    /// Compute ray-intersection with plane
    /// </summary>
    public class PlaneIntersectionTarget : IIntersectionTarget
    {
        public Frame3f PlaneFrame;
        public int NormalAxis = 2;

        public bool HasNormal { get { return true; } }
        public bool RayIntersect(Ray3d ray, out Vector3d vHit, out Vector3d vHitNormal)
        {
            Vector3f rayHit = PlaneFrame.RayPlaneIntersection((Vector3f)ray.Origin, (Vector3f)ray.Direction, NormalAxis);
            vHit = rayHit;
            vHitNormal = Vector3f.AxisY;
            return (rayHit != Vector3f.Invalid);
        }
    }



}
