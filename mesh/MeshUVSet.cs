using System;
using System.Collections.Generic;

namespace g3
{
	// 
	// Standalone UV mesh
	//   (mainly we are using this as a UV layer for an existing 3D Mesh, so the assumption
	//    is that TriangleUVs has the same # of triangles as that mesh...)
    public class DenseUVMesh
    {
        public DVector<Vector2f> UVs;
        public DVector<Index3i> TriangleUVs;

        public DenseUVMesh()
        {
            UVs = new DVector<Vector2f>();
            TriangleUVs = new DVector<Index3i>();
        }

        public int AppendUV(Vector2f uv)
        {
            int id = UVs.Length;
            UVs.Add(uv);
            return id;
        }

    }
}
