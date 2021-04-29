using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace g3
{
    class GTSWriter : IMeshWriter
    {
        private const int StartingIndex = 1;

        public IOWriteResult Write(TextWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            if (vMeshes.Count != 1)
            {
                throw new Exception("GTS Writer supports only single mesh exporting");
            }

            string three_floats = Util.MakeVec3FormatString(0, 1, 2, options.RealPrecisionDigits);

            IMesh mesh = vMeshes[0].Mesh;

            int edgeIndex = StartingIndex;
            int[] vertices = new int[mesh.MaxVertexID];
            Dictionary<UndirectedEdge, int> edges = new Dictionary<UndirectedEdge, int>();

            foreach (int ti in mesh.TriangleIndices())
            {
                Index3i t = mesh.GetTriangle(ti);
                if (!edges.ContainsKey(new UndirectedEdge(t[0], t[1])))
                {
                    edges.Add(new UndirectedEdge(t[0], t[1]), edgeIndex++);
                }

                if (!edges.ContainsKey(new UndirectedEdge(t[1], t[2])))
                {
                    edges.Add(new UndirectedEdge(t[1], t[2]), edgeIndex++);
                }

                if (!edges.ContainsKey(new UndirectedEdge(t[2], t[0])))
                {
                    edges.Add(new UndirectedEdge(t[2], t[0]), edgeIndex++);
                }
            }

            writer.WriteLine($"{mesh.VertexCount} {edges.Count} {mesh.TriangleCount}");

            int vertexIndex = StartingIndex;
            foreach (int vid in mesh.VertexIndices())
            {
                Vector3d vertex = mesh.GetVertex(vid);
                writer.WriteLine(three_floats, vertex.x, vertex.y, vertex.z, CultureInfo.InvariantCulture);
                vertices[vid] = vertexIndex++;
            }

            foreach (var edge in edges.OrderBy(w => w.Value))
            {
                writer.WriteLine($"{vertices[edge.Key.Vertex1]} {vertices[edge.Key.Vertex2]}");
            }

            foreach (int ti in mesh.TriangleIndices())
            {
                Index3i t = mesh.GetTriangle(ti);
                // According to GTS sources it is OK to just write triangle edges in this order
                writer.WriteLine($"{edges[new UndirectedEdge(t[0], t[1])]} {edges[new UndirectedEdge(t[1], t[2])]} {edges[new UndirectedEdge(t[2], t[0])]}");
            }

            return IOWriteResult.Ok;
        }

        public IOWriteResult Write(BinaryWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            throw new NotImplementedException();
        }

        readonly struct UndirectedEdge : IEquatable<UndirectedEdge>
        {
            public readonly int Vertex1;

            public readonly int Vertex2;

            public UndirectedEdge(int vertex1, int vertex2)
            {
                Vertex1 = Math.Min(vertex1, vertex2);
                Vertex2 = Math.Max(vertex1, vertex2);
            }
            
            public bool Equals(UndirectedEdge other)
            {
                return Vertex1 == other.Vertex1 && Vertex2 == other.Vertex2;
            }

            public override bool Equals(object obj)
            {
                return obj is UndirectedEdge other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Vertex1 * 397) ^ Vertex2;
                }
            }
        }
    }
}