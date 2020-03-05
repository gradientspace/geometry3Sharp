// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// This class fills an ordered sequence of planar spans. The 2D polygon is formed
    /// by chaining the spans.
    /// 
    /// Current issues:
    ///    - connectors have a single segment, so when simplified, they become a single edge.
    ///      should subsample them instead.
    ///    - currently mapping from inserted edges back to span edges is not calculated, so
    ///      we have no way to merge them (ie MergeFillBoundary not implemented)
    ///    - fill triangles not returned?
    ///   
    /// 
    /// </summary>
    public class PlanarSpansFiller
    {
        public DMesh3 Mesh;

        public Vector3d PlaneOrigin;
        public Vector3d PlaneNormal;

        /// <summary>
        /// fill mesh will be tessellated to this length, set to
        /// double.MaxValue to use zero-length tessellation
        /// </summary>
        public double FillTargetEdgeLen = double.MaxValue;

        /// <summary>
        /// in some cases fill can succeed but we can't merge w/o creating holes. In
        /// such cases it might be better to not merge at all...
        /// </summary>
        public bool MergeFillBoundary = false;

        // these will be computed if you don't set them
        Vector3d PlaneX, PlaneY;


        List<EdgeSpan> FillSpans;
        Polygon2d SpansPoly;
        AxisAlignedBox2d Bounds;

        public PlanarSpansFiller(DMesh3 mesh, IList<EdgeSpan> spans)
        {
            Mesh = mesh;
            FillSpans = new List<EdgeSpan>(spans);
            Bounds = AxisAlignedBox2d.Empty;
        }

        public void SetPlane(Vector3d origin, Vector3d normal)
        {
            PlaneOrigin = origin;
            PlaneNormal = normal;
            Vector3d.ComputeOrthogonalComplement(1, PlaneNormal, ref PlaneX, ref PlaneY);
        }
        public void SetPlane(Vector3d origin, Vector3d normal, Vector3d planeX, Vector3d planeY)
        {
            PlaneOrigin = origin;
            PlaneNormal = normal;
            PlaneX = planeX;
            PlaneY = planeY;
        }


        public bool Fill()
        {
            compute_polygon();

            // translate/scale fill loops to unit box. This will improve
            // accuracy in the calcs below...
            Vector2d shiftOrigin = Bounds.Center;
            double scale = 1.0 / Bounds.MaxDim;
            SpansPoly.Translate(-shiftOrigin);
            SpansPoly.Scale(scale * Vector2d.One, Vector2d.Zero);

            Dictionary<PlanarComplex.Element, int> ElemToLoopMap = new Dictionary<PlanarComplex.Element, int>();

            // generate planar mesh that we will insert polygons into
            MeshGenerator meshgen;
            float planeW = 1.5f;
            int nDivisions = 0;
            if ( FillTargetEdgeLen < double.MaxValue && FillTargetEdgeLen > 0) {
                int n = (int)((planeW / (float)scale) / FillTargetEdgeLen) + 1;
                nDivisions = (n <= 1) ? 0 : n;
            }

            if (nDivisions == 0) {
                meshgen = new TrivialRectGenerator() {
                    IndicesMap = new Index2i(1, 2), Width = planeW, Height = planeW,
                };
            } else {
                meshgen = new GriddedRectGenerator() {
                    IndicesMap = new Index2i(1, 2), Width = planeW, Height = planeW,
                    EdgeVertices = nDivisions
                };
            }
            DMesh3 FillMesh = meshgen.Generate().MakeDMesh();
            FillMesh.ReverseOrientation();   // why?!?

            int[] polyVertices = null;

            // insert each poly
            MeshInsertUVPolyCurve insert = new MeshInsertUVPolyCurve(FillMesh, SpansPoly);
            ValidationStatus status = insert.Validate(MathUtil.ZeroTolerancef * scale);
            bool failed = true;
            if (status == ValidationStatus.Ok) {
                if (insert.Apply()) {
                    insert.Simplify();
                    polyVertices = insert.CurveVertices;
                    failed = false;
                }
            }
            if (failed)
                return false;
            
            // remove any triangles not contained in gpoly
            // [TODO] degenerate triangle handling? may be 'on' edge of gpoly...
            List<int> removeT = new List<int>();
            foreach (int tid in FillMesh.TriangleIndices()) {
                Vector3d v = FillMesh.GetTriCentroid(tid);
                if ( SpansPoly.Contains(v.xy) == false)
                    removeT.Add(tid);
            }
            foreach (int tid in removeT)
                FillMesh.RemoveTriangle(tid, true, false);

            //Util.WriteDebugMesh(FillMesh, "c:\\scratch\\CLIPPED_MESH.obj");

            // transform fill mesh back to 3d
            MeshTransforms.PerVertexTransform(FillMesh, (v) => {
                Vector2d v2 = v.xy;
                v2 /= scale;
                v2 += shiftOrigin;
                return to3D(v2);
            });


            //Util.WriteDebugMesh(FillMesh, "c:\\scratch\\PLANAR_MESH_WITH_LOOPS.obj");
            //Util.WriteDebugMesh(MeshEditor.Combine(FillMesh, Mesh), "c:\\scratch\\FILLED_MESH.obj");

            // figure out map between new mesh and original edge loops
            // [TODO] if # of verts is different, we can still find correspondence, it is just harder
            // [TODO] should check that edges (ie sequential verts) are boundary edges on fill mesh
            //    if not, can try to delete nbr tris to repair
            IndexMap mergeMapV = new IndexMap(true);
            if (MergeFillBoundary && polyVertices != null) {
                throw new NotImplementedException("PlanarSpansFiller: merge fill boundary not implemented!");

                //int[] fillLoopVerts = polyVertices;
                //int NV = fillLoopVerts.Length;

                //PlanarComplex.Element sourceElem = (pi == 0) ? gsolid.Outer : gsolid.Holes[pi - 1];
                //int loopi = ElemToLoopMap[sourceElem];
                //EdgeLoop sourceLoop = Loops[loopi].edgeLoop;

                //for (int k = 0; k < NV; ++k) {
                //    Vector3d fillV = FillMesh.GetVertex(fillLoopVerts[k]);
                //    Vector3d sourceV = Mesh.GetVertex(sourceLoop.Vertices[k]);
                //    if (fillV.Distance(sourceV) < MathUtil.ZeroTolerancef)
                //        mergeMapV[fillLoopVerts[k]] = sourceLoop.Vertices[k];
                //}
            }

            // append this fill to input mesh
            MeshEditor editor = new MeshEditor(Mesh);
            int[] mapV;
            editor.AppendMesh(FillMesh, mergeMapV, out mapV, Mesh.AllocateTriangleGroup());

            // [TODO] should verify that we actually merged the loops...

            return true;
        }



        void compute_polygon()
        {
            SpansPoly = new Polygon2d();
            for ( int i = 0; i < FillSpans.Count; ++i ) {
                foreach (int vid in FillSpans[i].Vertices) {
                    Vector2d v = to2D(Mesh.GetVertex(vid));
                    SpansPoly.AppendVertex(v);
                }
            }

            Bounds = SpansPoly.Bounds;
        }


        Vector2d to2D(Vector3d v)
        {
            Vector3d dv = v - PlaneOrigin;
            dv -= dv.Dot(PlaneNormal) * PlaneNormal;
            return new Vector2d(PlaneX.Dot(dv), PlaneY.Dot(dv));
        }

        Vector3d to3D(Vector2d v)
        {
            return PlaneOrigin + PlaneX * v.x + PlaneY * v.y;
        }

    }
}
