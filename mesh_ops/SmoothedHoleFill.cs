// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    /// <summary>
    /// This fills a hole in a mesh by doing a trivial fill, optionally offsetting along a fixed vector,
    /// then doing a remesh, then a laplacian smooth, then a second remesh.
    /// </summary>
    public class SmoothedHoleFill
    {
        public DMesh3 Mesh;

        // after initial coarse hole fill, (optionally) offset fill patch in this direction/distance
        public Vector3d OffsetDirection = Vector3d.Zero;
        public double OffsetDistance = 0.0;

        // remeshing parameters
        public double TargetEdgeLength = 2.5;
        public double SmoothAlpha = 1.0;
        public int InitialRemeshPasses = 20;
        public bool RemeshBeforeSmooth = true;
        public bool RemeshAfterSmooth = true;

        // optionally allows extended customization of internal remesher
        // bool argument is true before smooth, false after
        public Action<Remesher, bool> ConfigureRemesherF = null;

        // the laplacian smooth is what gives us the smooth fill
        public bool EnableLaplacianSmooth = true;

        // higher iterations == smoother result, but more expensive
        // [TODO] currently only has effect if ConstrainToHoleInterior==true  (otherwise ROI expands each iteration...)
        public int SmoothSolveIterations = 1;

        /// <summary>If this is true, we don't modify any triangles outside hole (often results in lower-quality fill)</summary>
        public bool ConstrainToHoleInterior = false;


        /*
         *  ways to specify the hole we will fill
         *   (you really should use FillLoop unless you have a good reason not to)
         */

        // Option 1: fill this loop specifically
        public EdgeLoop FillLoop = null;

        // Option 2: identify right loop using these tris on the border of hole
        public List<int> BorderHintTris = null;


        /*
         *  Outputs
         */

        /// <summary> Final fill triangles. May include triangles outside initial fill loop, if ConstrainToHoleInterior=false </summary>
        public int[] FillTriangles;

        /// <summary> Final fill vertices </summary>
        public int[] FillVertices;


        public SmoothedHoleFill(DMesh3 mesh, EdgeLoop fillLoop = null)
        {
            this.Mesh = mesh;
            this.FillLoop = fillLoop;
        }


        public bool Apply()
        {
            EdgeLoop useLoop = null;

            if (FillLoop == null) {
                MeshBoundaryLoops loops = new MeshBoundaryLoops(Mesh, true);
                if (loops.Count == 0)
                    return false;

                if (BorderHintTris != null)
                    useLoop = select_loop_tris_hint(loops);
                if (useLoop == null && loops.MaxVerticesLoopIndex >= 0)
                    useLoop = loops[loops.MaxVerticesLoopIndex];
            } else {
                useLoop = FillLoop;
            }
            if (useLoop == null)
                return false;

            // step 1: do stupid hole fill
            SimpleHoleFiller filler = new SimpleHoleFiller(Mesh, useLoop);
            if (filler.Fill() == false)
                return false;

            if (useLoop.Vertices.Length <= 3 ) {
                FillTriangles = filler.NewTriangles;
                FillVertices = new int[0];
                return true;
            }

            MeshFaceSelection tris = new MeshFaceSelection(Mesh);
            tris.Select(filler.NewTriangles);

            // extrude initial fill surface (this is used in socketgen for example)
            if (OffsetDistance > 0) {
                MeshExtrudeFaces extrude = new MeshExtrudeFaces(Mesh, tris);
                extrude.ExtrudedPositionF = (v, n, vid) => {
                    return v + OffsetDistance * OffsetDirection;
                };
                if (!extrude.Extrude())
                    return false;
                tris.Select(extrude.JoinTriangles);
            }

            // if we aren't trying to stay inside hole, expand out a bit,
            // which allows us to clean up ugly edges
            if (ConstrainToHoleInterior == false) {
                tris.ExpandToOneRingNeighbours(2);
                tris.LocalOptimize(true, true);
            }

            // remesh the initial coarse fill region
            if (RemeshBeforeSmooth) {
                RegionRemesher remesh = new RegionRemesher(Mesh, tris);
                remesh.SetTargetEdgeLength(TargetEdgeLength);
                remesh.EnableSmoothing = (SmoothAlpha > 0);
                remesh.SmoothSpeedT = SmoothAlpha;
                if (ConfigureRemesherF != null)
                    ConfigureRemesherF(remesh, true);
                for (int k = 0; k < InitialRemeshPasses; ++k)
                    remesh.BasicRemeshPass();
                remesh.BackPropropagate();

                tris = new MeshFaceSelection(Mesh);
                tris.Select(remesh.CurrentBaseTriangles);
                if (ConstrainToHoleInterior == false)
                    tris.LocalOptimize(true, true);
            }

            if (ConstrainToHoleInterior) {
                for (int k = 0; k < SmoothSolveIterations; ++k ) {
                    smooth_and_remesh_preserve(tris, k == SmoothSolveIterations-1);
                    tris = new MeshFaceSelection(Mesh); tris.Select(FillTriangles);
                }
            } else {
                smooth_and_remesh(tris);
                tris = new MeshFaceSelection(Mesh); tris.Select(FillTriangles);
            }

            MeshVertexSelection fill_verts = new MeshVertexSelection(Mesh);
            fill_verts.SelectInteriorVertices(tris);
            FillVertices = fill_verts.ToArray();

            return true;
        }



        void smooth_and_remesh_preserve(MeshFaceSelection tris, bool bFinal)
        {
            if (EnableLaplacianSmooth) {
                LaplacianMeshSmoother.RegionSmooth(Mesh, tris, 2, 2, true);
            }

            if (RemeshAfterSmooth) {
                MeshProjectionTarget target = (bFinal) ? MeshProjectionTarget.Auto(Mesh, tris, 5) : null;

                RegionRemesher remesh2 = new RegionRemesher(Mesh, tris);
                remesh2.SetTargetEdgeLength(TargetEdgeLength);
                remesh2.SmoothSpeedT = 1.0;
                remesh2.SetProjectionTarget(target);
                if (ConfigureRemesherF != null)
                    ConfigureRemesherF(remesh2, false);
                for (int k = 0; k < 10; ++k)
                    remesh2.BasicRemeshPass();
                remesh2.BackPropropagate();

                FillTriangles = remesh2.CurrentBaseTriangles;
            } else {
                FillTriangles = tris.ToArray();
            }
        }



        void smooth_and_remesh(MeshFaceSelection tris)
        {
            if (EnableLaplacianSmooth) {
                LaplacianMeshSmoother.RegionSmooth(Mesh, tris, 2, 2, false);
            }

            if (RemeshAfterSmooth) {
                tris.ExpandToOneRingNeighbours(2);
                tris.LocalOptimize(true, true);
                MeshProjectionTarget target = MeshProjectionTarget.Auto(Mesh, tris, 5);

                RegionRemesher remesh2 = new RegionRemesher(Mesh, tris);
                remesh2.SetTargetEdgeLength(TargetEdgeLength);
                remesh2.SmoothSpeedT = 1.0;
                remesh2.SetProjectionTarget(target);
                if (ConfigureRemesherF != null)
                    ConfigureRemesherF(remesh2, false);
                for (int k = 0; k < 10; ++k)
                    remesh2.BasicRemeshPass();
                remesh2.BackPropropagate();

                FillTriangles = remesh2.CurrentBaseTriangles;
            } else {
                FillTriangles = tris.ToArray();
            }
        }









        EdgeLoop select_loop_tris_hint(MeshBoundaryLoops loops)
        {
            HashSet<int> hint_edges = new HashSet<int>();
            foreach ( int tid in BorderHintTris ) {
                if (Mesh.IsTriangle(tid) == false)
                    continue;
                Index3i et = Mesh.GetTriEdges(tid);
                for (int j = 0; j < 3; ++j) {
                    if (Mesh.IsBoundaryEdge(et[j]))
                        hint_edges.Add(et[j]);
                }
            }


            int N = loops.Count;
            int best_loop = -1;
            int max_votes = 0;
            for ( int li = 0; li < N; ++li ) {
                int votes = 0;
                EdgeLoop l = loops[li];
                foreach (int eid in l.Edges) {
                    if (hint_edges.Contains(eid))
                        votes++;
                }
                if ( votes > max_votes ) {
                    best_loop = li;
                    max_votes = votes;
                }
            }

            if (best_loop == -1)
                return null;
            return loops[best_loop];

        }


    }
}
