using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public static class MeshTransforms
    {

        public static void Translate(DMesh3 mesh, double tx, double ty, double tz)
        {
            foreach (int vid in mesh.VertexIndices()) {
                Vector3d v = mesh.GetVertex(vid);
                v.x += tx; v.y += ty; v.z += tz;
                mesh.SetVertex(vid, v);
            }
        }
        public static void Scale(DMesh3 mesh, double sx, double sy, double sz)
        {
            foreach (int vid in mesh.VertexIndices()) {
                Vector3d v = mesh.GetVertex(vid);
                v.x *= sx; v.y *= sy; v.z *= sz;
                mesh.SetVertex(vid, v);
            }
        }
        public static void Scale(DMesh3 mesh, double s)
        {
            Scale(mesh, s, s, s);
        }




        public static void FlipLeftRightCoordSystems(IDeformableMesh mesh)
        {
            int NV = mesh.MaxVertexID;
            for ( int i = 0; i < NV; ++i ) {
                if ( mesh.IsVertex(i) ) {
                    Vector3d v = mesh.GetVertex(i);
                    v.z = -v.z;
                    mesh.SetVertex(i, v);

                    if (mesh.HasVertexNormals) {
                        Vector3f n = mesh.GetVertexNormal(i);
                        n.z = -n.z;
                        mesh.SetVertexNormal(i, n);
                    }
                }
            }

            if ( mesh is DMesh3 ) {
                (mesh as DMesh3).ReverseOrientation(false);
            } else {
                throw new Exception("argh don't want this in IDeformableMesh...but then for SimpleMesh??");
            }

        }

    }
}
