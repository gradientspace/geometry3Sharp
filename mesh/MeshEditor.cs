using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace g3
{
    // This class implements various ways to do low-level edits to a mesh, in
    // a robust/reliable way.
    //   - if operations fail in-progress, we try to back them out
    //   - (?)
    //
    // 
    public class MeshEditor
    {
        public DMesh3 Mesh;


        public MeshEditor(DMesh3 mesh)
        {
            Mesh = mesh;
        }



        public virtual int[] AddTriangleFan_OrderedVertexLoop(int center, int[] vertex_loop, int group_id = -1)
        {
            int N = vertex_loop.Length;
            int[] new_tris = new int[N];

            int i = 0;
            for ( i = 0; i < N; ++i ) {
                int a = vertex_loop[i];
                int b = vertex_loop[(i + 1) % N];

                Index3i newT = new Index3i(center, b, a);
                int new_tid = Mesh.AppendTriangle(newT, group_id);
                if (new_tid == DMesh3.InvalidID)
                    goto operation_failed;

                new_tris[i] = new_tid;
            }

            return new_tris;


            operation_failed:
                // remove what we added so far
                if (i > 0) {
                    if (remove_triangles(new_tris, i) == false)
                        throw new Exception("MeshConstructor.AddTriangleFan_OrderedVertexLoop: failed to add fan, and also falied to back out changes.");
                }
                return null;
        }





        public virtual int[] AddTriangleFan_OrderedEdgeLoop(int center, int[] edge_loop, int group_id = -1)
        {
            int N = edge_loop.Length;
            int[] new_tris = new int[N];

            int i = 0;
            for ( i = 0; i < N; ++i ) {
                if (Mesh.edge_is_boundary(edge_loop[i]) == false)
                    goto operation_failed;

                Index2i ev = Mesh.GetOrientedBoundaryEdgeV(edge_loop[i]);
                int a = ev.a, b = ev.b;

                Index3i newT = new Index3i(center, b, a);
                int new_tid = Mesh.AppendTriangle(newT, group_id);
                if (new_tid == DMesh3.InvalidID)
                    goto operation_failed;

                new_tris[i] = new_tid;
            }

            return new_tris;


            operation_failed:
                // remove what we added so far
                if (i > 0) {
                    if (remove_triangles(new_tris, i) == false)
                        throw new Exception("MeshConstructor.AddTriangleFan_OrderedEdgeLoop: failed to add fan, and also falied to back out changes.");
                }
                return null;
        }



        // this is for backing out changes we have made...
        bool remove_triangles(int[] tri_list, int count)
        {
            for (int i = 0; i < count; ++i) {
                bool bOK = Mesh.RemoveTriangle(tri_list[i], false, false);
                if (!bOK)
                    return false;
            }
            return true;
        }


    }
}
