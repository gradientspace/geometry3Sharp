// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace g3
{
    public class MeshBoolean
    {
        public DMesh3 Target;
        public DMesh3 Tool;

        // points within this tolerance are merged
        public double VertexSnapTol = 0.00001;

        public DMesh3 Result;

        MeshMeshCut cutTargetOp;
        MeshMeshCut cutToolOp;

        DMesh3 cutTargetMesh;
        DMesh3 cutToolMesh;

        public bool Compute()
        {
            // Alternate strategy:
            //   - don't do RemoveContained
            //   - match embedded vertices, split where possible
            //   - find min-cut path through shared edges
            //   - remove contiguous patches that are inside both/etc (use MWN)
            //   ** no good for coplanar regions...


            cutTargetOp = new MeshMeshCut() {
                Target = new DMesh3(Target),
                CutMesh = Tool,
                VertexSnapTol = VertexSnapTol
            };
            cutTargetOp.Compute();
            cutTargetOp.RemoveContained();
            cutTargetMesh = cutTargetOp.Target;

            cutToolOp = new MeshMeshCut() {
                Target = new DMesh3(Tool),
                CutMesh = Target,
                VertexSnapTol = VertexSnapTol
            };
            cutToolOp.Compute();
            cutToolOp.RemoveContained();
            cutToolMesh = cutToolOp.Target;

            resolve_vtx_pairs();

            Result = cutToolMesh;
            MeshEditor.Append(Result, cutTargetMesh);

            return true;
        }







        void resolve_vtx_pairs()
        {
            //HashSet<int> targetVerts = new HashSet<int>(cutTargetOp.CutVertices);
            //HashSet<int> toolVerts = new HashSet<int>(cutToolOp.CutVertices);

            // tracking on-cut vertices is not working yet...
            Util.gDevAssert(Target.IsClosed() && Tool.IsClosed());

            HashSet<int> targetVerts = new HashSet<int>(MeshIterators.BoundaryVertices(cutTargetMesh));
            HashSet<int> toolVerts = new HashSet<int>(MeshIterators.BoundaryVertices(cutToolMesh));

            split_missing(cutTargetOp, cutToolOp, cutTargetMesh, cutToolMesh, targetVerts, toolVerts);
            split_missing(cutToolOp, cutTargetOp, cutToolMesh, cutTargetMesh, toolVerts, targetVerts);
        }


        void split_missing(MeshMeshCut fromOp, MeshMeshCut toOp, 
                           DMesh3 fromMesh, DMesh3 toMesh,
                           HashSet<int> fromVerts, HashSet<int> toVerts)
        {
            List<int> missing = new List<int>();
            foreach (int vid in fromVerts) {
                Vector3d v = fromMesh.GetVertex(vid);
                int near_vid = find_nearest_vertex(toMesh, v, toVerts);
                if (near_vid == DMesh3.InvalidID )
                    missing.Add(vid);
            }

            foreach (int vid in missing) {
                Vector3d v = fromMesh.GetVertex(vid);
                int near_eid = find_nearest_edge(toMesh, v, toVerts);
                if ( near_eid == DMesh3.InvalidID) {
                    System.Console.WriteLine("could not find edge to split?");
                    continue;
                }

                DMesh3.EdgeSplitInfo splitInfo;
                MeshResult result = toMesh.SplitEdge(near_eid, out splitInfo);
                if ( result != MeshResult.Ok ) {
                    System.Console.WriteLine("edge split failed");
                    continue;
                }

                toMesh.SetVertex(splitInfo.vNew, v);
                toVerts.Add(splitInfo.vNew);
            }
        }



        int find_nearest_vertex(DMesh3 mesh, Vector3d v, HashSet<int> vertices)
        {
            int near_vid = DMesh3.InvalidID;
            double nearSqr = VertexSnapTol * VertexSnapTol;
            foreach ( int vid in vertices ) {
                double dSqr = mesh.GetVertex(vid).DistanceSquared(ref v);
                if ( dSqr < nearSqr ) {
                    near_vid = vid;
                    nearSqr = dSqr;
                }
            }
            return near_vid;
        }

        int find_nearest_edge(DMesh3 mesh, Vector3d v, HashSet<int> vertices)
        {
            int near_eid = DMesh3.InvalidID;
            double nearSqr = VertexSnapTol * VertexSnapTol;
            foreach ( int eid in mesh.BoundaryEdgeIndices() ) {
                Index2i ev = mesh.GetEdgeV(eid);
                if (vertices.Contains(ev.a) == false || vertices.Contains(ev.b) == false)
                    continue;
                Segment3d seg = new Segment3d(mesh.GetVertex(ev.a), mesh.GetVertex(ev.b));
                double dSqr = seg.DistanceSquared(v);
                if (dSqr < nearSqr) {
                    near_eid = eid;
                    nearSqr = dSqr;
                }
            }
            return near_eid;
        }

    }
}
