using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{
    public class MeshLoopClosure
    {
        public DMesh3 Mesh;
        public EdgeLoop InitialBorderLoop;

        // [TODO] auto-mode for this option
        public Frame3f FlatClosePlane;

        public double TargetEdgeLen = 0;

        public int ExtrudeGroup = -1;
        public int FillGroup = -1;

        public MeshLoopClosure(DMesh3 mesh, EdgeLoop border_loop)
        {
            Mesh = mesh;
            InitialBorderLoop = border_loop;
        }


        public virtual ValidationStatus Validate()
        {
            ValidationStatus loopStatus = MeshValidation.IsBoundaryLoop(Mesh, InitialBorderLoop);
            if ( loopStatus != ValidationStatus.Ok)
                return loopStatus;
            ValidationStatus meshStatus = MeshValidation.HasDuplicateTriangles(Mesh);
            if (meshStatus != ValidationStatus.Ok)
                return meshStatus;
            return ValidationStatus.Ok;
        }


        public virtual bool Close()
        {
            Close_Flat();
            return true;
        }


        public void Close_Flat()
        {
            double minlen, maxlen, avglen;
            MeshQueries.EdgeLengthStats(Mesh, out minlen, out maxlen, out avglen, 1000);
            double target_edge_len = (TargetEdgeLen <= 0) ? avglen : TargetEdgeLen;

            // massage around boundary loop
            List<int> refinedBorderEdges;
            cleanup_boundary(Mesh, InitialBorderLoop, avglen, out refinedBorderEdges, 3);

            // find new border loop. try to find new loop containing edges from loop we refined in cleanup_boundary,
            // if that fails just use largest loop.
            MeshBoundaryLoops loops = new MeshBoundaryLoops(Mesh);
            int iloop = loops.FindLoopContainingEdge(refinedBorderEdges[0]);
            if (iloop == -1)
                iloop = loops.MaxVerticesLoopIndex;
            EdgeLoop fill_loop = loops.Loops[iloop];

            int extrude_group = (ExtrudeGroup == -1) ? Mesh.AllocateTriangleGroup() : ExtrudeGroup;
            int fill_group = (FillGroup == -1) ? Mesh.AllocateTriangleGroup() : FillGroup;

            // decide on projection plane
            //AxisAlignedBox3d loopbox = fill_loop.GetBounds();
            //Vector3d topPt = loopbox.Center;
            //if ( bIsUpper ) {
            //    topPt.y = loopbox.Max.y + 0.25 * dims.y;
            //} else {
            //    topPt.y = loopbox.Min.y - 0.25 * dims.y;
            //}
            //Frame3f plane = new Frame3f((Vector3f)topPt);

            // extrude loop to this plane
            MeshExtrudeLoop extrude = new MeshExtrudeLoop(Mesh, fill_loop);
            extrude.PositionF = (v, n, i) => {
                return FlatClosePlane.ProjectToPlane((Vector3f)v, 1);
            };
            extrude.Extrude(extrude_group);
            MeshValidation.IsBoundaryLoop(Mesh, extrude.NewLoop);

            Debug.Assert(Mesh.CheckValidity());

            // smooth the extrude loop
            MeshLoopSmooth loop_smooth = new MeshLoopSmooth(Mesh, extrude.NewLoop);
            loop_smooth.ProjectF = (v, i) => {
                return FlatClosePlane.ProjectToPlane((Vector3f)v, 1);
            };
            loop_smooth.Alpha = 0.5f;
            loop_smooth.Rounds = 100;
            loop_smooth.Smooth();

            Debug.Assert(Mesh.CheckValidity());

            // fill result
            SimpleHoleFiller filler = new SimpleHoleFiller(Mesh, extrude.NewLoop);
            filler.Fill(fill_group);

            Debug.Assert(Mesh.CheckValidity());

            // make selection for remesh region
            MeshFaceSelection remesh_roi = new MeshFaceSelection(Mesh);
            remesh_roi.Select(extrude.NewTriangles);
            remesh_roi.Select(filler.NewTriangles);
            remesh_roi.ExpandToOneRingNeighbours();
            remesh_roi.ExpandToOneRingNeighbours();
            remesh_roi.LocalOptimize(true, true);
            int[] new_roi = remesh_roi.ToArray();

            // get rid of extrude group
            FaceGroupUtil.SetGroupToGroup(Mesh, extrude_group, 0);

            /*  clean up via remesh
             *     - constrain loop we filled to itself
             */

            RegionRemesher r = new RegionRemesher(Mesh, new_roi);

            DCurve3 top_curve = MeshUtil.ExtractLoopV(Mesh, extrude.NewLoop.Vertices);
            DCurveProjectionTarget curve_target = new DCurveProjectionTarget(top_curve);
            int[] top_loop = (int[])extrude.NewLoop.Vertices.Clone();
            r.Region.MapVerticesToSubmesh(top_loop);
            MeshConstraintUtil.ConstrainVtxLoopTo(r.Constraints, r.Mesh, top_loop, curve_target);

            DMeshAABBTree3 spatial = new DMeshAABBTree3(Mesh);
            spatial.Build();
            MeshProjectionTarget target = new MeshProjectionTarget(Mesh, spatial);
            r.SetProjectionTarget(target);

            bool bRemesh = true;
            if (bRemesh) {
                r.Precompute();
                r.EnableFlips = r.EnableSplits = r.EnableCollapses = true;
                r.MinEdgeLength = target_edge_len;
                r.MaxEdgeLength = 2 * target_edge_len;
                r.EnableSmoothing = true;
                r.SmoothSpeedT = 1.0f;
                for (int k = 0; k < 40; ++k)
                    r.BasicRemeshPass();
                r.SetProjectionTarget(null);
                r.SmoothSpeedT = 0.25f;
                for (int k = 0; k < 10; ++k)
                    r.BasicRemeshPass();
                Debug.Assert(Mesh.CheckValidity());

                r.BackPropropagate();
            }

            // smooth around the join region to clean up ugliness
            smooth_region(Mesh, r.Region.BaseBorderV, 3);
        }




        // local mesh smooth applied to all vertices in N-rings around input list
        public static void smooth_region(DMesh3 mesh, IEnumerable<int> vertices, int nRings)
        {
            MeshFaceSelection roi_t = new MeshFaceSelection(mesh);
            roi_t.SelectVertexOneRings(vertices);
            for (int i = 0; i < nRings; ++i)
                roi_t.ExpandToOneRingNeighbours();
            roi_t.LocalOptimize(true, true);

            MeshVertexSelection roi_v = new MeshVertexSelection(mesh);
            roi_v.SelectTriangleVertices(roi_t.ToArray());

            MeshIterativeSmooth mesh_smooth = new MeshIterativeSmooth(mesh, roi_v.ToArray(), true);
            mesh_smooth.Alpha = 0.2f;
            mesh_smooth.Rounds = 10;
            mesh_smooth.Smooth();
        }


        // smooths embedded loop in mesh, by first smoothing edge loop and then
        // smoothing vertex neighbourhood
        // [TODO] geodesic nbrhoold instead of # of rings
        // [TODO] reprojection?
        public static void smooth_loop(DMesh3 mesh, EdgeLoop loop, int nRings)
        {
            MeshFaceSelection roi_t = new MeshFaceSelection(mesh);
            roi_t.SelectVertexOneRings(loop.Vertices);
            for (int i = 0; i < nRings; ++i)
                roi_t.ExpandToOneRingNeighbours();
            roi_t.LocalOptimize(true, true);

            MeshVertexSelection roi_v = new MeshVertexSelection(mesh);
            roi_v.SelectTriangleVertices(roi_t.ToArray());
            roi_v.Deselect(loop.Vertices);

            MeshLoopSmooth loop_smooth = new MeshLoopSmooth(mesh, loop);
            loop_smooth.Rounds = 1;

            MeshIterativeSmooth mesh_smooth = new MeshIterativeSmooth(mesh, roi_v.ToArray(), true);
            mesh_smooth.Rounds = 1;

            for ( int i = 0; i < 10; ++i ) {
                loop_smooth.Smooth();
                mesh_smooth.Smooth();
            }
        }


        // This function does local remeshing around a boundary loop within a fixed # of
        // rings, to try to 'massage' it into a cleaner shape/topology.
        // The result_edges list is the mapped edges of loop on the resulting mesh, but it is *not* in-order
        // [TODO] use geodesic distance instead of fixed # of rings?
        public static void cleanup_boundary(DMesh3 mesh, EdgeLoop loop, double target_edge_len, out List<int> result_edges, int nRings = 3)
        {
            Debug.Assert(loop.IsBoundaryLoop());

            MeshFaceSelection roi = new MeshFaceSelection(mesh);
            roi.SelectVertexOneRings(loop.Vertices);

            for ( int i = 0; i < nRings; ++i )
                roi.ExpandToOneRingNeighbours();
            roi.LocalOptimize(true, true);

            RegionRemesher r = new RegionRemesher(mesh, roi.ToArray());

            // tag the input loop edges in the remesher, so that we can find this loop afterwards
            int[] init_loop_edges = new int[loop.EdgeCount];
            Array.Copy(loop.Edges, init_loop_edges, loop.EdgeCount);
            r.Region.MapEdgesToSubmesh(init_loop_edges);
            MeshConstraintUtil.AddTrackedEdges(r.Constraints, init_loop_edges, 100);
            //foreach (int eid in init_loop_edges)
            //    Debug.Assert(r.Region.SubMesh.IsBoundaryEdge(eid));

            r.Precompute();
            r.EnableFlips = r.EnableSplits = r.EnableCollapses = true;
            r.MinEdgeLength = target_edge_len;
            r.MaxEdgeLength = 2 * target_edge_len;
            r.EnableSmoothing = true;
            r.SmoothSpeedT = 0.1f;
            for (int k = 0; k < nRings*3; ++k)
                r.BasicRemeshPass();
            Debug.Assert(mesh.CheckValidity());

            // extract the edges we tagged (they are unordered)
            List<int> new_loop_edges = r.Constraints.FindConstrainedEdgesBySetID(100);
            //foreach (int eid in new_loop_edges)
            //    Debug.Assert(r.Region.SubMesh.IsBoundaryEdge(eid));

            r.BackPropropagate();

            // map the extracted edges back to the backpropped input mesh
            result_edges = MeshIndexUtil.MapEdgesViaVertexMap(r.ReinsertSubToBaseMapV, r.Region.SubMesh, r.BaseMesh, new_loop_edges);
            //foreach (int eid in result_edges)
            //    Debug.Assert(mesh.IsBoundaryEdge(eid));
        }


    }



}
