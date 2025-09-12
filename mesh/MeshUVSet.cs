using System;
using System.Collections.Generic;

namespace g3
{
    // 
    // Standalone UV indexed-triangle mesh
    //   (mainly we are using this as a UV layer for an existing 3D Mesh, so the assumption
    //    is that TriangleUVs has the same # of triangles as that mesh...)
    public class IndexedUVMesh
    {
        public DVector<Vector2f> UVs;
        public DVector<Index3i> UVTriangles;

        public IndexedUVMesh()
        {
            UVs = new DVector<Vector2f>();
            UVTriangles = new DVector<Index3i>();
        }

        // initialize DenseUVMesh with a TriangleUVs attribute layer (which stores per-triangle-vertex UVs)
        public static IndexedUVMesh FromPerVertexUVs(DMesh3 SourceMesh)
        {
            if (SourceMesh.HasVertexUVs == false)
                throw new Exception("SourceMesh has no Vertex UVs");

            int NumTris = SourceMesh.TriangleCount;
            int NumVerts = SourceMesh.MaxVertexID;

            IndexedUVMesh uvmesh = new IndexedUVMesh();
            uvmesh.UVs = new DVector<Vector2f>();
            uvmesh.UVTriangles = new DVector<Index3i>();

            for (int i = 0; i < NumVerts; ++i) {
                Vector2f uv = SourceMesh.IsVertex(i) ? SourceMesh.GetVertexUV(i) : Vector2f.Zero;
                uvmesh.UVs.Add(uv);
            }
            foreach(int tid in SourceMesh.TriangleIndices())
                uvmesh.UVTriangles.Add(SourceMesh.GetTriangle(tid));
            return uvmesh;
        }


        // initialize DenseUVMesh with a TriangleUVs attribute layer (which stores per-triangle-vertex UVs)
        public IndexedUVMesh(DMesh3 SourceMesh, TriUVsGeoAttribute UVLayer)
        {
            int NumTris = SourceMesh.MaxTriangleID;
            int NumElements = UVLayer.NumElements;
            Util.gDevAssert(NumTris == NumElements);

            UVs = new DVector<Vector2f>();
            UVTriangles = new DVector<Index3i>();

            for ( int i = 0; i < NumElements; ++i ) {
                TriUVs tuv = UVLayer.GetValue(i);
                int k = UVs.size;
                UVs.Add(tuv.A); UVs.Add(tuv.B); UVs.Add(tuv.C);
                UVTriangles.Add(new Index3i(k, k+1, k+2));
            }
        }

        public int AppendUV(Vector2f uv)
        {
            int id = UVs.Length;
            UVs.Add(uv);
            return id;
        }


        public DMesh3 ToDMesh3()
        {
            DMesh3 mesh = new DMesh3();
            for (int i = 0; i < UVs.Length; ++i)
                mesh.AppendVertex(new Vector3d(UVs[i].x, 0, UVs[i].y));
            for (int i = 0; i < UVTriangles.Length; ++i)
                mesh.AppendTriangle(UVTriangles[i]);
            return mesh;
        }

    }
}
