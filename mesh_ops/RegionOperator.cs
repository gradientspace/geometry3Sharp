using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace g3
{
    /// <summary>
    /// This class automatically extracts a submesh from a mesh, and can re-insert it after you have
    /// edited it, as long as you have not messed up the boundary
    /// 
    /// [TODO] Nearly all the code here is duplicated from RegionRemesher. Maybe this could be a base class for that?
    /// [TODO] ReinsertSubToBaseMapT is not returned by the MeshEditor.ReinsertSubmesh, instead we are
    ///   trying to guess it here, by making some assumptions about what happens. It works for now, but
    ///   it would better if MeshEditor returned this information.
    /// </summary>
    public class RegionOperator
    {
        public DMesh3 BaseMesh;
        public DSubmesh3 Region;

        // this is only valid after BackPropagate() call!! maps submeshverts to base mesh
        public IndexMap ReinsertSubToBaseMapV;

        // [RMS] computation of this is kind of a hack right now...
        public IndexMap ReinsertSubToBaseMapT;

        // handle a tricky problem...see comments for DuplicateTriBehavior enum
        public MeshEditor.DuplicateTriBehavior ReinsertDuplicateTriBehavior = MeshEditor.DuplicateTriBehavior.AssertContinue;

        int[] cur_base_tris;

        public RegionOperator(DMesh3 mesh, int[] regionTris, Action<DSubmesh3> submeshConfigF = null)
        {
            BaseMesh = mesh;
            Region = new DSubmesh3(mesh);
            if (submeshConfigF != null)
                submeshConfigF(Region);
            Region.Compute(regionTris);
            Region.ComputeBoundaryInfo(regionTris);

            cur_base_tris = (int[])regionTris.Clone();
        }

        public RegionOperator(DMesh3 mesh, IEnumerable<int> regionTris, Action<DSubmesh3> submeshConfigF = null)
        {
            BaseMesh = mesh;
            Region = new DSubmesh3(mesh);
            if (submeshConfigF != null)
                submeshConfigF(Region);
            Region.Compute(regionTris);
            int count = regionTris.Count();
            Region.ComputeBoundaryInfo(regionTris, count);

            cur_base_tris = regionTris.ToArray();
        }


        /// <summary>
        /// list of sub-region triangles. This is either the input regionTris,
        /// or the submesh triangles after they are re-inserted.
        /// </summary>
        public int[] CurrentBaseTriangles {
            get { return cur_base_tris; }
        }


        /// <summary>
        /// find base-mesh interior vertices of region (ie does not include region boundary vertices)
        /// </summary>
        public HashSet<int> CurrentBaseInteriorVertices()
        {
            HashSet<int> verts = new HashSet<int>();
            IndexHashSet borderv = Region.BaseBorderV;
            foreach ( int tid in cur_base_tris ) {
                Index3i tv = BaseMesh.GetTriangle(tid);
                if (borderv[tv.a] == false) verts.Add(tv.a);
                if (borderv[tv.b] == false) verts.Add(tv.b);
                if (borderv[tv.c] == false) verts.Add(tv.c);
            }
            return verts;
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
            for (int eid = 0; eid < NE; ++eid) {
                if (Region.SubMesh.IsEdge(eid) == false)
                    continue;
                if (Region.SubMesh.IsBoundaryEdge(eid))
                    continue;
                Index2i edgev = Region.SubMesh.GetEdgeV(eid);
                if (Region.SubMesh.IsBoundaryVertex(edgev.a) && Region.SubMesh.IsBoundaryVertex(edgev.b)) {
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
            for (int i = 0; i < split_edges.Count; ++i) {
                DMesh3.EdgeSplitInfo split_info;
                Region.SubMesh.SplitEdge(split_edges[i], out split_info);
            }
        }


        /// <summary>
        /// set group ID for entire submesh
        /// </summary>
        public void SetSubmeshGroupID(int gid)
        {
            FaceGroupUtil.SetGroupID(Region.SubMesh, gid);
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
            if (bAllowSubmeshRepairs) {
                RepairPossibleNonManifoldEdges();
            }

            // remove existing submesh triangles
            MeshEditor editor = new MeshEditor(BaseMesh);
            editor.RemoveTriangles(cur_base_tris, true);

            // insert new submesh
            int[] new_tris = new int[Region.SubMesh.TriangleCount];
            ReinsertSubToBaseMapV = null;
            bool bOK = editor.ReinsertSubmesh(Region, ref new_tris, out ReinsertSubToBaseMapV, ReinsertDuplicateTriBehavior);

            // reconstruct this...hacky?
            int NT = Region.SubMesh.MaxTriangleID;
            ReinsertSubToBaseMapT = new IndexMap(false, NT);
            int nti = 0;
            for (int ti = 0; ti < NT; ++ti) {
                if (Region.SubMesh.IsTriangle(ti) == false)
                    continue;
                ReinsertSubToBaseMapT[ti] = new_tris[nti++];
            }

            // assert that new triangles are all valid (goes wrong sometimes??)
            Debug.Assert(IndexUtil.IndicesCheck(new_tris, BaseMesh.IsTriangle));

            cur_base_tris = new_tris;
            return bOK;
        }





        // transfer vertex positions in submesh back to base mesh
        public bool BackPropropagateVertices(bool bRecomputeBoundaryNormals = false)
        {
            bool bNormals = (Region.SubMesh.HasVertexNormals && Region.BaseMesh.HasVertexNormals);
            foreach ( int subvid in Region.SubMesh.VertexIndices() ) {
                int basevid = Region.SubToBaseV[subvid];
                Vector3d v = Region.SubMesh.GetVertex(subvid);
                Region.BaseMesh.SetVertex(basevid, v);
                if (bNormals)
                    Region.BaseMesh.SetVertexNormal(basevid, Region.SubMesh.GetVertexNormal(subvid));
            }

            if (bRecomputeBoundaryNormals) {
                foreach ( int basevid in Region.BaseBorderV ) {
                    Vector3d n = MeshNormals.QuickCompute(Region.BaseMesh, basevid);
                    Region.BaseMesh.SetVertexNormal(basevid, (Vector3f)n);
                }
            }

            return true;
        }



    }
}
