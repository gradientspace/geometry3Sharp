// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;

namespace gs
{
    /// <summary>
    /// Extract topological information about the mesh based on identifying
    /// semantic edges/vertices/etc
    /// 
    /// WIP
    /// 
    /// </summary>
    public class MeshTopology
    {
        public DMesh3 Mesh;

        double crease_angle = 30.0f;
        public double CreaseAngle {
            get { return crease_angle; }
            set { crease_angle = value; invalidate_topology(); }
        }

        public MeshTopology(DMesh3 mesh)
        {
            Mesh = mesh;
        }


        public HashSet<int> BoundaryEdges;
        public HashSet<int> CreaseEdges;
        public HashSet<int> AllEdges;

        public HashSet<int> AllVertices;
        public HashSet<int> JunctionVertices;

        public EdgeLoop[] Loops;
        public EdgeSpan[] Spans;

        int topo_timestamp = -1;
        public bool IgnoreTimestamp = false;


        /// <summary>
        /// Compute the topology elements
        /// </summary>
        public void Compute()
        {
            validate_topology();
        }


        /// <summary>
        /// add topological edges/vertices as constraints for remeshing 
        /// </summary>
        public void AddRemeshConstraints(MeshConstraints constraints)
        {
            validate_topology();

            int set_index = 10;

            foreach (EdgeSpan span in Spans) {
                DCurveProjectionTarget target = new DCurveProjectionTarget(span.ToCurve());
                MeshConstraintUtil.ConstrainVtxSpanTo(constraints, Mesh, span.Vertices, target, set_index++);
            }

            foreach (EdgeLoop loop in Loops) {
                DCurveProjectionTarget target = new DCurveProjectionTarget(loop.ToCurve());
                MeshConstraintUtil.ConstrainVtxLoopTo(constraints, Mesh, loop.Vertices, target, set_index++);
            }

            VertexConstraint corners = VertexConstraint.Pinned;
            corners.FixedSetID = -1;
            foreach (int vid in JunctionVertices) {
                if (constraints.HasVertexConstraint(vid)) {
                    VertexConstraint v = constraints.GetVertexConstraint(vid);
                    v.Target = null;
                    v.Fixed = true;
                    v.FixedSetID = -1;
                    constraints.SetOrUpdateVertexConstraint(vid, v);
                } else {
                    constraints.SetOrUpdateVertexConstraint(vid, corners);
                }
            }
        }



        void invalidate_topology()
        {
            topo_timestamp = -1;
        }


        void validate_topology()
        {
            if (IgnoreTimestamp && AllEdges != null)
                return;

            if ( Mesh.ShapeTimestamp != topo_timestamp ) {
                find_crease_edges(CreaseAngle);
                extract_topology();
                topo_timestamp = Mesh.ShapeTimestamp;
            }
        }



        void find_crease_edges(double angle_tol)
        {
            CreaseEdges = new HashSet<int>();
            BoundaryEdges = new HashSet<int>();

            double dot_tol = Math.Cos(angle_tol * MathUtil.Deg2Rad);

            foreach ( int eid in Mesh.EdgeIndices() ) {
                Index2i et = Mesh.GetEdgeT(eid);
                if ( et.b == DMesh3.InvalidID ) {
                    BoundaryEdges.Add(eid);
                    continue;
                }

                Vector3d n0 = Mesh.GetTriNormal(et.a);
                Vector3d n1 = Mesh.GetTriNormal(et.b);
                if ( Math.Abs(n0.Dot(n1)) < dot_tol ) {
                    CreaseEdges.Add(eid);
                }
            }

            AllEdges = new HashSet<int>(CreaseEdges); ;
            foreach ( int eid in BoundaryEdges ) 
                AllEdges.Add(eid);

            AllVertices = new HashSet<int>();
            IndexUtil.EdgesToVertices(Mesh, AllEdges, AllVertices);
        }





        void extract_topology()
        {
            DGraph3 graph = new DGraph3();

            // add vertices to graph, and store mappings
            int[] mapV = new int[Mesh.MaxVertexID];
            int[] mapVFrom = new int[AllVertices.Count];
            foreach (int vid in AllVertices) {
                int new_vid = graph.AppendVertex(Mesh.GetVertex(vid));
                mapV[vid] = new_vid;
                mapVFrom[new_vid] = vid;
            }

            // add edges to graph. graph-to-mesh eid mapping is stored via graph edge-group-id
            int[] mapE = new int[Mesh.MaxEdgeID];
            foreach (int eid in AllEdges) {
                Index2i ev = Mesh.GetEdgeV(eid);
                int new_a = mapV[ev.a];
                int new_b = mapV[ev.b];
                int new_eid = graph.AppendEdge(new_a, new_b, eid);
                mapE[eid] = new_eid;
            }

            // extract the graph topology
            DGraph3Util.Curves curves = DGraph3Util.ExtractCurves(graph, true);

            // reconstruct mesh spans / curves / junctions from graph topology

            int NP = curves.PathEdges.Count;
            Spans = new EdgeSpan[NP];
            for (int pi = 0; pi < NP; ++pi) {
                List<int> pathE = curves.PathEdges[pi];
                for (int k = 0; k < pathE.Count; ++k) {
                    pathE[k] = graph.GetEdgeGroup(pathE[k]);
                }
                Spans[pi] =  EdgeSpan.FromEdges(Mesh, pathE);
            }

            int NL = curves.LoopEdges.Count;
            Loops = new EdgeLoop[NL];
            for (int li = 0; li < NL; ++li) {
                List<int> loopE = curves.LoopEdges[li];
                for (int k = 0; k < loopE.Count; ++k) {
                    loopE[k] = graph.GetEdgeGroup(loopE[k]);
                }
                Loops[li] = EdgeLoop.FromEdges(Mesh, loopE);
            }

            JunctionVertices = new HashSet<int>();
            foreach (int gvid in curves.JunctionV)
                JunctionVertices.Add(mapVFrom[gvid]);
        }





        public DMesh3 MakeElementsMesh(Polygon2d spanProfile, Polygon2d loopProfile)
        {
            DMesh3 result = new DMesh3();
            validate_topology();

            foreach (EdgeSpan span in Spans) {
                DCurve3 curve = span.ToCurve(Mesh);
                TubeGenerator tubegen = new TubeGenerator(curve, spanProfile);
                MeshEditor.Append(result, tubegen.Generate().MakeDMesh());
            }

            foreach (EdgeLoop loop in Loops) {
                DCurve3 curve = loop.ToCurve(Mesh);
                TubeGenerator tubegen = new TubeGenerator(curve, loopProfile);
                MeshEditor.Append(result, tubegen.Generate().MakeDMesh());
            }

            return result;
        }





    }
}
