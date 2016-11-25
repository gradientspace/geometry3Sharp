using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace g3
{
    public class Polygon2d
    {
        List<Vector2d> vertices;

        public Polygon2d() {
            vertices = new List<Vector2d>();
        }

        public Polygon2d(Vector2d[] v)
        {
            vertices = new List<Vector2d>(v);
        }
        public Polygon2d(VectorArray2d v)
        {
            vertices = new List<Vector2d>(v.AsVector2d());
        }

        public ReadOnlyCollection<Vector2d> Vertices {
            get { return vertices.AsReadOnly(); }
        }

        public int VertexCount
        {
            get { return vertices.Count; }
        }

        public void AppendVertex(Vector2d v)
        {
            vertices.Add(v);
        }


        public Vector2d GetTangent(int i)
        {
            if (i == 0)
                return (vertices[1] - vertices[0]).Normalized;
            else if (i == vertices.Count - 1)
                return (vertices[vertices.Count - 1] - vertices[vertices.Count - 2]).Normalized;
            else
                return (vertices[i + 1] - vertices[i - 1]).Normalized;
        }



        static public Polygon2d MakeCircle(float fRadius, int nSteps)
        {
            VectorArray2d vertices = new VectorArray2d(nSteps);

            for ( int i = 0; i < nSteps; ++i ) {
                double t = (double)i / (double)nSteps;
                double a = MathUtil.TwoPI * t;
                vertices.Set(i, fRadius * Math.Cos(a), fRadius * Math.Sin(a));
            }

            return new Polygon2d(vertices);
        }


    }
}
