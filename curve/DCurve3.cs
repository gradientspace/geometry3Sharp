using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class DCurve3 : ICurve
    {
        // [TODO] use dvector? or double-indirection indexing?
        //   question is how to insert efficiently...
        public List<Vector3d> vertices;
        public bool Closed;
        public int Timestamp;

        public DCurve3()
        {
            vertices = new List<Vector3d>();
            Closed = false;
            Timestamp = 0;
        }

        public void AppendVertex(Vector3d v) {
            vertices.Add(v);
            Timestamp++;
        }

        public int VertexCount {
            get { return vertices.Count; }
        }

        public Vector3d GetVertex(int i) {
            return vertices[i];
        }
        public void SetVertex(int i, Vector3d v) {
            vertices[i] = v;
            Timestamp++;
        }

        public Vector3d Start {
            get { return vertices[0]; }
        }
        public Vector3d End {
            get { return vertices.Last(); }
        }

        public IEnumerable<Vector3d> Vertices() {
            return vertices;
        }
    }
}
