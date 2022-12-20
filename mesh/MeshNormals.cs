using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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


        public Vector3d this[int vid] {
            get { return Normals[vid]; }
        }


        public void CopyTo(DMesh3 SetMesh)
        {
            if (SetMesh.MaxVertexID < Mesh.MaxVertexID)
                throw new Exception("MeshNormals.Set: SetMesh does not have enough vertices!");
            if (!SetMesh.HasVertexNormals)
                SetMesh.EnableVertexNormals(Vector3f.AxisY);
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

            SpinLock Normals_lock = new SpinLock();

            gParallel.ForEach(Mesh.TriangleIndices(), (ti) => {
                Index3i tri = Mesh.GetTriangle(ti);
                Vector3d va = Mesh.GetVertex(tri.a);
                Vector3d vb = Mesh.GetVertex(tri.b);
                Vector3d vc = Mesh.GetVertex(tri.c);
                Vector3d N = MathUtil.Normal(ref va, ref vb, ref vc);
                double a = MathUtil.Area(ref va, ref vb, ref vc);
                bool taken = false;
                Normals_lock.Enter(ref taken);
                Normals[tri.a] += a * N;
                Normals[tri.b] += a * N;
                Normals[tri.c] += a * N;
                Normals_lock.Exit();
            });

            gParallel.BlockStartEnd(0, NV - 1, (vi_start, vi_end) => {
                for (int vi = vi_start; vi <= vi_end; vi++) {
                    if (Normals[vi].LengthSquared > MathUtil.ZeroTolerancef)
                        Normals[vi] = Normals[vi].Normalized;
                }
            });
        }




        public static void QuickCompute(DMesh3 mesh)
        {
            MeshNormals normals = new MeshNormals(mesh);
            normals.Compute();
            normals.CopyTo(mesh);
        }


        public static Vector3d QuickCompute(DMesh3 mesh, int vid, NormalsTypes type = NormalsTypes.Vertex_OneRingFaceAverage_AreaWeighted)
        {
            Vector3d sum = Vector3d.Zero;
            Vector3d n, c; double a;
            foreach ( int tid in mesh.VtxTrianglesItr(vid)) {
                mesh.GetTriInfo(tid, out n, out a, out c);
                sum += a * n;
            }
            return sum.Normalized;
        }


    }
}
