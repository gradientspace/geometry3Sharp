using System;
using System.Collections.Generic;
using System.Linq;

namespace g3
{

    /// <summary>
    /// 
    /// </summary>
    public class GraphSupportGenerator
    {
        public DMesh3 Mesh;
        public DMeshAABBTree3 MeshSpatial;

        // size of cubes in the grid
        public double CellSize;

        /// <summary>
        /// overhang angle requiring support
        /// </summary>
        public double OverhangAngleDeg = 30;

        /// <summary>
        /// If this is not set, 'ground' is Mesh.CachedBounds.Min.y
        /// (eg, if mesh is floating off ground, set this to 0, otherwise support stops at bottom of mesh)
        /// </summary>
        public float ForceMinY = float.MaxValue;

        /// <summary>
        /// Get *very* different graphs is we generate bottom-up vs top-down
        /// </summary>
        public bool ProcessBottomUp = false;

        /// <summary>
        /// Graph *vertices* will try to stay this far from surface
        /// </summary>
        public double GraphSurfaceDistanceOffset = 1.5;

        /// <summary>
        /// We will try to maintain at least this angle during graph optimization
        /// (does this need to be separate from the one above?)
        /// </summary>
        public double OverhangAngleOptimizeDeg = 25;

        /// <summary>
        /// optimization (ie smoothing) "speed"
        /// </summary>
        public double OptimizationAlpha = 1.0;

        /// <summary>
        /// graph smoothing rounds
        /// </summary>
        public int OptimizationRounds = 20;



        /// <summary>
        /// Set this to be able to cancel running remesher
        /// </summary>
        public ProgressCancel Progress = null;

        /// <summary>
        /// if this returns true, abort computation. 
        /// </summary>
        protected virtual bool Cancelled()
        {
            return (Progress == null) ? false : Progress.Cancelled();
        }



        public bool DebugPrint = false;

        // computed results
        Vector3f grid_origin;
        DenseGrid3f volume_grid;

        /*
         *  Results
         */

        public DGraph3 Graph;
        public HashSet<int> TipVertices;
        public HashSet<int> TipBaseVertices;
        public HashSet<int> GroundVertices;



        public GraphSupportGenerator(DMesh3 mesh, DMeshAABBTree3 spatial, double cellSize)
        {
            Mesh = mesh;
            MeshSpatial = spatial;
            CellSize = cellSize;
        }


        public GraphSupportGenerator(DMesh3 mesh, DMeshAABBTree3 spatial, int grid_resolution)
        {
            Mesh = mesh;
            MeshSpatial = spatial;

            double maxdim = Math.Max(Mesh.CachedBounds.Width, Mesh.CachedBounds.Height);
            CellSize = maxdim / grid_resolution;
        }


        public void Generate()
        {
            // figure out origin & dimensions
            AxisAlignedBox3d bounds = Mesh.CachedBounds;
            if (ForceMinY != float.MaxValue )
                bounds.Min.y = ForceMinY;

            // expand grid so we have some border space in x and z
            float fBufferWidth = 2 * (float)CellSize;
            Vector3f b = new Vector3f(fBufferWidth, 0, fBufferWidth);
            grid_origin = (Vector3f)bounds.Min - b;

            // need zero isovalue to be at y=0. right now we set yi=0 voxels to be -1, 
            // so if we nudge up half a cell, then interpolation with boundary outside 
            // value should be 0 right at cell border (seems to be working?)
            grid_origin.y += (float)CellSize * 0.5f;

            Vector3f max = (Vector3f)bounds.Max + b;
            int ni = (int)((max.x - grid_origin.x) / (float)CellSize) + 1;
            int nj = (int)((max.y - grid_origin.y) / (float)CellSize) + 1;
            int nk = (int)((max.z - grid_origin.z) / (float)CellSize) + 1;

            volume_grid = new DenseGrid3f();
            generate_support(grid_origin, (float)CellSize, ni, nj, nk, volume_grid);
        }



        public Vector3i Dimensions {
            get { return new Vector3i(volume_grid.ni, volume_grid.nj, volume_grid.nk); }
        }

        /// <summary>
        /// winding-number grid available after calling Compute()
        /// </summary>
        public DenseGrid3f Grid {
            get { return volume_grid; }
        }

        /// <summary>
        /// Origin of the winding-number grid, in same coordinates as mesh
        /// </summary>
        public Vector3f GridOrigin {
            get { return grid_origin; }
        }



        public float this[int i, int j, int k] {
            get { return volume_grid[i, j, k]; }
        }

        public Vector3f CellCenter(int i, int j, int k)
        {
            return new Vector3f((float)i * CellSize + grid_origin.x,
                                (float)j * CellSize + grid_origin.y,
                                (float)k * CellSize + grid_origin.z);
        }



        const float SUPPORT_GRID_USED = -1.0f;
        const float SUPPORT_TIP_TOP = -2.0f;
        const float SUPPORT_TIP_BASE = -3.0f;


        void generate_support(Vector3f origin, float dx,
                             int ni, int nj, int nk,
                             DenseGrid3f supportGrid)
        {
            supportGrid.resize(ni, nj, nk);
            supportGrid.assign(1); // sentinel

            if (DebugPrint) System.Console.WriteLine("start");

            bool CHECKERBOARD = false;


            System.Console.WriteLine("Computing SDF");

            // compute unsigned SDF
            MeshSignedDistanceGrid sdf = new MeshSignedDistanceGrid(Mesh, CellSize) {
                ComputeSigns = true, ExactBandWidth = 3,
                /*,ComputeMode = MeshSignedDistanceGrid.ComputeModes.FullGrid*/ };
            sdf.CancelF = Cancelled;
            sdf.Compute();
            if (Cancelled())
                return;
            var distanceField = new DenseGridTrilinearImplicit(sdf.Grid, sdf.GridOrigin, sdf.CellSize);


            double angle = MathUtil.Clamp(OverhangAngleDeg, 0.01, 89.99);
            double cos_thresh = Math.Cos(angle * MathUtil.Deg2Rad);


            System.Console.WriteLine("Marking overhangs");

            // Compute narrow-band distances. For each triangle, we find its grid-coord-bbox,
            // and compute exact distances within that box. The intersection_count grid
            // is also filled in this computation
            double ddx = (double)dx;
            double ox = (double)origin[0], oy = (double)origin[1], oz = (double)origin[2];
            Vector3d va = Vector3d.Zero, vb = Vector3d.Zero, vc = Vector3d.Zero;
            foreach (int tid in Mesh.TriangleIndices()) {
                if (tid % 100 == 0 && Cancelled())
                    break;

                Mesh.GetTriVertices(tid, ref va, ref vb, ref vc);
                Vector3d normal = MathUtil.Normal(ref va, ref vb, ref vc);
                if (normal.Dot(-Vector3d.AxisY) < cos_thresh)
                    continue;

                // real ijk coordinates of va/vb/vc
                double fip = (va[0] - ox) / ddx, fjp = (va[1] - oy) / ddx, fkp = (va[2] - oz) / ddx;
                double fiq = (vb[0] - ox) / ddx, fjq = (vb[1] - oy) / ddx, fkq = (vb[2] - oz) / ddx;
                double fir = (vc[0] - ox) / ddx, fjr = (vc[1] - oy) / ddx, fkr = (vc[2] - oz) / ddx;

                // clamped integer bounding box of triangle plus exact-band
                int exact_band = 0;
                int i0 = MathUtil.Clamp(((int)MathUtil.Min(fip, fiq, fir)) - exact_band, 0, ni - 1);
                int i1 = MathUtil.Clamp(((int)MathUtil.Max(fip, fiq, fir)) + exact_band + 1, 0, ni - 1);
                int j0 = MathUtil.Clamp(((int)MathUtil.Min(fjp, fjq, fjr)) - exact_band, 0, nj - 1);
                int j1 = MathUtil.Clamp(((int)MathUtil.Max(fjp, fjq, fjr)) + exact_band + 1, 0, nj - 1);
                int k0 = MathUtil.Clamp(((int)MathUtil.Min(fkp, fkq, fkr)) - exact_band, 0, nk - 1);
                int k1 = MathUtil.Clamp(((int)MathUtil.Max(fkp, fkq, fkr)) + exact_band + 1, 0, nk - 1);

                // don't put into y=0 plane
                if (j0 == 0)
                    j0 = 1;

                // compute distance for each tri inside this bounding box
                // note: this can be very conservative if the triangle is large and on diagonal to grid axes
                for (int k = k0; k <= k1; ++k) {
                    for (int j = j0; j <= j1; ++j) {
                        for (int i = i0; i <= i1; ++i) {
                            Vector3d gx = new Vector3d((float)i * dx + origin[0], (float)j * dx + origin[1], (float)k * dx + origin[2]);
                            float d = (float)MeshSignedDistanceGrid.point_triangle_distance(ref gx, ref va, ref vb, ref vc);

                            // vertical checkerboard pattern (eg 'tips')
                            if (CHECKERBOARD) {
                                int zz = (k % 2 == 0) ? 1 : 0;
                                if (i % 2 == zz)
                                    continue;
                            }

                            if (d < dx / 2) {
                                if (j > 1) { 
                                    supportGrid[i, j, k] = SUPPORT_TIP_TOP;
                                    supportGrid[i, j - 1, k] = SUPPORT_TIP_BASE;
                                } else {
                                    supportGrid[i, j, k] = SUPPORT_TIP_BASE;
                                }
                            }
                        }
                    }
                }
            }
            if (Cancelled())
                return;


            //process_version1(supportGrid, distanceField);
            //process_version2(supportGrid, distanceField);

            generate_graph(supportGrid, distanceField);
            //Util.WriteDebugMesh(MakeDebugGraphMesh(), "c:\\scratch\\__LAST_GRAPH_INIT.obj");

            postprocess_graph();
            //Util.WriteDebugMesh(MakeDebugGraphMesh(), "c:\\scratch\\__LAST_GRAPH_OPT.obj");
        }




        Vector3d get_cell_center(Vector3i ijk)
        {
            return new Vector3d(ijk.x * CellSize, ijk.y * CellSize, ijk.z * CellSize) + this.GridOrigin;
        }


        void generate_graph(DenseGrid3f supportGrid, DenseGridTrilinearImplicit distanceField)
        {
            int ni = supportGrid.ni, nj = supportGrid.nj, nk = supportGrid.nk;
            float dx = (float)CellSize;
            Vector3f origin = this.GridOrigin;

            // parameters for initializing cost grid
            float MODEL_SPACE = 0.01f;      // needs small positive so that points on triangles count as inside (eg on ground plane)
            //float MODEL_SPACE = 2.0f*(float)CellSize;
            float CRAZY_DISTANCE = 99999.0f;
            bool UNIFORM_DISTANCE = true;
            float MAX_DIST = 10 * (float)CellSize;


            // parameters for sorting seeds
            Vector3i center_idx = new Vector3i(ni / 2, 0, nk / 2);      // middle
            //Vector3i center_idx = new Vector3i(0, 0, 0);              // corner
            bool reverse_per_layer = true;


            DenseGrid3f costGrid = new DenseGrid3f(supportGrid);
            foreach ( Vector3i ijk in costGrid.Indices() ) {
                Vector3d cell_center = new Vector3f(ijk.x * dx, ijk.y * dx, ijk.z * dx) + origin;
                float f = (float)distanceField.Value(ref cell_center);
                if (f <= MODEL_SPACE)
                    f = CRAZY_DISTANCE;
                else if (UNIFORM_DISTANCE)
                    f = 1.0f;
                else if (f > MAX_DIST)
                    f = MAX_DIST;
                costGrid[ijk] = f;
            }

            // Find seeds on each layer, sort, and add to accumulated bottom-up seeds list.
            // This sorting has an *enormous* effect on the support generation.

            List<Vector3i> seeds = new List<Vector3i>();
            List<Vector3i> layer_seeds = new List<Vector3i>();
            for (int j = 0; j < nj; ++j) {
                layer_seeds.Clear();
                for (int k = 0; k < nk; ++k) {
                    for (int i = 0; i < ni; ++i) {
                        if (supportGrid[i, j, k] == SUPPORT_TIP_BASE)
                            layer_seeds.Add(new Vector3i(i, j, k));
                    }
                }

                layer_seeds.Sort((a, b) => {
                    Vector3i pa = a; pa.y = 0;
                    Vector3i pb = b; pb.y = 0;
                    int sa = (pa-center_idx).LengthSquared, sb = (pb-center_idx).LengthSquared;
                    return sa.CompareTo(sb);
                });

                // reversing sort order is intresting?
                if(reverse_per_layer)
                    layer_seeds.Reverse();

                seeds.AddRange(layer_seeds);
            }
            HashSet<Vector3i> seed_indices = new HashSet<Vector3i>(seeds);

            // gives very different results...
            if (ProcessBottomUp == false)
                seeds.Reverse();

            // for linear index a, is this a node we allow in graph? (ie graph bounds)
            Func<int, bool> node_filter_f = (a) => {
                Vector3i ai = costGrid.to_index(a);
                // why not y check??
                return ai.x > 0 &&  ai.z > 0 && ai.x != ni - 1 && ai.y != nj - 1 && ai.z != nk - 1;
            };

            // distance from linear index a to linear index b
            // this defines the cost field we want to find shortest path through
            Func<int, int, float> node_dist_f = (a, b) => {
                Vector3i ai = costGrid.to_index(a), bi = costGrid.to_index(b);
                if (bi.y >= ai.y)               // b.y should always be a.y-1
                    return float.MaxValue;      
                float sg = supportGrid[bi];

                // don't connect to tips
                //if (sg == SUPPORT_TIP_BASE || sg == SUPPORT_TIP_TOP)
                //    return float.MaxValue;
                if (sg == SUPPORT_TIP_TOP)
                    return float.MaxValue;
                
                if (sg < 0)
                    return -999999;    // if b is already used, we will terminate there, so this is a good choice

                // otherwise cost is sqr-grid-distance + costGrid value  (which is basically distance to surface)
                float c = costGrid[b];
                float f = (float)(Math.Sqrt((bi - ai).LengthSquared) * CellSize);
                //float f = 0;
                return c + f;
            };

            // which linear-index nbrs to consider for linear index a
            Func<int, IEnumerable<int>> neighbour_f = (a) => {
                Vector3i ai = costGrid.to_index(a);
                return down_neighbours(ai, costGrid);
            };

            // when do we terminate
            Func<int, bool> terminate_f = (a) => {
                Vector3i ai = costGrid.to_index(a);
                // terminate if we hit existing support path
                if (seed_indices.Contains(ai) == false && supportGrid[ai] < 0)
                    return true;
                // terminate if we hit ground plane
                if (ai.y == 0)
                    return true;
                return false;
            };

            DijkstraGraphDistance dijkstra = new DijkstraGraphDistance(ni * nj * nk, false,
                node_filter_f, node_dist_f, neighbour_f);
            dijkstra.TrackOrder = true;

            List<int> path = new List<int>();

            Graph = new DGraph3();
            Dictionary<Vector3i, int> CellToGraph = new Dictionary<Vector3i, int>();
            TipVertices = new HashSet<int>();
            TipBaseVertices = new HashSet<int>();
            GroundVertices = new HashSet<int>();

            // seeds are tip-base points
            for (int k = 0; k < seeds.Count; ++k) {
                // add seed point (which is a tip-base vertex) as seed for dijkstra prop
                int seed = costGrid.to_linear(seeds[k]);
                dijkstra.Reset();
                dijkstra.AddSeed(seed, 0);

                // compute to termination (ground, existing node, etc)
                int base_node = dijkstra.ComputeToNode(terminate_f);
                if (base_node < 0)
                    base_node = dijkstra.GetOrder().Last();

                // extract the path
                path.Clear();
                dijkstra.GetPathToSeed(base_node, path);
                int N = path.Count;

                // first point on path is termination point. 
                // create vertex for it if we have not yet
                Vector3i basept_idx = supportGrid.to_index(path[0]);
                int basept_vid;
                if ( CellToGraph.TryGetValue(basept_idx, out basept_vid) == false ) {
                    Vector3d curv = get_cell_center(basept_idx);
                    if (basept_idx.y == 0) {
                        curv.y = 0;
                    }
                    basept_vid = Graph.AppendVertex(curv);
                    if (basept_idx.y == 0) {
                        GroundVertices.Add(basept_vid);
                    }
                    CellToGraph[basept_idx] = basept_vid;
                } 

                int cur_vid = basept_vid;

                // now walk up path and create vertices as necessary
                for (int i = 0; i < N; ++i) {
                    int idx = path[i];
                    if ( supportGrid[idx] >= 0 )
                        supportGrid[idx] = SUPPORT_GRID_USED;
                    if ( i > 0 ) {
                        Vector3i next_idx = supportGrid.to_index(path[i]);
                        int next_vid;
                        if (CellToGraph.TryGetValue(next_idx, out next_vid) == false) {
                            Vector3d nextv = get_cell_center(next_idx);
                            next_vid = Graph.AppendVertex(nextv);
                            CellToGraph[next_idx] = next_vid;
                        }
                        Graph.AppendEdge(cur_vid, next_vid);
                        cur_vid = next_vid;
                    }
                }

                // seed was tip-base so we should always get back there. Then we
                // explicitly add tip-top and edge to it.
                if ( supportGrid[path[N-1]] == SUPPORT_TIP_BASE ) {
                    Vector3i vec_idx = supportGrid.to_index(path[N-1]);
                    TipBaseVertices.Add(CellToGraph[vec_idx]);

                    Vector3i tip_idx = vec_idx + Vector3i.AxisY;
                    int tip_vid;
                    if (CellToGraph.TryGetValue(tip_idx, out tip_vid) == false) {
                        Vector3d tipv = get_cell_center(tip_idx);
                        tip_vid = Graph.AppendVertex(tipv);
                        CellToGraph[tip_idx] = tip_vid;
                        Graph.AppendEdge(cur_vid, tip_vid);
                        TipVertices.Add(tip_vid);
                    }
                }

            }



            /*
             * Snap tips to surface
             */

            gParallel.ForEach(TipVertices, (tip_vid) => {
                bool snapped = false;
                Vector3d v = Graph.GetVertex(tip_vid);
                Frame3f hitF;
                // try shooting ray straight up. if that hits, and point is close, we use it
                if (MeshQueries.RayHitPointFrame(Mesh, MeshSpatial, new Ray3d(v, Vector3d.AxisY), out hitF)) {
                    if (v.Distance(hitF.Origin) < 2 * CellSize) {
                        v = hitF.Origin;
                        snapped = true;
                    }
                }

                // if that failed, try straight down
                if (!snapped) {
                    if (MeshQueries.RayHitPointFrame(Mesh, MeshSpatial, new Ray3d(v, -Vector3d.AxisY), out hitF)) {
                        if (v.Distance(hitF.Origin) < CellSize) {
                            v = hitF.Origin;
                            snapped = true;
                        }
                    }
                }

                // if it missed, or hit pt was too far, find nearest point and try that
                if (!snapped) {
                    hitF = MeshQueries.NearestPointFrame(Mesh, MeshSpatial, v);
                    if (v.Distance(hitF.Origin) < 2 * CellSize) {
                        v = hitF.Origin;
                        snapped = true;
                    }
                    // can this ever fail? tips should always be within 2 cells...
                }
                if (snapped)
                    Graph.SetVertex(tip_vid, v);
            });

        }





        protected DMesh3 MakeDebugGraphMesh()
        {
            DMesh3 graphMesh = new DMesh3();
            graphMesh.EnableVertexColors(Vector3f.One);
            foreach (int vid in Graph.VertexIndices()) {
                if (TipVertices.Contains(vid)) {
                    MeshEditor.AppendBox(graphMesh, Graph.GetVertex(vid), 0.3f, Colorf.Green);
                } else if (TipBaseVertices.Contains(vid)) {
                    MeshEditor.AppendBox(graphMesh, Graph.GetVertex(vid), 0.225f, Colorf.Magenta);
                } else if (GroundVertices.Contains(vid)) {
                    MeshEditor.AppendBox(graphMesh, Graph.GetVertex(vid), 0.35f, Colorf.Blue);
                } else {
                    MeshEditor.AppendBox(graphMesh, Graph.GetVertex(vid), 0.15f, Colorf.White);
                }
            }
            foreach (int eid in Graph.EdgeIndices()) {
                Segment3d seg = Graph.GetEdgeSegment(eid);
                MeshEditor.AppendLine(graphMesh, seg, 0.1f);
            }
            return graphMesh;
        }




        protected virtual void postprocess_graph()
        {
            double alpha = MathUtil.Clamp(OptimizationAlpha, 0.0, 1.0);
            if (alpha == 0 || OptimizationRounds == 0)
                return;
            constrained_smooth(Graph, 
                GraphSurfaceDistanceOffset, 
                Math.Cos((90.0-OverhangAngleOptimizeDeg) * MathUtil.Deg2Rad), 
                alpha, OptimizationRounds);
        }




        IEnumerable<int> down_neighbours(Vector3i idx, DenseGrid3f grid)
        {
            yield return grid.to_linear(idx.x, idx.y - 1, idx.z);
            yield return grid.to_linear(idx.x-1, idx.y - 1, idx.z);
            yield return grid.to_linear(idx.x+1, idx.y - 1, idx.z);
            yield return grid.to_linear(idx.x, idx.y - 1, idx.z-1);
            yield return grid.to_linear(idx.x, idx.y - 1, idx.z+1);
            yield return grid.to_linear(idx.x-1, idx.y - 1, idx.z-1);
            yield return grid.to_linear(idx.x+1, idx.y - 1, idx.z-1);
            yield return grid.to_linear(idx.x-1, idx.y - 1, idx.z+1);
            yield return grid.to_linear(idx.x+1, idx.y - 1, idx.z+1);
        }


        void constrained_smooth(DGraph3 graph, double surfDist, double dotThresh, double alpha, int rounds)
        {
            int NV = graph.MaxVertexID;
            Vector3d[] pos = new Vector3d[NV];

            for (int ri = 0; ri < rounds; ++ri) {

                gParallel.ForEach(graph.VertexIndices(), (vid) => {
                    Vector3d v = graph.GetVertex(vid);

                    if ( GroundVertices.Contains(vid) || TipVertices.Contains(vid) ) {
                        pos[vid] = v;
                        return;
                    }

                    // for tip base vertices, we could allow them to move down and away within angle cone...
                    if (TipBaseVertices.Contains(vid)) {
                        pos[vid] = v;
                        return;
                    }


                    // compute smoothed position of vtx
                    Vector3d centroid = Vector3d.Zero; int nbr_count = 0;
                    foreach (int nbr_vid in graph.VtxVerticesItr(vid)) {
                        centroid += graph.GetVertex(nbr_vid);
                        nbr_count++;
                    }
                    if (nbr_count == 1) {
                        pos[vid] = v;
                        return;
                    }
                    centroid /= nbr_count;
                    Vector3d vnew = (1 - alpha) * v + (alpha) * centroid;

                    // make sure we don't violate angle constraint to any nbrs
                    int attempt = 0;
                    try_again:
                    foreach ( int nbr_vid in graph.VtxVerticesItr(vid)) {
                        Vector3d dv = graph.GetVertex(nbr_vid) - vnew;
                        dv.Normalize();
                        double dot = dv.Dot(Vector3d.AxisY);
                        if ( Math.Abs(dot) < dotThresh ) {
                            if (attempt++ < 3) {
                                vnew = Vector3d.Lerp(v, vnew, 0.66);
                                goto try_again;
                            } else {
                                pos[vid] = v;
                                return;
                            }
                        }
                    }

                    // offset from nearest point on surface
                    Frame3f fNearest = MeshQueries.NearestPointFrame(Mesh, MeshSpatial, vnew, true);
                    Vector3d vNearest = fNearest.Origin;
                    double dist = vnew.Distance(vNearest);
                    bool inside = MeshSpatial.IsInside(vnew);

                    if (inside || dist < surfDist) {
                        Vector3d normal = fNearest.Z;
                        // don't push down?
                        if (normal.Dot(Vector3d.AxisY) < 0) {
                            normal.y = 0; normal.Normalize();
                        }
                        vnew = fNearest.Origin + surfDist * normal;
                    }


                    pos[vid] = vnew;
                });

                foreach (int vid in graph.VertexIndices())
                    graph.SetVertex(vid, pos[vid]);
            }
        }




        /// <summary>
        /// Implicit tube around line segment
        /// </summary>
        public class ImplicitCurve3d : BoundedImplicitFunction3d
        {
            public DCurve3 Curve;
            public double Radius;
            public AxisAlignedBox3d Box;

            DCurve3BoxTree spatial;

            public ImplicitCurve3d(DCurve3 curve, double radius)
            {
                Curve = curve;
                Radius = radius;
                Box = curve.GetBoundingBox();
                Box.Expand(Radius);
                spatial = new DCurve3BoxTree(curve);
            }

            public double Value(ref Vector3d pt)
            {
                double d = spatial.Distance(pt);
                return d - Radius;
            }

            public AxisAlignedBox3d Bounds()
            {
                return Box;
            }
        }










        void process_version2(DenseGrid3f supportGrid, DenseGridTrilinearImplicit distanceField)
        {
            int ni = supportGrid.ni, nj = supportGrid.nj, nk = supportGrid.nk;
            float dx = (float)CellSize;
            Vector3f origin = this.GridOrigin;

            // sweep values down layer by layer
            DenseGrid2f prev = supportGrid.get_slice(nj - 1, 1);
            DenseGrid2f tmp = new DenseGrid2f(prev);

            Bitmap3 bmp = new Bitmap3(new Vector3i(ni, nj, nk));

            for (int j = nj - 2; j >= 0; j--) {

                // skeletonize prev layer
                DenseGrid2i prev_skel = binarize(prev, 0.0f);
                skeletonize(prev_skel, null, 2);
                //dilate_loners(prev_skel, null, 2);

                if (j == 0) {
                    dilate(prev_skel, null, true);
                    dilate(prev_skel, null, true);
                }

                for (int k = 1; k < nk - 1; ++k) {
                    for (int i = 1; i < ni - 1; ++i) 
                        bmp[new Vector3i(i,j,k)] = (prev_skel[i, k] == 1) ? true : false;
                }

                smooth(prev, tmp, 0.5f, 5);

                DenseGrid2f cur = supportGrid.get_slice(j, 1);
                cur.set_min(prev);

                for (int k = 1; k < nk - 1; ++k) {
                    for (int i = 1; i < ni - 1; ++i) {
                        float skelf = prev_skel[i, k] > 0 ? -1.0f : int.MaxValue;
                        cur[i, k] = Math.Min(cur[i, k], skelf);

                        if (cur[i, k] < 0) {
                            Vector3d cell_center = new Vector3f(i * dx, j * dx, k * dx) + origin;
                            if (distanceField.Value(ref cell_center) < -CellSize)
                                cur[i, k] = 1;
                        }
                    }
                }

                for (int k = 1; k < nk - 1; ++k) {
                    for (int i = 1; i < ni - 1; ++i) {
                        if (is_loner(prev_skel, i, k)) {
                            foreach (Vector2i d in gIndices.GridOffsets8) {
                                float f = 1.0f / (float)Math.Sqrt(d.x*d.x + d.y*d.y);
                                cur[i + d.x, k + d.y] += -0.25f * f;
                            }
                        }
                    }
                }

                for (int k = 1; k < nk - 1; ++k) {
                    for (int i = 1; i < ni - 1; ++i) {
                        supportGrid[i, j, k] = cur[i, k];
                    }
                }

                prev.swap(cur);
            }


            VoxelSurfaceGenerator gen = new VoxelSurfaceGenerator() { Voxels = bmp };
            gen.Generate();
            Util.WriteDebugMesh(gen.Meshes[0], "c:\\scratch\\binary.obj");

        }



        static DenseGrid2i binarize(DenseGrid2f grid, float thresh = 0)
        {
            DenseGrid2i result = new DenseGrid2i();
            result.resize(grid.ni, grid.nj);
            int size = result.size;
            for (int k = 0; k < size; ++k)
                result[k] = (grid[k] < thresh) ? 1 : 0;
            return result;
        }
        static DenseGrid3i binarize(DenseGrid3f grid, float thresh = 0)
        {
            DenseGrid3i result = new DenseGrid3i();
            result.resize(grid.ni, grid.nj, grid.nk);
            int size = result.size;
            for (int k = 0; k < size; ++k)
                result[k] = (grid[k] < thresh) ? 1 : 0;
            return result;
        }


        static void smooth(DenseGrid2f grid, DenseGrid2f tmp, float alpha, int rounds)
        {
            if (tmp == null)
                tmp = new DenseGrid2f(grid.ni, grid.nj, 0);

            int ni = grid.ni, nj = grid.nj;
            for (int k = 0; k < rounds; ++k) {
                tmp.assign_border(1, 1);
                for (int j = 1; j < nj - 1; ++j) {
                    for (int i = 1; i < ni - 1; ++i) {
                        float avg = grid[i - 1, j] + grid[i - 1, j + 1]
                                    + grid[i, j + 1] + grid[i + 1, j + 1]
                                    + grid[i + 1, j] + grid[i + 1, j - 1]
                                    + grid[i, j - 1] + grid[i - 1, j - 1];
                        avg /= 8.0f;
                        tmp[i, j] = (1 - alpha) * grid[i, j] + (alpha) * avg;
                    }
                }
                grid.copy(tmp);
            }
        }






        void process_version1(DenseGrid3f supportGrid, DenseGridTrilinearImplicit distanceField)
        {
            int ni = supportGrid.ni, nj = supportGrid.nj, nk = supportGrid.nk;
            float dx = (float)CellSize;
            Vector3f origin = this.GridOrigin;

            // sweep values down, column by column
            for (int k = 0; k < nk; ++k) {
                for (int i = 0; i < ni; ++i) {
                    bool in_support = false;
                    for (int j = nj - 1; j >= 0; j--) {
                        float fcur = supportGrid[i, j, k];
                        if (fcur >= 0) {
                            Vector3d cell_center = new Vector3f(i * dx, j * dx, k * dx) + origin;
                            if (in_support) {
                                bool is_inside = distanceField.Value(ref cell_center) < 0;
                                if (is_inside) {
                                    supportGrid[i, j, k] = -3;
                                    in_support = false;
                                } else {
                                    supportGrid[i, j, k] = -1;
                                }
                            }
                        } else {
                            in_support = true;
                        }
                    }
                }
            }


            // skeletonize each layer 
            // todo: would be nice to skeletonize the 3D volume.. ?
            DenseGrid3i binary = new DenseGrid3i(ni, nj, nk, 0);
            foreach (Vector3i idx in binary.Indices())
                binary[idx] = (supportGrid[idx] < 0) ? 1 : 0;
            for (int j = 0; j < nj; ++j)
                skeletonize_layer(binary, j);


            // debug thing
            //VoxelSurfaceGenerator voxgen = new VoxelSurfaceGenerator() {
            //    Voxels = binary.get_bitmap()
            //};
            //voxgen.Generate();
            //Util.WriteDebugMesh(voxgen.makemesh(), "c:\\scratch\\binary.obj");


            // for skeleton voxels, we add some power 
            for (int j = 0; j < nj; ++j) {
                for (int k = 1; k < nk - 1; ++k) {
                    for (int i = 1; i < ni - 1; ++i) {
                        if (binary[i, j, k] > 0)
                            supportGrid[i, j, k] = -3;
                        //else
                        //    supportGrid[i, j, k] = 1;   // clear non-skeleton voxels
                    }
                }
            }


            // power up the ground-plane voxels
            for (int k = 0; k < nk; ++k) {
                for (int i = 0; i < ni; ++i) {
                    if (supportGrid[i, 0, k] < 0)
                        supportGrid[i, 0, k] = -5;
                }
            }


#if true
            DenseGrid3f smoothed = new DenseGrid3f(supportGrid);
            float nbr_weight = 0.5f;
            for (int iter = 0; iter < 15; ++iter) {

                // add some mass to skeleton voxels
                for (int j = 0; j < nj; ++j) {
                    for (int k = 1; k < nk - 1; ++k) {
                        for (int i = 1; i < ni - 1; ++i) {
                            if (binary[i, j, k] > 0)
                                supportGrid[i, j, k] = supportGrid[i, j, k] - nbr_weight / 25.0f;
                        }
                    }
                }

                for (int j = 0; j < nj; ++j) {
                    for (int k = 1; k < nk - 1; ++k) {
                        for (int i = 1; i < ni - 1; ++i) {
                            int neg = 0;
                            float avg = 0, w = 0;
                            for (int n = 0; n < 8; ++n) {
                                int xi = i + gIndices.GridOffsets8[n].x;
                                int zi = k + gIndices.GridOffsets8[n].y;
                                float f = supportGrid[xi, j, zi];
                                if (f < 0) neg++;
                                avg += nbr_weight * f;
                                w += nbr_weight;
                            }
                            if (neg > -1) {
                                avg += supportGrid[i, j, k];
                                w += 1.0f;
                                smoothed[i, j, k] = avg / w;
                            } else {
                                smoothed[i, j, k] = supportGrid[i, j, k];
                            }
                        }
                    }
                }
                supportGrid.swap(smoothed);
            }
#endif


            // hard-enforce that skeleton voxels stay inside
            //for (int j = 0; j < nj; ++j) {
            //    for (int k = 1; k < nk - 1; ++k) {
            //        for (int i = 1; i < ni - 1; ++i) {
            //            if (binary[i, j, k] > 0)
            //                supportGrid[i, j, k] = Math.Min(supportGrid[i, j, k], - 1);
            //        }
            //    }
            //}

        }












        static void skeletonize_pass(DenseGrid2i grid, DenseGrid2i tmp, int iter)
        {
            int ni = grid.ni, nj = grid.nj;

            for (int i = 1; i < ni - 1; i++) {
                for (int j = 1; j < nj - 1; j++) {
                    int p2 = grid[i - 1, j];
                    int p3 = grid[i - 1, j + 1];
                    int p4 = grid[i, j + 1];
                    int p5 = grid[i + 1, j + 1];
                    int p6 = grid[i + 1, j];
                    int p7 = grid[i + 1, j - 1];
                    int p8 = grid[i, j - 1];
                    int p9 = grid[i - 1, j - 1];
                    int A =   ((p2 == 0 && p3 == 1) ? 1 : 0)
                            + ((p3 == 0 && p4 == 1) ? 1 : 0)
                            + ((p4 == 0 && p5 == 1) ? 1 : 0)
                            + ((p5 == 0 && p6 == 1) ? 1 : 0)
                            + ((p6 == 0 && p7 == 1) ? 1 : 0)
                            + ((p7 == 0 && p8 == 1) ? 1 : 0)
                            + ((p8 == 0 && p9 == 1) ? 1 : 0)
                            + ((p9 == 0 && p2 == 1) ? 1 : 0);
                    int B = p2 + p3 + p4 + p5 + p6 + p7 + p8 + p9;
                    int m1 = iter == 0 ? (p2 * p4 * p6) : (p2 * p4 * p8);
                    int m2 = iter == 0 ? (p4 * p6 * p8) : (p2 * p6 * p8);
                    if (A == 1 && B >= 2 && B <= 6 && m1 == 0 && m2 == 0) {
                        tmp[i, j] = 1;
                    }
                }
            }

            for (int i = 0; i < ni; ++i)
                for (int j = 0; j < nj; ++j)
                    grid[i,j] = grid[i, j] & ~tmp[i, j];
        }



        static void dilate(DenseGrid2i grid, DenseGrid2i tmp, bool corners = true)
        {
            if (tmp == null)
                tmp = new DenseGrid2i(grid.ni, grid.nj, 0);

            int ni = grid.ni, nj = grid.nj;

            for (int i = 1; i < ni - 1; i++) {
                for (int j = 1; j < nj - 1; j++) {
                    if ( grid[i,j] == 1 ) {
                        tmp[i, j] = 1;
                        tmp[i - 1, j] = 1;
                        tmp[i, j + 1] = 1;
                        tmp[i + 1, j] = 1;
                        tmp[i, j - 1] = 1;
                        if (corners) {
                            tmp[i - 1, j + 1] = 1;
                            tmp[i + 1, j + 1] = 1;
                            tmp[i + 1, j - 1] = 1;
                            tmp[i - 1, j - 1] = 1;
                        }
                    }
                }
            }
            grid.copy(tmp);
        }



        static void dilate_loners(DenseGrid2i grid, DenseGrid2i tmp, int mode)
        {
            if (tmp == null)
                tmp = new DenseGrid2i(grid.ni, grid.nj, 0);

            int ni = grid.ni, nj = grid.nj;

            for (int i = 1; i < ni - 1; i++) {
                for (int j = 1; j < nj - 1; j++) {
                    if (grid[i, j] == 1) {
                        tmp[i, j] = 1;

                        int nbrs =
                          grid[i - 1, j] + grid[i - 1, j + 1]
                        + grid[i, j + 1] + grid[i + 1, j + 1]
                        + grid[i + 1, j] + grid[i + 1, j - 1]
                        + grid[i, j - 1] + grid[i - 1, j - 1];

                        if (nbrs == 0) {
                            if (mode != 3) {
                                tmp[i - 1, j] = 1;
                                tmp[i + 1, j] = 1;
                                tmp[i, j + 1] = 1;
                                tmp[i, j - 1] = 1;
                            }
                            if (mode == 2 || mode == 3) {
                                tmp[i - 1, j + 1] = 1;
                                tmp[i + 1, j + 1] = 1;
                                tmp[i + 1, j - 1] = 1;
                                tmp[i - 1, j - 1] = 1;
                            }
                        }
                    }
                }
            }
            grid.copy(tmp);
        }

        

        bool is_loner(DenseGrid2i grid, int i, int j)
        {
            if (grid[i, j] == 0)
                return false;
            int nbrs =
              grid[i - 1, j] + grid[i - 1, j + 1]
            + grid[i, j + 1] + grid[i + 1, j + 1]
            + grid[i + 1, j] + grid[i + 1, j - 1]
            + grid[i, j - 1] + grid[i - 1, j - 1];
            return nbrs == 0;
        }


        static void skeletonize(DenseGrid2i grid, DenseGrid2i tmp, int dilation_rounds = 1)
        {
            if ( tmp == null )
                tmp = new DenseGrid2i(grid.ni, grid.nj, 0);

            for (int k = 0; k < dilation_rounds; ++k) {
                tmp.clear();
                dilate(grid, tmp);
            }

            bool done = false;
            while (!done) {
                int sum_before = grid.sum();
                tmp.clear();
                skeletonize_pass(grid, tmp, 0);
                tmp.clear();
                skeletonize_pass(grid, tmp, 1);
                int sum_after = grid.sum();
                if (sum_before == sum_after)
                    break;
            }
        }




        static void diffuse(DenseGrid2f grid, float t, Func<int, int, bool> skipF)
        {
            int ni = grid.ni, nj = grid.nj;
            DenseGrid2f dilated = new DenseGrid2f(grid);
            for (int j = 1; j < nj - 1; ++j) {
                for (int i = 1; i < ni - 1; ++i) {
                    if (skipF != null && skipF(i, j))
                        continue;
                    if (grid[i, j] < 0) {
                        foreach (Vector2i o in gIndices.GridOffsets8) {
                            float df = (o.LengthSquared > 1) ? -1f : -0.707f;
                            df *= t;
                            dilated[i + o.x, j + o.y] = Math.Min(dilated[i + o.x, j + o.y], df);
                            //dilated[i + o.x, j + o.y] += df;
                        }
                    }
                }
            }
            grid.swap(dilated);
        }



        static void skeletonize_layer(DenseGrid3i grid, int j, int dilation_rounds = 1)
        {
            DenseGrid2i layer = grid.get_slice(j, 1);
            DenseGrid2i tmp = new DenseGrid2i(layer.ni, layer.nj, 0);

            for (int k = 0; k < dilation_rounds; ++k) {
                tmp.assign(0);
                dilate(layer, tmp);
            }

            bool done = false;
            while (!done) {
                int sum_before = layer.sum();
                tmp.assign(0);
                skeletonize_pass(layer, tmp, 0);
                tmp.assign(0);
                skeletonize_pass(layer, tmp, 1);
                int sum_after = layer.sum();
                if (sum_before == sum_after)
                    break;
            }

            for (int i = 0; i < grid.ni; ++i)
                for (int k = 0; k < grid.nk; ++k)
                    grid[i, j, k] = layer[i, k];
        }








        static void smooth(DenseGrid3f grid, DenseGrid3f tmp, float alpha, int iters, int min_j = 1)
        {
            if ( tmp == null )
                tmp = new DenseGrid3f(grid);

            int ni = grid.ni, nj = grid.nj, nk = grid.nk;

            for (int iter = 0; iter < iters; ++iter) {

                for (int j = min_j; j < nj-1; ++j) {
                    for (int k = 1; k < nk - 1; ++k) {
                        for (int i = 1; i < ni - 1; ++i) {

                            float avg = 0;
                            foreach (Vector3i o in gIndices.GridOffsets26) {
                                int xi = i + o.x, yi = j + o.y, zi = k + o.z;
                                float f = grid[xi, yi, zi];
                                avg += f;
                            }
                            avg /= 26.0f;
                            tmp[i, j, k] = (1 - alpha) * grid[i, j, k] + (alpha) * avg;
                        }
                    }
                }

                grid.swap(tmp);
            }
        }


    }
}
