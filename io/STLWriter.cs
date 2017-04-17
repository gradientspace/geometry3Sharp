using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

namespace g3
{

    public class STLWriter : IMeshWriter
    {
        // entire data block for stl triangle, that we can directly convert to byte[]
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        struct stl_triangle
        {
            public float nx, ny, nz;
            public float ax, ay, az;
            public float bx, by, bz;
            public float cx, cy, cz;
            public short attrib;
        }


        public IOWriteResult Write(BinaryWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            string header = "g3sharp_stl ";
            byte[] header_bytes = ASCIIEncoding.ASCII.GetBytes(header);
            byte[] stl_header = new byte[80];
            Array.Clear(stl_header, 0, stl_header.Length);
            Array.Copy(header_bytes, stl_header, header_bytes.Length);

            writer.Write(stl_header);

            int total_tris = 0;
            foreach (WriteMesh mesh in vMeshes)
                total_tris += mesh.Mesh.TriangleCount;
            writer.Write(total_tris);

            for (int mi = 0; mi < vMeshes.Count; ++mi) {
                IMesh mesh = vMeshes[mi].Mesh;

                if (options.ProgressFunc != null)
                    options.ProgressFunc(mi, vMeshes.Count - 1);

                Func<int, stl_triangle> producerF = (ti) => {
                    stl_triangle tri = new stl_triangle();
                    Index3i t = mesh.GetTriangle(ti);
                    Vector3d a = mesh.GetVertex(t.a), b = mesh.GetVertex(t.b), c = mesh.GetVertex(t.c);
                    Vector3d n = MathUtil.Normal(a, b, c);

                    tri.nx = (float)n.x; tri.ny = (float)n.y; tri.nz = (float)n.z;
                    tri.ax = (float)a.x; tri.ay = (float)a.y; tri.az = (float)a.z;
                    tri.bx = (float)b.x; tri.by = (float)b.y; tri.bz = (float)b.z;
                    tri.cx = (float)c.x; tri.cy = (float)c.y; tri.cz = (float)c.z;
                    tri.attrib = 0;
                    return tri;
                };
                Action<stl_triangle> consumerF = (tri) => {
                    byte[] tri_bytes = Util.StructureToByteArray(tri);
                    writer.Write(tri_bytes);
                };

                ParallelStream<int, stl_triangle> stream = new ParallelStream<int, stl_triangle>();
                stream.ProducerF = producerF;
                stream.ConsumerF = consumerF;

                // parallel version is slower =\
                //stream.Run_Thread(mesh.TriangleIndices());
                stream.Run(mesh.TriangleIndices());
            }

            return new IOWriteResult(IOCode.Ok, "");
        }






        public IOWriteResult Write(TextWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            if (options.bCombineMeshes == true)
                writer.WriteLine("solid \"mesh\"");

            string three_floats = Util.MakeVec3FormatString(0, 1, 2, options.RealPrecisionDigits);

            for (int mi = 0; mi < vMeshes.Count; ++mi) {

                IMesh mesh = vMeshes[mi].Mesh;

                if (options.ProgressFunc != null)
                    options.ProgressFunc(mi, vMeshes.Count - 1);

                string solid_name = string.Format("mesh_{0}", mi);
                if (options.bCombineMeshes == false) {
                    if (vMeshes[mi].Name != null && vMeshes[mi].Name.Length > 0)
                        solid_name = vMeshes[mi].Name;
                    writer.WriteLine("solid \"{0}\"", solid_name);
                }

                foreach ( int ti in mesh.TriangleIndices() ) {
                    Index3i t = mesh.GetTriangle(ti);
                    Vector3d a = mesh.GetVertex(t.a), b = mesh.GetVertex(t.b), c = mesh.GetVertex(t.c);
                    Vector3d n = MathUtil.Normal(a, b, c);
                    writer.WriteLine("facet normal " + three_floats, n.x, n.y, n.z);
                    writer.WriteLine("outer loop" + writer.NewLine + "vertex " + three_floats, a.x, a.y, a.z);
                    writer.WriteLine("vertex " + three_floats, b.x, b.y, b.z);
                    writer.WriteLine("vertex " + three_floats, c.x, c.y, c.z);
                    writer.WriteLine("endloop" + writer.NewLine + "endfacet");
                }

                if (options.bCombineMeshes == false)
                    writer.WriteLine("endsolid \"{0}\"", solid_name);
            }

            if (options.bCombineMeshes == true)
                writer.WriteLine("endsolid \"mesh\"");

            return new IOWriteResult(IOCode.Ok, "");
        }


    }
}
