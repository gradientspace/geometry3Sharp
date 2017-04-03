using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public static class MeshTransforms
    {

        public static void Translate(IDeformableMesh mesh, double tx, double ty, double tz)
        {
            int NV = mesh.MaxVertexID;
            for ( int vid = 0; vid < NV; ++vid ) {
                if (mesh.IsVertex(vid)) {
                    Vector3d v = mesh.GetVertex(vid);
                    v.x += tx; v.y += ty; v.z += tz;
                    mesh.SetVertex(vid, v);
                }
            }
        }


        public static Vector3d Rotate(Vector3d pos, Vector3d origin, Quaternionf rotation)
        {
            Vector3d v = pos - origin;
            v = (Vector3d)(rotation * (Vector3f)v);
            v += origin;
            return v;
        }
        public static Frame3f Rotate(Frame3f f, Vector3d origin, Quaternionf rotation)
        {
            f.Origin -= (Vector3f)origin;
            f.Rotate(rotation);
            f.Origin += (Vector3f)origin;
            return f;
        }
        public static void Rotate(IDeformableMesh mesh, Vector3d origin, Quaternionf rotation)
        {
            int NV = mesh.MaxVertexID;
            for ( int vid = 0; vid < NV; ++vid ) {
                if (mesh.IsVertex(vid)) {
                    Vector3d v = mesh.GetVertex(vid);
                    v -= origin;
                    v = (Vector3d)(rotation * (Vector3f)v);
                    v += origin;
                    mesh.SetVertex(vid, v);
                }
            }
        }

        public static void Scale(IDeformableMesh mesh, double sx, double sy, double sz)
        {
            int NV = mesh.MaxVertexID;
            for ( int vid = 0; vid < NV; ++vid ) {
                if (mesh.IsVertex(vid)) {
                    Vector3d v = mesh.GetVertex(vid);
                    v.x *= sx; v.y *= sy; v.z *= sz;
                    mesh.SetVertex(vid, v);
                }
            }
        }
        public static void Scale(IDeformableMesh mesh, double s)
        {
            Scale(mesh, s, s, s);
        }


        public static void ToFrame(IDeformableMesh mesh, Frame3f f)
        {
            int NV = mesh.MaxVertexID;
            for ( int vid = 0; vid < NV; ++vid ) {
                if (mesh.IsVertex(vid)) {
                    Vector3d v = mesh.GetVertex(vid);
                    Vector3d vf = f.ToFrameP((Vector3f)v);
                    mesh.SetVertex(vid, vf);
                }
            }
        }
        public static void FromFrame(IDeformableMesh mesh, Frame3f f)
        {
            int NV = mesh.MaxVertexID;
            for ( int vid = 0; vid < NV; ++vid ) {
                if (mesh.IsVertex(vid)) {
                    Vector3d vf = mesh.GetVertex(vid);
                    Vector3d v = f.FromFrameP((Vector3f)vf);
                    mesh.SetVertex(vid, v);
                }
            }
        }


        public static Vector3d ConvertZUpToYUp(Vector3d v)
        {
            return new Vector3d(v.x, v.z, -v.y);
        }
        public static Vector3f ConvertZUpToYUp(Vector3f v)
        {
            return new Vector3f(v.x, v.z, -v.y);
        }
        public static Frame3f ConvertZUpToYUp(Frame3f f)
        {
            return new Frame3f(
                ConvertZUpToYUp(f.Origin),
                ConvertZUpToYUp(f.X),
                ConvertZUpToYUp(f.Y),
                ConvertZUpToYUp(f.Z));
        }
        public static void ConvertZUpToYUp(IDeformableMesh mesh)
        {
            int NV = mesh.MaxVertexID;
            for ( int vid = 0; vid < NV; ++vid ) {
                if ( mesh.IsVertex(vid) ) {
                    Vector3d v = mesh.GetVertex(vid);
                    mesh.SetVertex(vid, new Vector3d(v.x, v.z, -v.y));
                }
            }
        }

        public static Vector3d ConvertYUpToZUp(Vector3d v)
        {
            return new Vector3d(v.x, -v.z, v.y);
        }
        public static Vector3f ConvertYUpToZUp(Vector3f v)
        {
            return new Vector3f(v.x, -v.z, v.y);
        }
        public static Frame3f ConvertYUpToZUp(Frame3f f)
        {
            return new Frame3f(
                ConvertYUpToZUp(f.Origin),
                ConvertYUpToZUp(f.X),
                ConvertYUpToZUp(f.Y),
                ConvertYUpToZUp(f.Z));
        }
        public static void ConvertYUpToZUp(IDeformableMesh mesh)
        {
            int NV = mesh.MaxVertexID;
            for ( int vid = 0; vid < NV; ++vid ) {
                if ( mesh.IsVertex(vid) ) {
                    Vector3d v = mesh.GetVertex(vid);
                    mesh.SetVertex(vid, new Vector3d(v.x, -v.z, v.y));
                }
            }
        }


        public static Vector3d FlipLeftRightCoordSystems(Vector3d v)
        {
            return new Vector3d(v.x, v.y, -v.z);
        }
        public static Vector3f FlipLeftRightCoordSystems(Vector3f v)
        {
            return new Vector3f(v.x, v.y, -v.z);
        }
        public static Frame3f FlipLeftRightCoordSystems(Frame3f f)
        {
            throw new NotImplementedException("this doesn't work...frame becomes broken somehow?");
            //return new Frame3f(
            //    FlipLeftRightCoordSystems(f.Origin),
            //    f.X, f.Y, f.Z);
            //    //FlipLeftRightCoordSystems(f.X),
            //    //FlipLeftRightCoordSystems(f.Y),
            //    //FlipLeftRightCoordSystems(f.Z));
        }
        public static void FlipLeftRightCoordSystems(IDeformableMesh mesh)
        {
            int NV = mesh.MaxVertexID;
            for ( int vid = 0; vid < NV; ++vid ) {
                if ( mesh.IsVertex(vid) ) {
                    Vector3d v = mesh.GetVertex(vid);
                    v.z = -v.z;
                    mesh.SetVertex(vid, v);

                    if (mesh.HasVertexNormals) {
                        Vector3f n = mesh.GetVertexNormal(vid);
                        n.z = -n.z;
                        mesh.SetVertexNormal(vid, n);
                    }
                }
            }

            if ( mesh is DMesh3 ) {
                (mesh as DMesh3).ReverseOrientation(false);
            } else {
                throw new Exception("argh don't want this in IDeformableMesh...but then for SimpleMesh??");
            }

        }



        public static void PerVertexTransform(IDeformableMesh mesh, Func<Vector3d,Vector3d> TransformF )
        {
            int NV = mesh.MaxVertexID;
            for (int vid = 0; vid < NV; ++vid) {
                if (mesh.IsVertex(vid)) {
                    Vector3d newPos = TransformF(mesh.GetVertex(vid));
                    mesh.SetVertex(vid, newPos);
                }
            }
        }


    }
}
