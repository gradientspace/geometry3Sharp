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
    /// Stitch together two edge loops without any constraint that they have the same vertex count
    /// (otherwise can use MeshEditor.StitchLoop / StitchUnorderedEdges).
    /// 
    /// [TODO]
    ///    - something smarter than stitch_span_simple(). For example, equalize length we have
    ///      travelled along the span. Could also use normals to try to keep span "smooth"
    ///    - currently Loop0 and Loop1 need to be reversed/not depending on whether we are
    ///      stitching "through" mesh or not. If not set properly, then fill self-intersects.
    ///      Could we (optionally) resolve this automatically, eg by checking total of the two alternatives?
    /// </summary>
    public class MeshStitchLoops
    {
        public DMesh3 Mesh;
        public EdgeLoop Loop0;
        public EdgeLoop Loop1;

        // if you are not sure that loops have correct order relative to
        // existing boundary edges, set this to false and we will figure out ourselves
        public bool TrustLoopOrientations = true;

        public SetGroupBehavior Group = SetGroupBehavior.AutoGenerate;


        // span represents an interval of loop indices on either side that
        // need to be stitched together
        struct span
        {
            public Interval1i span0;
            public Interval1i span1;
        }
        List<span> spans = new List<span>();


        public MeshStitchLoops(DMesh3 mesh, EdgeLoop l0, EdgeLoop l1)
        {
            Mesh = mesh;
            Loop0 = new EdgeLoop(l0);
            Loop1 = new EdgeLoop(l1);

            span s = new span() {
                span0 = new Interval1i(0, 0),
                span1 = new Interval1i(0, 0)
            };
            spans.Add(s);
        }


        /// <summary>
        /// specify subset of vertices that have known correspondences. 
        /// </summary>
        public void AddKnownCorrespondences(int[] verts0, int[] verts1)
        {
            int N = verts0.Length;
            if (N != verts1.Length)
                throw new Exception("MeshStitchLoops.AddKnownCorrespondence: lengths not the same!");

            // construct list of pair correspondences as loop indices
            List<Index2i> pairs = new List<Index2i>();
            for ( int k = 0; k < N; ++k ) {
                int i0 = Loop0.FindVertexIndex(verts0[k]);
                int i1 = Loop1.FindVertexIndex(verts1[k]);
                pairs.Add(new Index2i(i0, i1));
            }

            // sort by increasing index in loop0 (arbitrary)
            pairs.Sort((pair1, pair2) => { return pair1.a.CompareTo(pair2.a); });

            // now construct spans
            List<span> new_spans = new List<span>();
            for ( int k = 0; k < pairs.Count; ++k ) {
                Index2i p1 = pairs[k];
                Index2i p2 = pairs[(k + 1) % pairs.Count];
                span s = new span() {
                    span0 = new Interval1i(p1.a, p2.a),
                    span1 = new Interval1i(p1.b, p2.b)
                };
                new_spans.Add(s);
            }
            spans = new_spans;
        }




        public bool Stitch()
        {
            if (spans.Count == 1)
                throw new Exception("MeshStitchLoops.Stitch: blind stitching not supported yet...");

            int gid = Group.GetGroupID(Mesh);

            bool all_ok = true;

            int NS = spans.Count;
            for ( int si = 0; si < NS; si++ ) {
                span s = spans[si];

                if (stitch_span_simple(s, gid) == false)
                    all_ok = false;
            }

            return all_ok;
        }



        /// <summary>
        /// this just does back-and-forth zippering, of as many quads as possible, and
        /// then a triangle-fan to finish whichever side is longer
        /// </summary>
        bool stitch_span_simple(span s, int gid)
        {
            bool all_ok = true;

            int N0 = Loop0.Vertices.Length;
            int N1 = Loop1.Vertices.Length;

            // stitch as many quads as we can
            int cur0 = s.span0.a, end0 = s.span0.b;
            int cur1 = s.span1.a, end1 = s.span1.b;
            while (cur0 != end0 && cur1 != end1) {
                int next0 = (cur0 + 1) % N0;
                int next1 = (cur1 + 1) % N1;

                int a = Loop0.Vertices[cur0], b = Loop0.Vertices[next0];
                int c = Loop1.Vertices[cur1], d = Loop1.Vertices[next1];
                if (add_triangle(b, a, c, gid) == false)
                    all_ok = false;
                if (add_triangle(c, d, b, gid) == false)
                    all_ok = false;

                cur0 = next0;
                cur1 = next1;
            }

            // now finish remaining verts on one side
            int last_c = Loop1.Vertices[cur1];
            while (cur0 != end0) {
                int next0 = (cur0 + 1) % N0;
                int a = Loop0.Vertices[cur0], b = Loop0.Vertices[next0];
                if (add_triangle(b, a, last_c, gid) == false)
                    all_ok = false;
                cur0 = next0;
            }

            // or the other (only one of these two loops will happen)
            int last_b = Loop0.Vertices[cur0];
            while (cur1 != end1) {
                int next1 = (cur1 + 1) % N1;
                int c = Loop1.Vertices[cur1], d = Loop1.Vertices[next1];
                if (add_triangle(c, d, last_b, gid) == false)
                    all_ok = false;
                cur1 = next1;
            }

            return all_ok;
        }




        bool add_triangle(int a, int b, int c, int gid)
        {
            int new_tid = DMesh3.InvalidID;
            if (TrustLoopOrientations == false) {
                int eid = Mesh.FindEdge(a, b);
                Index2i ab = Mesh.GetOrientedBoundaryEdgeV(eid);
                new_tid = Mesh.AppendTriangle(ab.b, ab.a, c, gid);
            } else {
                new_tid = Mesh.AppendTriangle(a, b, c, gid);
            }
            return (new_tid >= 0);
        }




    }
}
