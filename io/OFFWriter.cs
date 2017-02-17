using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

namespace g3
{
	//
	// Write OFF mesh format
	// https://en.wikipedia.org/wiki/OFF_(file_format)
	//
    public class OFFWriter : IMeshWriter
    {
 

        public IOWriteResult Write(BinaryWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            return new IOWriteResult(IOCode.FormatNotSupportedError, "binary write not supported for OFF format");
        }



        public IOWriteResult Write(TextWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            int N = vMeshes.Count;

            writer.WriteLine("OFF");

            string three_floats = Util.MakeVec3FormatString(0, 1, 2, options.RealPrecisionDigits);

            int nTotalV = 0, nTotalT = 0, nTotalE = 0;

			// OFF only supports one mesh, so have to collapse all input meshes
			// into a single list, with mapping for triangles
			// [TODO] can skip this if input is a single mesh!
            int[][] mapV = new int[N][];
            for ( int mi = 0; mi < N; ++mi ) {
                nTotalV += vMeshes[mi].Mesh.VertexCount;
                nTotalT += vMeshes[mi].Mesh.TriangleCount;
                nTotalE += 0;
                mapV[mi] = new int[vMeshes[mi].Mesh.MaxVertexID];
            }
            writer.WriteLine(string.Format("{0} {1} {2}", nTotalV, nTotalT, nTotalE));


            // write all vertices, and construct vertex re-map
            int vi = 0;
            for (int mi = 0; mi < N; ++mi) {
                IMesh mesh = vMeshes[mi].Mesh;
                if (options.ProgressFunc != null)
                    options.ProgressFunc(mi, 2*(N - 1));
                foreach (int vid in mesh.VertexIndices()) {
                    Vector3d v = mesh.GetVertex(vid);
                    writer.WriteLine(three_floats, v.x, v.y, v.z);
                    mapV[mi][vid] = vi;
                    vi++;
                }
            }

            // write all triangles
            for (int mi = 0; mi < N; ++mi) {
                IMesh mesh = vMeshes[mi].Mesh;
                if (options.ProgressFunc != null)
                    options.ProgressFunc(N + mi, 2*(N - 1));

                foreach ( int ti in mesh.TriangleIndices() ) {
                    Index3i t = mesh.GetTriangle(ti);
                    t[0] = mapV[mi][t[0]];
                    t[1] = mapV[mi][t[1]];
                    t[2] = mapV[mi][t[2]];
                    writer.WriteLine(string.Format("3 {0} {1} {2}", t[0], t[1], t[2]));
                }
            }

            return new IOWriteResult(IOCode.Ok, "");
        }


    }
}
