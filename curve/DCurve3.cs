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
        protected List<Vector3d> vertices;
        public bool Closed { get; set; }
        public int Timestamp;

        public DCurve3()
        {
            vertices = new List<Vector3d>();
            Closed = false;
            Timestamp = 0;
        }

        public DCurve3(DCurve3 copy)
        {
            vertices = new List<Vector3d>(copy.vertices);
            Closed = copy.Closed;
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

        public void SetVertices(VectorArray3d v)
        {
            vertices = new List<Vector3d>();
            for (int i = 0; i < v.Count; ++i)
                vertices.Add(v[i]);
            Timestamp++;
        }

        public Vector3d this[int key]
        {
            get { return vertices[key]; }
            set { vertices[key] = value; Timestamp++; }
        }

        public Vector3d Start {
            get { return vertices[0]; }
        }
        public Vector3d End {
            get { return vertices.Last(); }
        }

        public IEnumerable<Vector3d> Vertices {
            get { return vertices; }
        }


        public AxisAlignedBox3d GetBoundingBox()
        {
            AxisAlignedBox3d box = AxisAlignedBox3d.Empty;
            foreach (Vector3d v in vertices)
                box.Contain(v);
            return box;
        }

        public double ArcLength {
            get {
                double dLen = 0;
                for (int i = 1; i < vertices.Count; ++i)
                    dLen += (vertices[i] - vertices[i - 1]).Length;
                return dLen;
            }
        }

        public Vector3d Tangent(int i)
        {
            if (i == 0)
                return (vertices[1] - vertices[0]).Normalized;
            else if (i == vertices.Count - 1)
                return (vertices.Last() - vertices[vertices.Count - 2]).Normalized;
            else
                return (vertices[i + 1] - vertices[i - 1]).Normalized;
        }

    }
}
