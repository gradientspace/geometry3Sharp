using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class PlanarHoleFiller
    {
        public DMesh3 Mesh;

        public Vector3d PlaneOrigin;
        public Vector3d PlaneNormal;

        /// <summary>
        /// fill mesh will be tessellated to this length, set to
        /// double.MaxValue to use zero-length tessellation
        /// </summary>
        public double FillTargetEdgeLen = double.MaxValue;

        // these will be computed if you don't set them
        Vector3d PlaneX, PlaneY;

        class FillLoop
        {
            public EdgeLoop edgeLoop;
            public Polygon2d poly;
        }
        List<FillLoop> Loops = new List<FillLoop>();
        AxisAlignedBox2d Bounds;

        public PlanarHoleFiller(DMesh3 mesh)
        {
            Mesh = mesh;
            Bounds = AxisAlignedBox2d.Empty;
        }

        public PlanarHoleFiller(MeshPlaneCut cut)
        {
            Mesh = cut.Mesh;
            AddFillLoops(cut.CutLoops);
            SetPlane(cut.PlaneOrigin, cut.PlaneNormal);
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


        public void AddFillLoop(EdgeLoop loop)
        {
            Loops.Add(new FillLoop() { edgeLoop = loop });
        }

        public void AddFillLoops(IEnumerable<EdgeLoop> loops)
        {
            foreach (var loop in loops)
                AddFillLoop(loop);
        }


        public bool Fill()
        {
            compute_polygons();

            // translate/scale fill loops to unit box. This will improve
            // accuracy in the calcs below...
            Vector2d shiftOrigin = Bounds.Center;
            double scale = 1.0 / Bounds.MaxDim;
            foreach ( var floop in Loops ) {
                floop.poly.Translate(-shiftOrigin);
                floop.poly.Scale(scale * Vector2d.One, Vector2d.Zero);
            }

            Dictionary<PlanarComplex.Element, int> ElemToLoopMap = new Dictionary<PlanarComplex.Element, int>();

            // [TODO] if we have multiple components in input mesh, we could do this per-component.
            // This also helps avoid nested shells creating holes.
            // *However*, we shouldn't *have* to because FindSolidRegions will do the right thing if
            // the polygons have the same orientation

            // add all loops to planar complex
            PlanarComplex complex = new PlanarComplex();
            for (int i = 0; i < Loops.Count; ++i) {
                var elem = complex.Add(Loops[i].poly);
                ElemToLoopMap[elem] = i;
            }

            // sort into separate 2d solids
            PlanarComplex.SolidRegionInfo solids =
                complex.FindSolidRegions(PlanarComplex.FindSolidsOptions.SortPolygons);

            // fill each 2d solid
            List<Index2i> failed_inserts = new List<Index2i>();
            List<Index2i> failed_merges = new List<Index2i>();
            for ( int fi = 0; fi < solids.Polygons.Count; ++fi ) {
                var gpoly = solids.Polygons[fi];
                PlanarComplex.GeneralSolid gsolid = solids.PolygonsSources[fi];

                // [TODO] could do scale/translate here, per-polygon would be more precise

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

                // convenient list
                List<Polygon2d> polys = new List<Polygon2d>() { gpoly.Outer };
                polys.AddRange(gpoly.Holes);

                // for each poly, we track the set of vertices inserted into mesh
                int[][] polyVertices = new int[polys.Count][];

                // insert each poly
                for ( int pi = 0; pi < polys.Count; ++pi ) { 
                    MeshInsertUVPolyCurve insert = new MeshInsertUVPolyCurve(FillMesh, polys[pi]);
                    ValidationStatus status = insert.Validate(MathUtil.ZeroTolerancef * scale);
                    bool failed = true;
                    if (status == ValidationStatus.Ok) {
                        if (insert.Apply()) {
                            insert.Simplify();
                            polyVertices[pi] = insert.CurveVertices;
                            failed = false;
                        }
                    }
                    if (failed)
                        failed_inserts.Add(new Index2i(fi, pi));
                }

                // remove any triangles not contained in gpoly
                // [TODO] degenerate triangle handling? may be 'on' edge of gpoly...
                List<int> removeT = new List<int>();
                foreach (int tid in FillMesh.TriangleIndices()) {
                    Vector3d v = FillMesh.GetTriCentroid(tid);
                    if ( gpoly.Contains(v.xy) == false)
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
                for ( int pi = 0; pi < polys.Count; ++pi ) {
                    if (polyVertices[pi] == null)
                        continue;
                    int[] fillLoopVerts = polyVertices[pi];
                    int NV = fillLoopVerts.Length;

                    PlanarComplex.Element sourceElem = (pi == 0) ? gsolid.Outer : gsolid.Holes[pi - 1];
                    int loopi = ElemToLoopMap[sourceElem];
                    EdgeLoop sourceLoop = Loops[loopi].edgeLoop;

                    if (sourceLoop.VertexCount != NV) {
                        failed_merges.Add(new Index2i(fi, pi));
                        continue;
                    }

                    for ( int k = 0; k < NV; ++k ) {
                        Vector3d fillV = FillMesh.GetVertex(fillLoopVerts[k]);
                        Vector3d sourceV = Mesh.GetVertex(sourceLoop.Vertices[k]);
                        if (fillV.Distance(sourceV) < MathUtil.ZeroTolerancef)
                            mergeMapV[fillLoopVerts[k]] = sourceLoop.Vertices[k];
                    }

                }

                // append this fill to input mesh
                MeshEditor editor = new MeshEditor(Mesh);
                int[] mapV;
                editor.AppendMesh(FillMesh, mergeMapV, out mapV, Mesh.AllocateTriangleGroup());

                // [TODO] should verify that we actually merged the loops...
            }

            if (failed_inserts.Count > 0 || failed_merges.Count > 0)
                return false;

            return true;
        }



        void compute_polygons()
        {
            Bounds = AxisAlignedBox2d.Empty;
            for ( int i = 0; i < Loops.Count; ++i ) {
                EdgeLoop loop = Loops[i].edgeLoop;
                Polygon2d poly = new Polygon2d();

                foreach (int vid in loop.Vertices) {
                    Vector2d v = to2D(Mesh.GetVertex(vid));
                    poly.AppendVertex(v);
                }

                Loops[i].poly = poly;
                Bounds.Contain(poly.Bounds);
            }
        }



        bool inPolygon(Vector2d v2, List<GeneralPolygon2d> polys, bool all = false)
        {
            int inside = 0;
            foreach (var poly in polys) {
                if (poly.Contains(v2)) {
                    if (all)
                        inside++;
                    else
                        return true;
                }
            }
            if (all && inside == polys.Count)
                return true;
            return false;
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
