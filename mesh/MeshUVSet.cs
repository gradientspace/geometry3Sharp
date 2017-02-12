using System;
using System.Collections.Generic;

namespace g3
{
    // [RMS] assumption is that TriangleUVs contains a UV-triangle for each base-mesh triangle...
    public class DenseMeshUVSet
    {
        public DVector<Vector2f> UVs;
        public DVector<Index3i> TriangleUVs;

        public DenseMeshUVSet()
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
