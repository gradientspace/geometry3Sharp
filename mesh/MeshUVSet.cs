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
        public DVector<Index3i> TriangleUVs;

        public IndexedUVMesh()
        {
            UVs = new DVector<Vector2f>();
            TriangleUVs = new DVector<Index3i>();
        }


        // initialize DenseUVMesh with a TriangleUVs attribute layer (which stores per-triangle-vertex UVs)
        public IndexedUVMesh(DMesh3 SourceMesh, TriUVsGeoAttribute UVLayer)
        {
            int NumTris = SourceMesh.MaxTriangleID;
            int NumElements = UVLayer.NumElements;
            Util.gDevAssert(NumTris == NumElements);

            UVs = new DVector<Vector2f>();
            TriangleUVs = new DVector<Index3i>();

            for ( int i = 0; i < NumElements; ++i ) {
                TriUVs tuv = UVLayer.GetValue(i);
                int k = UVs.size;
                UVs.Add(tuv.A); UVs.Add(tuv.B); UVs.Add(tuv.C);
                TriangleUVs.Add(new Index3i(k, k+1, k+2));
            }
        }

        public int AppendUV(Vector2f uv)
        {
            int id = UVs.Length;
            UVs.Add(uv);
            return id;
        }

    }
}
