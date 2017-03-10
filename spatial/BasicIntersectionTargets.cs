using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
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
}
