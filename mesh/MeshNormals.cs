using System;
using System.Collections.Generic;
using System.Linq;

namespace g3
{
    public class MeshNormals
    {
        public IMesh Mesh;
        public DVector<Vector3d> Normals;

        /// <summary>
        /// By default this is Mesh.GetVertex(). Can override to provide
        /// alternate vertex source.
        /// </summary>
        public Func<int, Vector3d> VertexF;



        public enum NormalsTypes
        {
            Vertex_OneRingFaceAverage_AreaWeighted
        }
        public NormalsTypes NormalType;


        public MeshNormals(IMesh mesh, NormalsTypes eType = NormalsTypes.Vertex_OneRingFaceAverage_AreaWeighted)
        {
            Mesh = mesh;
            NormalType = eType;
            Normals = new DVector<Vector3d>();
            VertexF = Mesh.GetVertex;
        }


        public void Compute()
        {
            Compute_FaceAvg_AreaWeighted();
        }


        public void CopyTo(DMesh3 SetMesh)
        {
            if (SetMesh.MaxVertexID < Mesh.MaxVertexID)
                throw new Exception("MeshNormals.Set: SetMesh does not have enough vertices!");
            int NV = Mesh.MaxVertexID;
            for ( int vi = 0; vi < NV; ++vi ) {
                if ( Mesh.IsVertex(vi) && SetMesh.IsVertex(vi) ) {
                    SetMesh.SetVertexNormal(vi, (Vector3f)Normals[vi]);
                }
            }
        }




        // TODO: parallel version, cache tri normals
        void Compute_FaceAvg_AreaWeighted()
        {
            int NV = Mesh.MaxVertexID;
            if ( NV != Normals.size ) 
                Normals.resize(NV);
            for (int i = 0; i < NV; ++i)
                Normals[i] = Vector3d.Zero;

            int NT = Mesh.MaxTriangleID;
            for (int ti = 0; ti < NT; ++ti) {
                if (Mesh.IsTriangle(ti) == false)
                    continue;
                Index3i tri = Mesh.GetTriangle(ti);
                Vector3d va = Mesh.GetVertex(tri.a);
                Vector3d vb = Mesh.GetVertex(tri.b);
                Vector3d vc = Mesh.GetVertex(tri.c);
                Vector3d N = MathUtil.Normal(va, vb, vc);
                double a = MathUtil.Area(va, vb, vc);
                Normals[tri.a] += a * N;
                Normals[tri.b] += a * N;
                Normals[tri.c] += a * N;
            }

            for ( int vi = 0; vi < NV; ++vi) {
                if (Normals[vi].LengthSquared > MathUtil.ZeroTolerancef)
                    Normals[vi].Normalize();
            }

        }



    }
}
