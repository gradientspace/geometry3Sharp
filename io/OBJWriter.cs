using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace g3
{
    public class OBJWriter : IMeshWriter
    {
        public IOWriteResult Write(BinaryWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            // [RMS] not supported
            throw new NotImplementedException();
        }



        public IOWriteResult Write(TextWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            int nAccumCountV = 1;       // OBJ indices always start at 1

            for (int mi = 0; mi < vMeshes.Count; ++mi) {

                IMesh mesh = vMeshes[mi].Mesh;
                bool bVtxColors = options.bPerVertexColors && mesh.HasVertexColors;
                bool bNormals = options.bPerVertexNormals && mesh.HasVertexNormals;

				int[] mapV = new int[mesh.MaxVertexID];

                foreach ( int vi in mesh.VertexIndices() ) { 
					mapV[vi] = nAccumCountV++;
                    Vector3d v = mesh.GetVertex(vi);
                    if ( bVtxColors ) {
                        Vector3d c = mesh.GetVertexColor(vi);
                        writer.WriteLine("v {0} {1} {2} {3:F8} {4:F8} {5:F8}", v[0], v[1], v[2], c[0],c[1],c[2]);
                    } else {
                        writer.WriteLine("v {0} {1} {2}", v[0], v[1], v[2]);
                    }

                    if ( options.bPerVertexNormals && mesh.HasVertexNormals ) {
                        Vector3d n = mesh.GetVertexNormal(vi);
                        writer.WriteLine("vn {0:F10} {1:F10} {2:F10}", n[0], n[1], n[2]);
                    }
                }

                if (options.bWriteGroups && mesh.HasTriangleGroups)
                    write_triangles_bygroup(writer, mesh, mapV, bNormals);
                else
                    write_triangles_flat(writer, mesh, mapV, bNormals);

            }


            return new IOWriteResult(IOCode.Ok, "");
        }




        void write_triangles_bygroup(TextWriter writer, IMesh mesh, int[] mapV, bool bNormals)
        {
            // This makes N passes over mesh indices, but doesn't use much extra memory.
            // would there be a faster way? could construct integer-pointer-list during initial
            // scan, this would need O(N) memory but then write is effectively O(N) instead of O(N*k)

            HashSet<int> vGroups = new HashSet<int>();
            foreach (int ti in mesh.TriangleIndices())
                vGroups.Add(mesh.GetTriangleGroup(ti));

            List<int> sortedGroups = new List<int>(vGroups);
            sortedGroups.Sort();
            foreach ( int g in sortedGroups ) {
                writer.WriteLine(string.Format("g mmGroup{0}", g));

                foreach (int ti in mesh.TriangleIndices() ) {
                    if (mesh.GetTriangleGroup(ti) != g)
                        continue;

                    Index3i t = mesh.GetTriangle(ti);
				    t[0] = mapV[t[0]];
				    t[1] = mapV[t[1]];
				    t[2] = mapV[t[2]];
                    
                    if ( bNormals ) {
                        writer.WriteLine("f {0}//{0} {1}//{1} {2}//{2}", t[0], t[1], t[2]);
                    } else {
                        writer.WriteLine("f {0} {1} {2}", t[0], t[1], t[2]);
                    }
                }
            }
        }


        void write_triangles_flat(TextWriter writer, IMesh mesh, int[] mapV, bool bNormals)
        {
            foreach (int ti in mesh.TriangleIndices() ) { 
                Index3i t = mesh.GetTriangle(ti);
				t[0] = mapV[t[0]];
				t[1] = mapV[t[1]];
				t[2] = mapV[t[2]];
                    
                if ( bNormals ) {
                    writer.WriteLine("f {0}//{0} {1}//{1} {2}//{2}", t[0], t[1], t[2]);
                } else {
                    writer.WriteLine("f {0} {1} {2}", t[0], t[1], t[2]);
                }
            }
        }


    }
}
