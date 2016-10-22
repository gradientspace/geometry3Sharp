using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace g3
{
    public class OBJWriter : IMeshWriter
    {
        public Tuple<WriteResult, string> Write(BinaryWriter writer, List<IMesh> vMeshes, WriteOptions options)
        {
            // [RMS] not supported
            throw new NotImplementedException();
        }



        public Tuple<WriteResult, string> Write(TextWriter writer, List<IMesh> vMeshes, WriteOptions options)
        {
            int nAccumCountV = 1;       // OBJ indices always start at 1

            for (int mi = 0; mi < vMeshes.Count; ++mi) {

                IMesh mesh = vMeshes[mi];
                bool bVtxColors = options.bPerVertexColors && mesh.HasVertexColors();
                bool bNormals = options.bPerVertexNormals && mesh.HasVertexNormals();

                int nCountV = mesh.GetVertexCount();
                for (int vi = 0; vi < nCountV; ++vi) {
                    Vector3d v = mesh.GetVertex(vi);
                    if ( bVtxColors ) {
                        Vector3d c = mesh.GetVertexColor(vi);
                        writer.WriteLine("v {0} {1} {2} {3:F8} {4:F8} {5:F8}", v[0], v[1], v[2], c[0], c[1], c[2]);
                    } else {
                        writer.WriteLine("v {0} {1} {2}", v[0], v[1], v[2]);
                    }

                    if ( options.bPerVertexNormals && mesh.HasVertexNormals() ) {
                        Vector3d n = mesh.GetVertexNormal(vi);
                        writer.WriteLine("vn {0:F10} {1:F10} {2:F10}", n[0], n[1], n[2]);
                    }
                }

                int nCountT = mesh.GetTriangleCount();
                for (int ti = 0; ti < nCountT; ++ti) {
                    Vector3i t = mesh.GetTriangle(ti);
                    t.Add(nAccumCountV);
                    
                    if ( bNormals ) {
                        writer.WriteLine("f {0}//{0} {1}//{1} {2}//{2}", t[0], t[1], t[2]);
                    } else {
                        writer.WriteLine("f {0} {1} {2}", t[0], t[1], t[2]);
                    }

                }

                nAccumCountV += nCountV;
            }


            return Tuple.Create(WriteResult.Ok, "");
        }


    }
}
