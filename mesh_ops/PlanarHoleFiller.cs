using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// Try to fill planar holes in a mesh. The fill is computed by mapping the hole boundary into 2D,
    /// filling using 2D algorithms, and then mapping back to 3D. This allows us to properly handle cases like
    /// nested holes (eg from slicing a torus in half). 
    /// 
    /// PlanarComplex is used to sort the input 2D polyons. 
    /// 
    /// MeshInsertUVPolyCurve is used to insert each 2D polygon into a generated planar mesh.
    /// The resolution of the generated mesh is controlled by .FillTargetEdgeLen
    /// 
    /// In theory this approach can handle more geometric degeneracies than Delaunay triangluation.
    /// However, the current code requires that MeshInsertUVPolyCurve produce output boundary loops that
    /// have a 1-1 correspondence with the input polygons. This is not always possible.
    /// 
    /// Currently these failure cases are not handled properly. In that case the loops will
    /// not be stitched.
    /// 
    /// </summary>
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

        /// <summary>
        /// in some cases fill can succeed but we can't merge w/o creating holes. In
        /// such cases it might be better to not merge at all...
        /// </summary>
        public bool MergeFillBoundary = true;


        /*
         * Error feedback
         */
        public bool OutputHasCracks = false;
        public int FailedInsertions = 0;
        public int FailedMerges = 0;

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


        /// <summary>
        /// Compute the fill mesh and append it.
        /// This returns false if anything went wrong. 
        /// The Error Feedback properties (.OutputHasCracks, etc) will provide more info.
        /// </summary>
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
                            failed = (insert.Loops.Count != 1) ||
                                     (insert.Loops[0].VertexCount != polys[pi].VertexCount);
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
                // [TODO] should check that edges (ie sequential verts) are boundary edges on fill mesh
                //    if not, can try to delete nbr tris to repair
                IndexMap mergeMapV = new IndexMap(true);
                if (MergeFillBoundary) {
                    for (int pi = 0; pi < polys.Count; ++pi) {
                        if (polyVertices[pi] == null)
                            continue;
                        int[] fillLoopVerts = polyVertices[pi];
                        int NV = fillLoopVerts.Length;

                        PlanarComplex.Element sourceElem = (pi == 0) ? gsolid.Outer : gsolid.Holes[pi - 1];
                        int loopi = ElemToLoopMap[sourceElem];
                        EdgeLoop sourceLoop = Loops[loopi].edgeLoop;

                        // construct vertex-merge map for this loop
                        List<int> bad_indices = build_merge_map(FillMesh, fillLoopVerts, Mesh, sourceLoop.Vertices,
                            MathUtil.ZeroTolerancef, mergeMapV);

                        bool errors = (bad_indices != null && bad_indices.Count > 0);
                        if (errors) {
                            failed_inserts.Add(new Index2i(fi, pi));
                            OutputHasCracks = true;
                        }
                    }
                }

                // append this fill to input mesh
                MeshEditor editor = new MeshEditor(Mesh);
                int[] mapV;
                editor.AppendMesh(FillMesh, mergeMapV, out mapV, Mesh.AllocateTriangleGroup());

                // [TODO] should verify that we actually merged all the loops. If there are bad_indices
                // we could fill them
            }

            FailedInsertions = failed_inserts.Count;
            FailedMerges = failed_merges.Count;
            if (failed_inserts.Count > 0 || failed_merges.Count > 0)
                return false;

            return true;
        }




        /// <summary>
        /// Construct vertex correspondences between fill mesh boundary loop
        /// and input mesh boundary loop. In ideal case there is an easy 1-1 
        /// correspondence. If that is not true, then do a brute-force search
        /// to find the best correspondences we can.
        /// 
        /// Currently only returns unique correspondences. If any vertex
        /// matches with multiple input vertices it is not merged. 
        /// [TODO] we could do better in many cases...
        /// 
        /// Return value is list of indices into fillLoopV that were not merged
        /// </summary>
        List<int> build_merge_map(DMesh3 fillMesh, int[] fillLoopV,
                             DMesh3 targetMesh, int[] targetLoopV,
                             double tol, IndexMap mergeMapV)
        {
            if (fillLoopV.Length == targetLoopV.Length) {
                if (build_merge_map_simple(fillMesh, fillLoopV, targetMesh, targetLoopV, tol, mergeMapV))
                    return null;
            }

            int NF = fillLoopV.Length, NT = targetLoopV.Length;
            bool[] doneF = new bool[NF], doneT = new bool[NT];
            int[] countF = new int[NF], countT = new int[NT];
            List<int> errorV = new List<int>();

            SmallListSet matchF = new SmallListSet(); matchF.Resize(NF);

            // find correspondences
            double tol_sqr = tol*tol;
            for (int i = 0; i < NF; ++i ) {
                if ( fillMesh.IsVertex(fillLoopV[i]) == false ) {
                    doneF[i] = true;
                    errorV.Add(i);
                    continue;
                }
                matchF.AllocateAt(i);
                Vector3d v = fillMesh.GetVertex(fillLoopV[i]);
                for ( int j = 0; j < NT; ++j ) {
                    Vector3d v2 = targetMesh.GetVertex(targetLoopV[j]);
                    if ( v.DistanceSquared(ref v2) < tol_sqr ) {
                        matchF.Insert(i, j);
                    }
                }
            }

            for ( int i = 0; i < NF; ++i ) {
                if (doneF[i]) continue;
                if ( matchF.Count(i) == 1 ) {
                    int j = matchF.First(i);
                    mergeMapV[fillLoopV[i]] = targetLoopV[j];
                    doneF[i] = true;
                }
            }

            for ( int i = 0; i < NF; ++i ) {
                if (doneF[i] == false)
                    errorV.Add(i);
            }

            return errorV;
        }




        /// <summary>
        /// verifies that there is a 1-1 correspondence between the fill and target loops.
        /// If so, adds to mergeMapV and returns true;
        /// </summary>
        bool build_merge_map_simple(DMesh3 fillMesh, int[] fillLoopV, 
                                    DMesh3 targetMesh, int[] targetLoopV, 
                                    double tol, IndexMap mergeMapV )
        {
            if (fillLoopV.Length != targetLoopV.Length)
                return false;
            int NV = fillLoopV.Length;
            for (int k = 0; k < NV; ++k) {
                if (!fillMesh.IsVertex(fillLoopV[k]))
                    return false;
                Vector3d fillV = fillMesh.GetVertex(fillLoopV[k]);
                Vector3d sourceV = Mesh.GetVertex(targetLoopV[k]);
                if (fillV.Distance(sourceV) > tol)
                    return false;
            }
            for (int k = 0; k < NV; ++k)
                mergeMapV[fillLoopV[k]] = targetLoopV[k];
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
