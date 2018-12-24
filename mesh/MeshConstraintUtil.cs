using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace g3
{
    public static class MeshConstraintUtil
    {

        // for all edges, disable flip/split/collapse
        // for all vertices, pin in current position
        public static void FixEdges(MeshConstraints cons, DMesh3 mesh, IEnumerable<int> edges)
        {
            foreach ( int ei in edges ) { 
                if (mesh.IsEdge(ei)) {
                    cons.SetOrUpdateEdgeConstraint(ei, EdgeConstraint.FullyConstrained);
                    Index2i ev = mesh.GetEdgeV(ei);
                    cons.SetOrUpdateVertexConstraint(ev.a, VertexConstraint.Pinned);
                    cons.SetOrUpdateVertexConstraint(ev.b, VertexConstraint.Pinned);
                }
            }
        }


        // for all mesh boundary edges, disable flip/split/collapse
        // for all mesh boundary vertices, pin in current position
        public static void FixAllBoundaryEdges(MeshConstraints cons, DMesh3 mesh)
        {
            int NE = mesh.MaxEdgeID;
            for ( int ei = 0; ei < NE; ++ei ) {
                if ( mesh.IsEdge(ei) && mesh.IsBoundaryEdge(ei) ) {
                    cons.SetOrUpdateEdgeConstraint(ei, EdgeConstraint.FullyConstrained);

                    Index2i ev = mesh.GetEdgeV(ei);
                    cons.SetOrUpdateVertexConstraint(ev.a, VertexConstraint.Pinned);
                    cons.SetOrUpdateVertexConstraint(ev.b, VertexConstraint.Pinned);
                }
            }
        }
        public static void FixAllBoundaryEdges(Remesher r)
        {
            if (r.Constraints == null)
                r.SetExternalConstraints(new MeshConstraints());
            FixAllBoundaryEdges(r.Constraints, r.Mesh);
        }


        // for all mesh boundary vertices, pin in current position, but allow collapses
        public static void FixAllBoundaryEdges_AllowCollapse(MeshConstraints cons, DMesh3 mesh, int setID)
        {
            EdgeConstraint edgeCons = new EdgeConstraint(EdgeRefineFlags.NoFlip | EdgeRefineFlags.NoSplit);
            VertexConstraint vertCons = new VertexConstraint(true, setID);

            int NE = mesh.MaxEdgeID;
            for ( int ei = 0; ei < NE; ++ei ) {
                if ( mesh.IsEdge(ei) && mesh.IsBoundaryEdge(ei) ) {
                    cons.SetOrUpdateEdgeConstraint(ei, edgeCons);

                    Index2i ev = mesh.GetEdgeV(ei);
                    cons.SetOrUpdateVertexConstraint(ev.a, vertCons);
                    cons.SetOrUpdateVertexConstraint(ev.b, vertCons);
                }
            }
        }



        // for all mesh boundary vertices, pin in current position, but allow splits
        public static void FixAllBoundaryEdges_AllowSplit(MeshConstraints cons, DMesh3 mesh, int setID)
        {
            EdgeConstraint edgeCons = new EdgeConstraint(EdgeRefineFlags.NoFlip | EdgeRefineFlags.NoCollapse);
            VertexConstraint vertCons = new VertexConstraint(true, setID);

            int NE = mesh.MaxEdgeID;
            for (int ei = 0; ei < NE; ++ei) {
                if (mesh.IsEdge(ei) && mesh.IsBoundaryEdge(ei)) {
                    cons.SetOrUpdateEdgeConstraint(ei, edgeCons);

                    Index2i ev = mesh.GetEdgeV(ei);
                    cons.SetOrUpdateVertexConstraint(ev.a, vertCons);
                    cons.SetOrUpdateVertexConstraint(ev.b, vertCons);
                }
            }
        }


        // loop through submesh border edges on basemesh, map to submesh, and
        // pin those edges / vertices
        public static void FixSubmeshBoundaryEdges(MeshConstraints cons, DSubmesh3 sub)
        {
            Debug.Assert(sub.BaseBorderE != null);
            foreach ( int base_eid in sub.BaseBorderE ) {
                Index2i base_ev = sub.BaseMesh.GetEdgeV(base_eid);
                Index2i sub_ev = sub.MapVerticesToSubmesh(base_ev);
                int sub_eid = sub.SubMesh.FindEdge(sub_ev.a, sub_ev.b);
                Debug.Assert(sub_eid != DMesh3.InvalidID);
                Debug.Assert(sub.SubMesh.IsBoundaryEdge(sub_eid));
            
                cons.SetOrUpdateEdgeConstraint(sub_eid, EdgeConstraint.FullyConstrained);
                cons.SetOrUpdateVertexConstraint(sub_ev.a, VertexConstraint.Pinned);
                cons.SetOrUpdateVertexConstraint(sub_ev.b, VertexConstraint.Pinned);
            }
        }




        // for all mesh boundary edges, disable flip/split/collapse
        // for all mesh boundary vertices, pin in current position
        public static void FixAllGroupBoundaryEdges(MeshConstraints cons, DMesh3 mesh, bool bPinVertices)
        {
            int NE = mesh.MaxEdgeID;
            for ( int ei = 0; ei < NE; ++ei ) {
                if ( mesh.IsEdge(ei) && mesh.IsGroupBoundaryEdge(ei) ) {
                    cons.SetOrUpdateEdgeConstraint(ei, EdgeConstraint.FullyConstrained);

                    if (bPinVertices) {
                        Index2i ev = mesh.GetEdgeV(ei);
                        cons.SetOrUpdateVertexConstraint(ev.a, VertexConstraint.Pinned);
                        cons.SetOrUpdateVertexConstraint(ev.b, VertexConstraint.Pinned);
                    }
                }
            }
        }
        public static void FixAllGroupBoundaryEdges(Remesher r, bool bPinVertices)
        {
            if (r.Constraints == null)
                r.SetExternalConstraints(new MeshConstraints());
            FixAllGroupBoundaryEdges(r.Constraints, r.Mesh, bPinVertices);
        }



        // for all vertices in loopV, constrain to target
        // for all edges in loopV, disable flips and constrain to target
        public static void ConstrainVtxLoopTo(MeshConstraints cons, DMesh3 mesh, IList<int> loopV, IProjectionTarget target, int setID = -1)
        {
            VertexConstraint vc = new VertexConstraint(target);
            int N = loopV.Count;
            for (int i = 0; i < N; ++i)
                cons.SetOrUpdateVertexConstraint(loopV[i], vc);

            EdgeConstraint ec = new EdgeConstraint(EdgeRefineFlags.NoFlip, target);
            ec.TrackingSetID = setID;
            for ( int i = 0; i < N; ++i ) {
                int v0 = loopV[i];
                int v1 = loopV[(i + 1) % N];

                int eid = mesh.FindEdge(v0, v1);
                Debug.Assert(eid != DMesh3.InvalidID);
                if ( eid != DMesh3.InvalidID )
                    cons.SetOrUpdateEdgeConstraint(eid, ec);
            }

        }
        public static void ConstrainVtxLoopTo(Remesher r, int[] loopV, IProjectionTarget target, int setID = -1)
        {
            if (r.Constraints == null)
                r.SetExternalConstraints(new MeshConstraints());
            ConstrainVtxLoopTo(r.Constraints, r.Mesh, loopV, target);
        }





        // for all vertices in loopV, constrain to target
        // for all edges in loopV, disable flips and constrain to target
        public static void ConstrainVtxSpanTo(MeshConstraints cons, DMesh3 mesh, IList<int> spanV, IProjectionTarget target, int setID = -1)
        {
            VertexConstraint vc = new VertexConstraint(target);
            int N = spanV.Count;
            for (int i = 1; i < N-1; ++i)
                cons.SetOrUpdateVertexConstraint(spanV[i], vc);
            cons.SetOrUpdateVertexConstraint(spanV[0], VertexConstraint.Pinned);
            cons.SetOrUpdateVertexConstraint(spanV[N-1], VertexConstraint.Pinned);

            EdgeConstraint ec = new EdgeConstraint(EdgeRefineFlags.NoFlip, target);
            ec.TrackingSetID = setID;
            for (int i = 0; i < N-1; ++i) {
                int v0 = spanV[i];
                int v1 = spanV[i + 1];

                int eid = mesh.FindEdge(v0, v1);
                Debug.Assert(eid != DMesh3.InvalidID);
                if (eid != DMesh3.InvalidID)
                    cons.SetOrUpdateEdgeConstraint(eid, ec);
            }

        }
        public static void ConstrainVtxSpanTo(Remesher r, int[] spanV, IProjectionTarget target, int setID = -1)
        {
            if (r.Constraints == null)
                r.SetExternalConstraints(new MeshConstraints());
            ConstrainVtxSpanTo(r.Constraints, r.Mesh, spanV, target);
        }






        public static void PreserveBoundaryLoops(MeshConstraints cons, DMesh3 mesh) {
            MeshBoundaryLoops loops = new MeshBoundaryLoops(mesh);
            foreach ( EdgeLoop loop in loops ) {
                DCurve3 loopC = MeshUtil.ExtractLoopV(mesh, loop.Vertices);
                DCurveProjectionTarget target = new DCurveProjectionTarget(loopC);
                ConstrainVtxLoopTo(cons, mesh, loop.Vertices, target);
            }
        }
        public static void PreserveBoundaryLoops(Remesher r)
        {
            if (r.Constraints == null)
                r.SetExternalConstraints(new MeshConstraints());
            PreserveBoundaryLoops(r.Constraints, r.Mesh);
        }



        public static void AddTrackedEdges(MeshConstraints cons, int[] edges, int setID)
        {
            EdgeConstraint ec = EdgeConstraint.Unconstrained;
            ec.TrackingSetID = setID;
            for ( int i = 0; i < edges.Length; ++i ) {
                cons.SetOrUpdateEdgeConstraint(edges[i], ec);
            }
        }


    }
}
