using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace g3
{
    public class RegionRemesher : Remesher
    {
        public DMesh3 BaseMesh;
        public DSubmesh3 Region;

        // By default is initialized w/ all boundary constraints
        // You can add more, but don't screw up!
        MeshConstraints bdry_constraints;

        int[] cur_base_tris;

        public RegionRemesher(DMesh3 mesh, int[] regionTris)
        {
            BaseMesh = mesh;
            Region = new DSubmesh3(mesh, regionTris);
            Region.ComputeBoundaryInfo(regionTris);
            base.mesh = Region.SubMesh;

            cur_base_tris = (int[])regionTris.Clone();

            // constrain region-boundary edges
            bdry_constraints = new MeshConstraints();
            MeshConstraintUtil.FixSubmeshBoundaryEdges(bdry_constraints, Region);
            SetExternalConstraints(bdry_constraints);
        }


        /// <summary>
        /// list of sub-region triangles. This is either the input regionTris,
        /// or the submesh triangles after they are re-inserted.
        /// </summary>
        public int[] CurrentBaseTriangles
        {
            get { return cur_base_tris; }
        }


        // After remeshing we may create an internal edge between two boundary vertices [a,b].
        // Those vertices will be merged with vertices c and d in the base mesh. If the edge
        // [c,d] already exists in the base mesh, then after the merge we would have at least
        // 3 triangles at this edge. Dang.
        //
        // A common example is a 'fin' triangle that would duplicate a
        // 'fin' on the border of the base mesh after removing the submesh, but this situation can
        // arise anywhere (eg think about one-triangle-wide strips).
        //
        // This is very hard to remove, but we can at least avoid creating non-manifold edges (which
        // with the current DMesh3 will be prevented, hence leaving a hole) by splitting the 
        // internal edge in the submesh (which presumably we were remeshing anyway, so changes are ok).
        public void RepairPossibleNonManifoldEdges()
        {
            // [TODO] do we need to repeat this more than once? I don't think so...

            // repair submesh
            int NE = Region.SubMesh.MaxEdgeID;
            List<int> split_edges = new List<int>();
            for ( int eid = 0; eid < NE; ++eid ) {
                if (Region.SubMesh.IsEdge(eid) == false)
                    continue;
                if (Region.SubMesh.edge_is_boundary(eid))
                    continue;
                Index2i edgev = Region.SubMesh.GetEdgeV(eid);
                if (Region.SubMesh.vertex_is_boundary(edgev.a) && Region.SubMesh.vertex_is_boundary(edgev.b)) {
                    // ok, we have an internal edge where both verts are on the boundary
                    // now check if it is an edge in the base mesh
                    int base_a = Region.MapVertexToBaseMesh(edgev.a);
                    int base_b = Region.MapVertexToBaseMesh(edgev.b);
                    if (base_a != DMesh3.InvalidID && base_b != DMesh3.InvalidID) {
                        // both vertices in base mesh...right?
                        Debug.Assert(Region.BaseMesh.IsVertex(base_a) && Region.BaseMesh.IsVertex(base_b));
                        int base_eid = Region.BaseMesh.FindEdge(base_a, base_b);
                        if (base_eid != DMesh3.InvalidID)
                            split_edges.Add(eid);
                    }
                }
            }

            // split any problem edges we found and repeat this loop
            for ( int i = 0; i < split_edges.Count; ++i ) {
                DMesh3.EdgeSplitInfo split_info;
                Region.SubMesh.SplitEdge(split_edges[i], out split_info);
            }
        }



        // Remove the original submesh region and merge in the remeshed version.
        // You can call this multiple times as the base-triangle-set is updated.
        //
        // By default, we allow the submesh to be modified to prevent creation of
        // non-manifold edges. You can disable this, however then some of the submesh
        // triangles may be discarded.
        //
        // Returns false if there were errors in insertion, ie if some triangles
        // failed to insert. Does not revert changes that were successful.
        public bool BackPropropagate(bool bAllowSubmeshRepairs = true)
        {
            if (bAllowSubmeshRepairs)
                RepairPossibleNonManifoldEdges();

            // remove existing submesh triangles
            MeshEditor editor = new MeshEditor(BaseMesh);
            editor.RemoveTriangles(cur_base_tris, true);

            // insert new submesh
            int[] new_tris = new int[Region.SubMesh.TriangleCount];
            IndexMap mapV;
            bool bOK = editor.ReinsertSubmesh(Region, ref new_tris, out mapV);
            cur_base_tris = new_tris;

            // assert that new triangles are all valid (goes wrong sometimes??)
            Debug.Assert(IndexUtil.IndicesCheck(cur_base_tris, BaseMesh.IsTriangle));

            return bOK;
        }





        public static RegionRemesher QuickRemesh(DMesh3 mesh, int[] tris, 
            double minEdgeLen, double maxEdgeLen, double smoothSpeed, 
            int rounds, 
            IProjectionTarget target)
        {
            RegionRemesher remesh = new RegionRemesher(mesh, tris);
            if ( target != null )
                remesh.SetProjectionTarget(target);
            remesh.MinEdgeLength = minEdgeLen;
            remesh.MaxEdgeLength = maxEdgeLen;
            remesh.SmoothSpeedT = smoothSpeed;
            for (int k = 0; k < rounds; ++k) {
                remesh.BasicRemeshPass();
            }
            remesh.BackPropropagate();
            return remesh;
        }


    }
}
