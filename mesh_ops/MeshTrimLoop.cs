// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{

    /// <summary>
    /// Delete triangles inside on/near-surface trimming curve, and then adapt the new
    /// boundary loop to conform to the loop.
    /// 
    /// [DANGER] To use this class, we require a spatial data structure we can project onto. 
    /// Currently we assume that this is a DMesh3AABBTree *because* if you don't provide a
    /// seed triangle, we use FindNearestTriangle() to find this index on the input mesh.
    /// So, it must be a tree for the exact same mesh (!). 
    /// However we then delete a bunch of triangles and use this spatial DS only for reprojection.
    /// Possibly these should be two separate things? Or force caller to provide seed triangle
    /// for trim loop, instead of solving this problem for them?
    /// (But basically there is no way around having a full mesh copy...)
    /// 
    /// 
    /// TODO:
    /// - output boundary EdgeLoop that has been aligned w/ trim curve
    /// - handle cases where input mesh has open borders
    /// </summary>
    public class MeshTrimLoop
	{
		public DMesh3 Mesh;
        public DMeshAABBTree3 Spatial;
        public DCurve3 TrimLine;

        public int RemeshBorderRings = 2;
        public double SmoothingAlpha = 1.0;     // valid range is [0,1]
        public double TargetEdgeLength = 0;     // if 0, will use average border edge length
        public int RemeshRounds = 20;

        int seed_tri = -1;
        Vector3d seed_pt = Vector3d.MaxValue;

        /// <summary>
        /// Cut mesh with plane. Assumption is that plane normal is Z value.
        /// </summary>
        public MeshTrimLoop(DMesh3 mesh, DCurve3 trimline, int tSeedTID, DMeshAABBTree3 spatial = null)
		{
            if (spatial != null && spatial.Mesh == mesh)
                throw new ArgumentException("MeshTrimLoop: input spatial DS must have its own copy of mesh");
			Mesh = mesh;
            TrimLine = new DCurve3(trimline);
            if (spatial != null) {
                Spatial = spatial;
            }
            seed_tri = tSeedTID;
		}

        public MeshTrimLoop(DMesh3 mesh, DCurve3 trimline, Vector3d vSeedPt, DMeshAABBTree3 spatial = null)
        {
            if (spatial != null && spatial.Mesh == mesh)
                throw new ArgumentException("MeshTrimLoop: input spatial DS must have its own copy of mesh");
            Mesh = mesh;
            TrimLine = new DCurve3(trimline);
            if (spatial != null) {
                Spatial = spatial;
            }
            seed_pt = vSeedPt;
        }

        public virtual ValidationStatus Validate()
		{
			// [TODO]
			return ValidationStatus.Ok;
		}


		public virtual bool Trim()
		{
            if ( Spatial == null ) {
                Spatial = new DMeshAABBTree3(new DMesh3(Mesh, false, MeshComponents.None));
                Spatial.Build();
            }

            if ( seed_tri == -1 ) {
                seed_tri = Spatial.FindNearestTriangle(seed_pt);
            }

            MeshFacesFromLoop loop = new MeshFacesFromLoop(Mesh, TrimLine, Spatial, seed_tri);

            MeshFaceSelection selection = loop.ToSelection();
            selection.LocalOptimize(true, true);
            MeshEditor editor = new MeshEditor(Mesh);
            editor.RemoveTriangles(selection, true);

            MeshConnectedComponents components = new MeshConnectedComponents(Mesh);
            components.FindConnectedT();
            if ( components.Count > 1 ) {
                int keep = components.LargestByCount;
                for ( int i = 0; i < components.Count; ++i ) {
                    if ( i != keep )
                        editor.RemoveTriangles(components[i].Indices, true);
                }
            }
            editor.RemoveAllBowtieVertices(true);

            MeshBoundaryLoops loops = new MeshBoundaryLoops(Mesh);
            bool loopsOK = false;
            try {
                loopsOK = loops.Compute();
            } catch (Exception) {
                return false;
            }
            if (!loopsOK)
                return false;


            // [TODO] to support trimming mesh w/ existing holes, we need to figure out which
            // loop we created in RemoveTriangles above!
            if (loops.Count > 1)
                return false;


            int[] loopVerts = loops[0].Vertices;

            MeshFaceSelection borderTris = new MeshFaceSelection(Mesh);
            borderTris.SelectVertexOneRings(loopVerts);
            borderTris.ExpandToOneRingNeighbours(RemeshBorderRings);

            RegionRemesher remesh = new RegionRemesher(Mesh, borderTris.ToArray());
            remesh.Region.MapVerticesToSubmesh(loopVerts);

            double target_len = TargetEdgeLength;
            if (target_len <= 0) {
                double mine, maxe, avge;
                MeshQueries.EdgeLengthStatsFromEdges(Mesh, loops[0].Edges, out mine, out maxe, out avge);
                target_len = avge;
            }

            MeshProjectionTarget meshTarget = new MeshProjectionTarget(Spatial.Mesh, Spatial);
            remesh.SetProjectionTarget(meshTarget);
            remesh.SetTargetEdgeLength(target_len);
            remesh.SmoothSpeedT = SmoothingAlpha;

            DCurveProjectionTarget curveTarget = new DCurveProjectionTarget(TrimLine);
            SequentialProjectionTarget multiTarget = new SequentialProjectionTarget(curveTarget, meshTarget);

            int set_id = 3;
            MeshConstraintUtil.ConstrainVtxLoopTo(remesh, loopVerts, multiTarget, set_id);

            for (int i = 0; i < RemeshRounds; ++i) {
                remesh.BasicRemeshPass();
            }

            remesh.BackPropropagate();

            // [TODO] output loop somehow...use MeshConstraints.FindConstrainedEdgesBySetID(set_id)...

            return true;

        } // Trim()



        


	}
}
