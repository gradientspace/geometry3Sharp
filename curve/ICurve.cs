using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public interface ICurve
    {
        int VertexCount { get; }
        bool Closed { get; }

        Vector3d GetVertex(int i);

        IEnumerable<Vector3d> Vertices { get; }
    }
}
