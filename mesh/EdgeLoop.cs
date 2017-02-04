using System;
using System.Collections.Generic;

namespace g3
{
    public class EdgeLoop
    {
        public DMesh3 Mesh;
        public EdgeLoop(DMesh3 mesh)
        {
            Mesh = mesh;
        }

        public int[] Vertices;
        public int[] Edges;

        public int[] BowtieVertices;



        public AxisAlignedBox3d GetBounds()
        {
            AxisAlignedBox3d box = AxisAlignedBox3d.Empty;
            for (int i = 0; i < Vertices.Length; ++i)
                box.Contain(Mesh.GetVertex(i));
            return box;
        }

    }
}
