using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;



namespace g3
{

    /// <summary>
    /// Compute discretely-sampled (ie gridded) signed distance field for a mesh
    /// The basic approach is, first compute exact distances in a narrow band, and then
    /// extend out to rest of grid using fast "sweeping" (ie like a distance transform).
    /// The resulting unsigned grid is then signed using ray-intersection counting, which
    /// is also computed on the grid, so no BVH is necessary
    /// 
    /// If you set ComputeMode to NarrowBandOnly, result is a narrow-band signed distance field.
    /// This is quite a bit faster as the sweeping is the most computationally-intensive step.
    /// 
    /// Caveats:
    ///  - the "narrow band" is based on triangle bounding boxes, so it is not necessarily
    ///    that "narrow" if you have large triangles on a diagonal to grid axes
    /// 
    /// 
    /// Potential optimizations:
    ///  - Often we have a spatial data structure that would allow faster computation of the
    ///    narrow-band distances (which become quite expensive if we want a wider band!)
    ///    Not clear how to take advantage of this though. Perhaps we could have a binary
    ///    grid that, in first pass, we set bits inside triangle bboxes to 1? Or perhaps
    ///    same as current code, but we use spatial-dist, and so for each ijk we only compute once?
    ///    (then have to test for computed value at each cell of each triangle...)
    ///    
    /// 
    /// This code is based on the C++ implementation found at https://github.com/christopherbatty/SDFGen
    /// Original license was public domain. 
    /// Permission granted by Christopher Batty to include C# port under Boost license.
    /// </summary>
    public class MeshSignedDistanceGrid
    {
        public DMesh3 Mesh;
        public DMeshAABBTree3 Spatial;
        public float CellSize;

        // Width of the band around triangles for which exact distances are computed
        // (In fact this is conservative, the band is often larger locally)
        public int ExactBandWidth = 1;

        // Bounds of grid will be expanded this much in positive and negative directions.
        // Useful for if you want field to extend outwards.
        public Vector3d ExpandBounds = Vector3d.Zero;

        // Most of this parallelizes very well, makes a huge speed difference
        public bool UseParallel = true;

        // The narrow band is always computed exactly, and the full grid is always signed.
        // Can also fill in the rest of the full grid with fast sweeping. This is 
        // quite computationally intensive, though, and not parallelizable 
        // (time only depends on grid resolution)
        public enum ComputeModes
        {
            FullGrid = 0,
            NarrowBandOnly = 1,
            NarrowBand_SpatialFloodFill = 2
        }
        public ComputeModes ComputeMode = ComputeModes.NarrowBandOnly;

        // how wide of narrow band should we compute. This value is 
        // currently only used if there is a spatial data structure, as
        // we can efficiently explore the space (in that case ExactBandWidth is not used)
        public double NarrowBandMaxDistance = 0;

        // should we try to compute signs? if not, grid remains unsigned
        public bool ComputeSigns = true;

        // What counts as "inside" the mesh. Crossing count does not use triangle
        // orientation, so inverted faces are fine, but overlapping shells or self intersections
        // will be filled using even/odd rules (as seen along X axis...)
        // Parity count is basically mesh winding number, handles overlap shells and
        // self-intersections, but inverted shells are 'subtracted', and inverted faces are a disaster.
        // Both modes handle internal cavities, neither handles open sheets.
        public enum InsideModes
        {
            CrossingCount = 0,
            ParityCount = 1
        }
        public InsideModes InsideMode = InsideModes.ParityCount;

        // Implementation computes the triangle closest to each grid cell, can
        // return this grid if desired (only reason not to is avoid hanging onto memory)
        public bool WantClosestTriGrid = false;

        // grid of per-cell crossing or parity counts
        public bool WantIntersectionsGrid = false;

        /// <summary> if this function returns true, we should abort calculation </summary>
        public Func<bool> CancelF = () => { return false; };


        public bool DebugPrint = false;


        // computed results
        Vector3f grid_origin;
        DenseGrid3f grid;
        DenseGrid3i closest_tri_grid;
        DenseGrid3i intersections_grid;

        public MeshSignedDistanceGrid(DMesh3 mesh, double cellSize, DMeshAABBTree3 spatial = null)
        {
            Mesh = mesh;
            CellSize = (float)cellSize;
            Spatial = spatial;
        }


        public void Compute()
        {
            // figure out origin & dimensions
            AxisAlignedBox3d bounds = Mesh.CachedBounds;

            float fBufferWidth = 2 * ExactBandWidth * CellSize;
            if (ComputeMode == ComputeModes.NarrowBand_SpatialFloodFill)
                fBufferWidth = (float)Math.Max(fBufferWidth, 2 * NarrowBandMaxDistance);
            grid_origin = (Vector3f)bounds.Min - fBufferWidth * Vector3f.One - (Vector3f)ExpandBounds;
            Vector3f max = (Vector3f)bounds.Max + fBufferWidth * Vector3f.One + (Vector3f)ExpandBounds;
            int ni = (int)((max.x - grid_origin.x) / CellSize) + 1;
            int nj = (int)((max.y - grid_origin.y) / CellSize) + 1;
            int nk = (int)((max.z - grid_origin.z) / CellSize) + 1;

            grid = new DenseGrid3f();
            if (ComputeMode == ComputeModes.NarrowBand_SpatialFloodFill) {
                if (Spatial == null || NarrowBandMaxDistance == 0 || UseParallel == false)
                    throw new Exception("MeshSignedDistanceGrid.Compute: must set Spatial data structure and band max distance, and UseParallel=true");
                make_level_set3_parallel_floodfill(grid_origin, CellSize, ni, nj, nk, grid, ExactBandWidth);

            } else {
                if (UseParallel) {
                    if (Spatial != null) {
                        make_level_set3_parallel_spatial(grid_origin, CellSize, ni, nj, nk, grid, ExactBandWidth);
                    } else {
                        make_level_set3_parallel(grid_origin, CellSize, ni, nj, nk, grid, ExactBandWidth);
                    }
                } else {
                    make_level_set3(grid_origin, CellSize, ni, nj, nk, grid, ExactBandWidth);
                }
            }
        }



        public Vector3i Dimensions {
            get { return new Vector3i(grid.ni, grid.nj, grid.nk); }
        }

        /// <summary>
        /// SDF grid available after calling Compute()
        /// </summary>
        public DenseGrid3f Grid {
            get { return grid; }
        }

        /// <summary>
        /// Origin of the SDF grid, in same coordinates as mesh
        /// </summary>
        public Vector3f GridOrigin {
            get { return grid_origin; }
        }


        public DenseGrid3i ClosestTriGrid {
            get {
                if ( WantClosestTriGrid == false)
                    throw new Exception("Set WantClosestTriGrid=true to return this value");
                return closest_tri_grid;
            }
        }
        public DenseGrid3i IntersectionsGrid {
            get {
                if (WantIntersectionsGrid == false)
                    throw new Exception("Set WantIntersectionsGrid=true to return this value");
                return intersections_grid;
            }
        }


        public float this[int i, int j, int k] {
            get { return grid[i, j, k]; }
        }
        public float this[Vector3i idx] {
            get { return grid[idx.x, idx.y, idx.z]; }
        }

        public Vector3f CellCenter(int i, int j, int k) {
            return cell_center(new Vector3i(i, j, k));
        }
        Vector3f cell_center(Vector3i ijk)
        {
            return new Vector3f((float)ijk.x * CellSize + grid_origin[0],
                                (float)ijk.y * CellSize + grid_origin[1],
                                (float)ijk.z * CellSize + grid_origin[2]);
        }

        float upper_bound(DenseGrid3f grid)
        {
            return (float)((grid.ni + grid.nj + grid.nk) * CellSize);
        }

        float cell_tri_dist(Vector3i idx, int tid)
        {
            Vector3d xp = Vector3d.Zero, xq = Vector3d.Zero, xr = Vector3d.Zero;
            Vector3d c = cell_center(idx);
            Mesh.GetTriVertices(tid, ref xp, ref xq, ref xr);
            return (float)point_triangle_distance(ref c, ref xp, ref xq, ref xr);
        }




        void make_level_set3(Vector3f origin, float dx,
                             int ni, int nj, int nk,
                             DenseGrid3f distances, int exact_band)
        {
            distances.resize(ni, nj, nk);
            distances.assign(upper_bound(distances)); // upper bound on distance

            // closest triangle id for each grid cell
            DenseGrid3i closest_tri = new DenseGrid3i(ni, nj, nk, -1);

            // intersection_count(i,j,k) is # of tri intersections in (i-1,i]x{j}x{k}
            DenseGrid3i intersection_count = new DenseGrid3i(ni, nj, nk, 0); 
                                                                             
            if (DebugPrint) System.Console.WriteLine("start");

            // Compute narrow-band distances. For each triangle, we find its grid-coord-bbox,
            // and compute exact distances within that box. The intersection_count grid
            // is also filled in this computation
            double ddx = (double)dx;
            double ox = (double)origin[0], oy = (double)origin[1], oz = (double)origin[2];
            Vector3d xp = Vector3d.Zero, xq = Vector3d.Zero, xr = Vector3d.Zero;
            foreach (int tid in Mesh.TriangleIndices()) {
                if (tid % 100 == 0 && CancelF())
                    break;
                Mesh.GetTriVertices(tid, ref xp, ref xq, ref xr);

                // real ijk coordinates of xp/xq/xr
                double fip = (xp[0] - ox) / ddx, fjp = (xp[1] - oy) / ddx, fkp = (xp[2] - oz) / ddx;
                double fiq = (xq[0] - ox) / ddx, fjq = (xq[1] - oy) / ddx, fkq = (xq[2] - oz) / ddx;
                double fir = (xr[0] - ox) / ddx, fjr = (xr[1] - oy) / ddx, fkr = (xr[2] - oz) / ddx;

                // clamped integer bounding box of triangle plus exact-band
                int i0 = MathUtil.Clamp(((int)MathUtil.Min(fip, fiq, fir)) - exact_band, 0, ni - 1);
                int i1 = MathUtil.Clamp(((int)MathUtil.Max(fip, fiq, fir)) + exact_band + 1, 0, ni - 1);
                int j0 = MathUtil.Clamp(((int)MathUtil.Min(fjp, fjq, fjr)) - exact_band, 0, nj - 1);
                int j1 = MathUtil.Clamp(((int)MathUtil.Max(fjp, fjq, fjr)) + exact_band + 1, 0, nj - 1);
                int k0 = MathUtil.Clamp(((int)MathUtil.Min(fkp, fkq, fkr)) - exact_band, 0, nk - 1);
                int k1 = MathUtil.Clamp(((int)MathUtil.Max(fkp, fkq, fkr)) + exact_band + 1, 0, nk - 1);

                // compute distance for each tri inside this bounding box
                // note: this can be very conservative if the triangle is large and on diagonal to grid axes
                for (int k = k0; k <= k1; ++k) {
                    for (int j = j0; j <= j1; ++j) {
                        for (int i = i0; i <= i1; ++i) {
                            Vector3d gx = new Vector3d((float)i * dx + origin[0], (float)j * dx + origin[1], (float)k * dx + origin[2]);
                            float d = (float)point_triangle_distance(ref gx, ref xp, ref xq, ref xr);
                            if (d < distances[i, j, k]) {
                                distances[i, j, k] = d;
                                closest_tri[i, j, k] = tid;
                            }
                        }
                    }
                }
            }
            if (CancelF())
                return;

            if (ComputeSigns == true) {

                if (DebugPrint) System.Console.WriteLine("done narrow-band");

                compute_intersections(origin, dx, ni, nj, nk, intersection_count);
                if (CancelF())
                    return;

                if (DebugPrint) System.Console.WriteLine("done intersections");

                if (ComputeMode == ComputeModes.FullGrid) {
                    // and now we fill in the rest of the distances with fast sweeping
                    for (int pass = 0; pass < 2; ++pass) {
                        sweep_pass(origin, dx, distances, closest_tri);
                        if (CancelF())
                            return;
                    }
                        if (DebugPrint) System.Console.WriteLine("done sweeping");
                } else {
                    // nothing!
                    if (DebugPrint) System.Console.WriteLine("skipped sweeping");
                }


                // then figure out signs (inside/outside) from intersection counts
                compute_signs(ni, nj, nk, distances, intersection_count);
                if (CancelF())
                    return;

                if (DebugPrint) System.Console.WriteLine("done signs");

                if (WantIntersectionsGrid)
                    intersections_grid = intersection_count;
            }

            if (WantClosestTriGrid)
                closest_tri_grid = closest_tri;

        }   // end make_level_set_3






        void make_level_set3_parallel(Vector3f origin, float dx,
                             int ni, int nj, int nk,
                             DenseGrid3f distances, int exact_band)
        {
            distances.resize(ni, nj, nk);
            distances.assign(upper_bound(grid)); // upper bound on distance

            // closest triangle id for each grid cell
            DenseGrid3i closest_tri = new DenseGrid3i(ni, nj, nk, -1);

            // intersection_count(i,j,k) is # of tri intersections in (i-1,i]x{j}x{k}
            DenseGrid3i intersection_count = new DenseGrid3i(ni, nj, nk, 0);

            if (DebugPrint) System.Console.WriteLine("start");

            double ox = (double)origin[0], oy = (double)origin[1], oz = (double)origin[2];
            double invdx = 1.0 / dx;

            // Compute narrow-band distances. For each triangle, we find its grid-coord-bbox,
            // and compute exact distances within that box.

            // To compute in parallel, we need to safely update grid cells. Current strategy is
            // to use a spinlock to control access to grid. Partitioning the grid into a few regions,
            // each w/ a separate spinlock, improves performance somewhat. Have also tried having a
            // separate spinlock per-row, this resulted in a few-percent performance improvement.
            // Also tried pre-sorting triangles into disjoint regions, this did not help much except
            // on "perfect" cases like a sphere. 
            int wi = ni / 2, wj = nj / 2, wk = nk / 2;
            SpinLock[] grid_locks = new SpinLock[8];

            bool abort = false;
            gParallel.ForEach(Mesh.TriangleIndices(), (tid) => {
                if (tid % 100 == 0)
                    abort = CancelF();
                if (abort)
                    return;

                Vector3d xp = Vector3d.Zero, xq = Vector3d.Zero, xr = Vector3d.Zero;
                Mesh.GetTriVertices(tid, ref xp, ref xq, ref xr);

                // real ijk coordinates of xp/xq/xr
                double fip = (xp[0] - ox) * invdx, fjp = (xp[1] - oy) * invdx, fkp = (xp[2] - oz) * invdx;
                double fiq = (xq[0] - ox) * invdx, fjq = (xq[1] - oy) * invdx, fkq = (xq[2] - oz) * invdx;
                double fir = (xr[0] - ox) * invdx, fjr = (xr[1] - oy) * invdx, fkr = (xr[2] - oz) * invdx;

                // clamped integer bounding box of triangle plus exact-band
                int i0 = MathUtil.Clamp(((int)MathUtil.Min(fip, fiq, fir)) - exact_band, 0, ni - 1);
                int i1 = MathUtil.Clamp(((int)MathUtil.Max(fip, fiq, fir)) + exact_band + 1, 0, ni - 1);
                int j0 = MathUtil.Clamp(((int)MathUtil.Min(fjp, fjq, fjr)) - exact_band, 0, nj - 1);
                int j1 = MathUtil.Clamp(((int)MathUtil.Max(fjp, fjq, fjr)) + exact_band + 1, 0, nj - 1);
                int k0 = MathUtil.Clamp(((int)MathUtil.Min(fkp, fkq, fkr)) - exact_band, 0, nk - 1);
                int k1 = MathUtil.Clamp(((int)MathUtil.Max(fkp, fkq, fkr)) + exact_band + 1, 0, nk - 1);

                // compute distance for each tri inside this bounding box
                // note: this can be very conservative if the triangle is large and on diagonal to grid axes
                for (int k = k0; k <= k1; ++k) {
                    for (int j = j0; j <= j1; ++j) {
                        int base_idx = ((j < wj) ? 0 : 1) | ((k < wk) ? 0 : 2);    // construct index into spinlocks array

                        for (int i = i0; i <= i1; ++i) {
                            Vector3d gx = new Vector3d((float)i * dx + origin[0], (float)j * dx + origin[1], (float)k * dx + origin[2]);
                            float d = (float)point_triangle_distance(ref gx, ref xp, ref xq, ref xr);
                            if (d < distances[i, j, k]) {
                                int lock_idx = base_idx | ((i < wi) ? 0 : 4);
                                bool taken = false;
                                grid_locks[lock_idx].Enter(ref taken);
                                if (d < distances[i, j, k]  ) {    // have to check again in case grid changed in another thread...
                                    distances[i, j, k] = d;
                                    closest_tri[i, j, k] = tid;
                                }
                                grid_locks[lock_idx].Exit();
                            }
                        }

                    }
                }
            });
            if (DebugPrint) System.Console.WriteLine("done narrow-band");
            if (CancelF())
                return;


            if (ComputeSigns == true) {

                compute_intersections(origin, dx, ni, nj, nk, intersection_count);
                if (CancelF())
                    return;

                if (DebugPrint) System.Console.WriteLine("done intersections");

                if (ComputeMode == ComputeModes.FullGrid) {
                    // and now we fill in the rest of the distances with fast sweeping
                    for (int pass = 0; pass < 2; ++pass) {
                        sweep_pass(origin, dx, distances, closest_tri);
                        if (CancelF())
                            return;
                    }
                    if (DebugPrint) System.Console.WriteLine("done sweeping");
                } else {
                    // nothing!
                    if (DebugPrint) System.Console.WriteLine("skipped sweeping");
                }

                if (DebugPrint) System.Console.WriteLine("done sweeping");

                // then figure out signs (inside/outside) from intersection counts
                compute_signs(ni, nj, nk, distances, intersection_count);
                if (CancelF())
                    return;

                if (WantIntersectionsGrid)
                    intersections_grid = intersection_count;

                if (DebugPrint) System.Console.WriteLine("done signs");
            }

            if (WantClosestTriGrid)
                closest_tri_grid = closest_tri;

        }   // end make_level_set_3





        void make_level_set3_parallel_spatial(Vector3f origin, float dx,
                             int ni, int nj, int nk,
                             DenseGrid3f distances, int exact_band)
        {
            distances.resize(ni, nj, nk);
            float upper_bound = this.upper_bound(distances);
            distances.assign(upper_bound); // upper bound on distance

            // closest triangle id for each grid cell
            DenseGrid3i closest_tri = new DenseGrid3i(ni, nj, nk, -1);

            // intersection_count(i,j,k) is # of tri intersections in (i-1,i]x{j}x{k}
            DenseGrid3i intersection_count = new DenseGrid3i(ni, nj, nk, 0);

            if (DebugPrint) System.Console.WriteLine("start");

            double ox = (double)origin[0], oy = (double)origin[1], oz = (double)origin[2];
            double invdx = 1.0 / dx;

            // Compute narrow-band distances. For each triangle, we find its grid-coord-bbox,
            // and compute exact distances within that box.

            // To compute in parallel, we need to safely update grid cells. Current strategy is
            // to use a spinlock to control access to grid. Partitioning the grid into a few regions,
            // each w/ a separate spinlock, improves performance somewhat. Have also tried having a
            // separate spinlock per-row, this resulted in a few-percent performance improvement.
            // Also tried pre-sorting triangles into disjoint regions, this did not help much except
            // on "perfect" cases like a sphere. 
            bool abort = false;
            gParallel.ForEach(Mesh.TriangleIndices(), (tid) => {
                if (tid % 100 == 0)
                    abort = CancelF();
                if (abort)
                    return;

                Vector3d xp = Vector3d.Zero, xq = Vector3d.Zero, xr = Vector3d.Zero;
                Mesh.GetTriVertices(tid, ref xp, ref xq, ref xr);

                // real ijk coordinates of xp/xq/xr
                double fip = (xp[0] - ox) * invdx, fjp = (xp[1] - oy) * invdx, fkp = (xp[2] - oz) * invdx;
                double fiq = (xq[0] - ox) * invdx, fjq = (xq[1] - oy) * invdx, fkq = (xq[2] - oz) * invdx;
                double fir = (xr[0] - ox) * invdx, fjr = (xr[1] - oy) * invdx, fkr = (xr[2] - oz) * invdx;

                // clamped integer bounding box of triangle plus exact-band
                int i0 = MathUtil.Clamp(((int)MathUtil.Min(fip, fiq, fir)) - exact_band, 0, ni - 1);
                int i1 = MathUtil.Clamp(((int)MathUtil.Max(fip, fiq, fir)) + exact_band + 1, 0, ni - 1);
                int j0 = MathUtil.Clamp(((int)MathUtil.Min(fjp, fjq, fjr)) - exact_band, 0, nj - 1);
                int j1 = MathUtil.Clamp(((int)MathUtil.Max(fjp, fjq, fjr)) + exact_band + 1, 0, nj - 1);
                int k0 = MathUtil.Clamp(((int)MathUtil.Min(fkp, fkq, fkr)) - exact_band, 0, nk - 1);
                int k1 = MathUtil.Clamp(((int)MathUtil.Max(fkp, fkq, fkr)) + exact_band + 1, 0, nk - 1);

                // compute distance for each tri inside this bounding box
                // note: this can be very conservative if the triangle is large and on diagonal to grid axes
                for (int k = k0; k <= k1; ++k) {
                    for (int j = j0; j <= j1; ++j) {
                        for (int i = i0; i <= i1; ++i) {
                            distances[i, j, k] = 1;
                        }
                    }
                }
            });


            if (DebugPrint) System.Console.WriteLine("done narrow-band tagging");

            double max_dist = exact_band * (dx * MathUtil.SqrtTwo);
            gParallel.ForEach(grid.Indices(), (idx) => {
                if ( distances[idx] == 1 ) {
                    int i = idx.x, j = idx.y, k = idx.z;
                    Vector3d p = new Vector3d((float)i * dx + origin[0], (float)j * dx + origin[1], (float)k * dx + origin[2]);
                    int near_tid = Spatial.FindNearestTriangle(p, max_dist);
                    if ( near_tid == DMesh3.InvalidID ) {
                        distances[idx] = upper_bound;
                        return;
                    }
                    Triangle3d tri = new Triangle3d();
                    Mesh.GetTriVertices(near_tid, ref tri.V0, ref tri.V1, ref tri.V2);
                    Vector3d closest = new Vector3d(), bary = new Vector3d();
                    double dsqr = DistPoint3Triangle3.DistanceSqr(ref p, ref tri, out closest, out bary);
                    distances[idx] = (float)Math.Sqrt(dsqr);
                    closest_tri[idx] = near_tid;
                }
            });


            if (DebugPrint) System.Console.WriteLine("done distances");


            if (CancelF())
                return;

            if (ComputeSigns == true) {

                if (DebugPrint) System.Console.WriteLine("done narrow-band");

                compute_intersections(origin, dx, ni, nj, nk, intersection_count);
                if (CancelF())
                    return;

                if (DebugPrint) System.Console.WriteLine("done intersections");

                if (ComputeMode == ComputeModes.FullGrid) {
                    // and now we fill in the rest of the distances with fast sweeping
                    for (int pass = 0; pass < 2; ++pass) {
                        sweep_pass(origin, dx, distances, closest_tri);
                        if (CancelF())
                            return;
                    }
                    if (DebugPrint) System.Console.WriteLine("done sweeping");
                } else {
                    // nothing!
                    if (DebugPrint) System.Console.WriteLine("skipped sweeping");
                }

                if (DebugPrint) System.Console.WriteLine("done sweeping");

                // then figure out signs (inside/outside) from intersection counts
                compute_signs(ni, nj, nk, distances, intersection_count);
                if (CancelF())
                    return;

                if (WantIntersectionsGrid)
                    intersections_grid = intersection_count;

                if (DebugPrint) System.Console.WriteLine("done signs");
            }

            if (WantClosestTriGrid)
                closest_tri_grid = closest_tri;

        }   // end make_level_set_3














        void make_level_set3_parallel_floodfill(Vector3f origin, float dx,
                             int ni, int nj, int nk,
                             DenseGrid3f distances, int exact_band)
        {
            distances.resize(ni, nj, nk);
            float upper_bound = this.upper_bound(distances);
            distances.assign(upper_bound); // upper bound on distance

            // closest triangle id for each grid cell
            DenseGrid3i closest_tri = new DenseGrid3i(ni, nj, nk, -1);

            // intersection_count(i,j,k) is # of tri intersections in (i-1,i]x{j}x{k}
            DenseGrid3i intersection_count = new DenseGrid3i(ni, nj, nk, 0);

            if (DebugPrint) System.Console.WriteLine("start");

            double ox = (double)origin[0], oy = (double)origin[1], oz = (double)origin[2];
            double invdx = 1.0 / dx;

            // compute values at vertices

            SpinLock grid_lock = new SpinLock();
            List<int> Q = new List<int>();
            bool[] done = new bool[distances.size];

            bool abort = false;
            gParallel.ForEach(Mesh.VertexIndices(), (vid) => {
                if (vid % 100 == 0) abort = CancelF();
                if (abort) return;

                Vector3d v = Mesh.GetVertex(vid);
                // real ijk coordinates of v
                double fi = (v.x-ox)*invdx, fj = (v.y-oy)*invdx, fk = (v.z-oz)*invdx;
                Vector3i idx = new Vector3i(
                    MathUtil.Clamp((int)fi, 0, ni - 1),
                    MathUtil.Clamp((int)fj, 0, nj - 1),
                    MathUtil.Clamp((int)fk, 0, nk - 1));

                if (distances[idx] < upper_bound)
                    return;

                bool taken = false;
                grid_lock.Enter(ref taken);

                Vector3d p = cell_center(idx);
                int near_tid = Spatial.FindNearestTriangle(p);
                Triangle3d tri = new Triangle3d();
                Mesh.GetTriVertices(near_tid, ref tri.V0, ref tri.V1, ref tri.V2);
                Vector3d closest = new Vector3d(), bary = new Vector3d();
                double dsqr = DistPoint3Triangle3.DistanceSqr(ref p, ref tri, out closest, out bary);
                distances[idx] = (float)Math.Sqrt(dsqr);
                closest_tri[idx] = near_tid;
                int idx_linear = distances.to_linear(ref idx);
                Q.Add(idx_linear);
                done[idx_linear] = true;
                grid_lock.Exit();
            });
            if (DebugPrint) System.Console.WriteLine("done vertices");
            if (CancelF())
                return;

            // we could do this parallel w/ some kind of producer-consumer...
            List<int> next_Q = new List<int>();
            AxisAlignedBox3i bounds = distances.BoundsInclusive;
            double max_dist = NarrowBandMaxDistance; 
            double max_query_dist = max_dist + (2*dx*MathUtil.SqrtTwo);
            int next_pass_count = Q.Count;
            while (next_pass_count > 0) {

                next_Q.Clear();
                gParallel.ForEach(Q, (cur_linear_index) => {
                    Vector3i cur_idx = distances.to_index(cur_linear_index);
                    foreach (Vector3i idx_offset in gIndices.GridOffsets26) {
                        Vector3i nbr_idx = cur_idx + idx_offset;
                        if (bounds.Contains(nbr_idx) == false)
                            continue;
                        int nbr_linear_idx = distances.to_linear(ref nbr_idx);
                        if (done[nbr_linear_idx])
                            continue;

                        Vector3d p = cell_center(nbr_idx);
                        int near_tid = Spatial.FindNearestTriangle(p, max_query_dist);
                        if (near_tid == -1) {
                            done[nbr_linear_idx] = true;
                            continue;
                        }

                        Triangle3d tri = new Triangle3d();
                        Mesh.GetTriVertices(near_tid, ref tri.V0, ref tri.V1, ref tri.V2);
                        Vector3d closest = new Vector3d(), bary = new Vector3d();
                        double dsqr = DistPoint3Triangle3.DistanceSqr(ref p, ref tri, out closest, out bary);
                        double dist = Math.Sqrt(dsqr);

                        bool taken = false;
                        grid_lock.Enter(ref taken);
                        if (done[nbr_linear_idx] == false) {
                            distances[nbr_linear_idx] = (float)dist;
                            closest_tri[nbr_linear_idx] = near_tid;
                            done[nbr_linear_idx] = true;
                            if (dist < max_dist) 
                                next_Q.Add(nbr_linear_idx);
                        }
                        grid_lock.Exit();
                    }
                });
                // swap lists
                var tmp = Q; Q = next_Q; next_Q = tmp;
                next_pass_count = Q.Count;
            }
            if (DebugPrint) System.Console.WriteLine("done floodfill");
            if (CancelF())
                return;


            if (ComputeSigns == true) {

                if (DebugPrint) System.Console.WriteLine("done narrow-band");

                compute_intersections(origin, dx, ni, nj, nk, intersection_count);
                if (CancelF())
                    return;

                if (DebugPrint) System.Console.WriteLine("done intersections");

                if (ComputeMode == ComputeModes.FullGrid) {
                    // and now we fill in the rest of the distances with fast sweeping
                    for (int pass = 0; pass < 2; ++pass) {
                        sweep_pass(origin, dx, distances, closest_tri);
                        if (CancelF())
                            return;
                    }
                    if (DebugPrint) System.Console.WriteLine("done sweeping");
                } else {
                    // nothing!
                    if (DebugPrint) System.Console.WriteLine("skipped sweeping");
                }

                if (DebugPrint) System.Console.WriteLine("done sweeping");

                // then figure out signs (inside/outside) from intersection counts
                compute_signs(ni, nj, nk, distances, intersection_count);
                if (CancelF())
                    return;

                if (WantIntersectionsGrid)
                    intersections_grid = intersection_count;

                if (DebugPrint) System.Console.WriteLine("done signs");
            }

            if (WantClosestTriGrid)
                closest_tri_grid = closest_tri;

        }   // end make_level_set_3



      









        // sweep through grid in different directions, distances and closest tris
        void sweep_pass(Vector3f origin, float dx,
                        DenseGrid3f distances, DenseGrid3i closest_tri)
        {
            sweep(distances, closest_tri, origin, dx, +1, +1, +1);
            if (CancelF()) return;
            sweep(distances, closest_tri, origin, dx, -1, -1, -1);
            if (CancelF()) return;
            sweep(distances, closest_tri, origin, dx, +1, +1, -1);
            if (CancelF()) return;
            sweep(distances, closest_tri, origin, dx, -1, -1, +1);
            if (CancelF()) return;
            sweep(distances, closest_tri, origin, dx, +1, -1, +1);
            if (CancelF()) return;
            sweep(distances, closest_tri, origin, dx, -1, +1, -1);
            if (CancelF()) return;
            sweep(distances, closest_tri, origin, dx, +1, -1, -1);
            if (CancelF()) return;
            sweep(distances, closest_tri, origin, dx, -1, +1, +1);
        }


        // single sweep pass
        void sweep(DenseGrid3f phi, DenseGrid3i closest_tri, 
                   Vector3f origin, float dx,
                   int di, int dj, int dk)
        {
            int i0, i1;
            if (di > 0) { i0 = 1; i1 = phi.ni; } else { i0 = phi.ni - 2; i1 = -1; }
            int j0, j1;
            if (dj > 0) { j0 = 1; j1 = phi.nj; } else { j0 = phi.nj - 2; j1 = -1; }
            int k0, k1;
            if (dk > 0) { k0 = 1; k1 = phi.nk; } else { k0 = phi.nk - 2; k1 = -1; }
            for (int k = k0; k != k1; k += dk) {
                if (CancelF()) return;
                for (int j = j0; j != j1; j += dj) {
                    for (int i = i0; i != i1; i += di) {
                        Vector3d gx = new Vector3d(i * dx + origin[0], j * dx + origin[1], k * dx + origin[2]);
                        check_neighbour(phi, closest_tri, ref gx, i, j, k, i - di, j, k);
                        check_neighbour(phi, closest_tri, ref gx, i, j, k, i, j - dj, k);
                        check_neighbour(phi, closest_tri, ref gx, i, j, k, i - di, j - dj, k);
                        check_neighbour(phi, closest_tri, ref gx, i, j, k, i, j, k - dk);
                        check_neighbour(phi, closest_tri, ref gx, i, j, k, i - di, j, k - dk);
                        check_neighbour(phi, closest_tri, ref gx, i, j, k, i, j - dj, k - dk);
                        check_neighbour(phi, closest_tri, ref gx, i, j, k, i - di, j - dj, k - dk);
                    }
                }
            }
        }



        void check_neighbour(DenseGrid3f phi, DenseGrid3i closest_tri,
                             ref Vector3d gx, int i0, int j0, int k0, int i1, int j1, int k1)
        {
            if (closest_tri[i1, j1, k1] >= 0) {
                Vector3d xp = Vector3f.Zero, xq = Vector3f.Zero, xr = Vector3f.Zero;
                Mesh.GetTriVertices(closest_tri[i1, j1, k1], ref xp, ref xq, ref xr);
                float d = (float)point_triangle_distance(ref gx, ref xp, ref xq, ref xr);
                if (d < phi[i0, j0, k0]) {
                    phi[i0, j0, k0] = d;
                    closest_tri[i0, j0, k0] = closest_tri[i1, j1, k1];
                }
            }
        }




        // fill the intersection grid w/ number of intersections in each cell
        void compute_intersections(Vector3f origin, float dx, int ni, int nj, int nk, DenseGrid3i intersection_count)
        {
            double ox = (double)origin[0], oy = (double)origin[1], oz = (double)origin[2];
            double invdx = 1.0 / dx;

            bool cancelled = false;

            // this is what we will do for each triangle. There are no grid-reads, only grid-writes, 
            // since we use atomic_increment, it is always thread-safe
            Action<int> ProcessTriangleF = (tid) => {
                if (tid % 100 == 0 && CancelF() == true)
                    cancelled = true;
                if (cancelled) return;

                Vector3d xp = Vector3d.Zero, xq = Vector3d.Zero, xr = Vector3d.Zero;
                Mesh.GetTriVertices(tid, ref xp, ref xq, ref xr);


                bool neg_x = false;
                if (InsideMode == InsideModes.ParityCount) {
                    Vector3d n = MathUtil.FastNormalDirection(ref xp, ref xq, ref xr);
                    neg_x = n.x > 0;
                }

                // real ijk coordinates of xp/xq/xr
                double fip = (xp[0] - ox) * invdx, fjp = (xp[1] - oy) * invdx, fkp = (xp[2] - oz) * invdx;
                double fiq = (xq[0] - ox) * invdx, fjq = (xq[1] - oy) * invdx, fkq = (xq[2] - oz) * invdx;
                double fir = (xr[0] - ox) * invdx, fjr = (xr[1] - oy) * invdx, fkr = (xr[2] - oz) * invdx;

                // recompute j/k integer bounds of triangle w/o exact band
                int j0 = MathUtil.Clamp((int)Math.Ceiling(MathUtil.Min(fjp, fjq, fjr)), 0, nj - 1);
                int j1 = MathUtil.Clamp((int)Math.Floor(MathUtil.Max(fjp, fjq, fjr)), 0, nj - 1);
                int k0 = MathUtil.Clamp((int)Math.Ceiling(MathUtil.Min(fkp, fkq, fkr)), 0, nk - 1);
                int k1 = MathUtil.Clamp((int)Math.Floor(MathUtil.Max(fkp, fkq, fkr)), 0, nk - 1);

                // and do intersection counts
                for (int k = k0; k <= k1; ++k) {
                    for (int j = j0; j <= j1; ++j) {
                        double a, b, c;
                        if (point_in_triangle_2d(j, k, fjp, fkp, fjq, fkq, fjr, fkr, out a, out b, out c)) {
                            double fi = a * fip + b * fiq + c * fir; // intersection i coordinate
                            int i_interval = (int)(Math.Ceiling(fi)); // intersection is in (i_interval-1,i_interval]
                            if (i_interval < 0) {
                                intersection_count.atomic_incdec(0, j, k, neg_x);
                            } else if (i_interval < ni) {
                                intersection_count.atomic_incdec(i_interval, j, k, neg_x);
                            } else {
                                // we ignore intersections that are beyond the +x side of the grid
                            }
                        }
                    }
                }
            };

            if (UseParallel) {
                gParallel.ForEach(Mesh.TriangleIndices(), ProcessTriangleF);
            } else {
                foreach (int tid in Mesh.TriangleIndices()) {
                    ProcessTriangleF(tid);
                }
            }

        }





        // iterate through each x-row of grid and set unsigned distances to be negative
        // inside the mesh, based on the intersection_counts
        void compute_signs(int ni, int nj, int nk, DenseGrid3f distances, DenseGrid3i intersection_counts)
        {
            Func<int, bool> isInsideF = (count) => { return count % 2 == 1; };
            if (InsideMode == InsideModes.ParityCount)
                isInsideF = (count) => { return count > 0; };

            if (UseParallel) {
                // can process each x-row in parallel
                AxisAlignedBox2i box = new AxisAlignedBox2i(0, 0, nj, nk);
                gParallel.ForEach(box.IndicesExclusive(), (vi) => {
                    if (CancelF())
                        return;

                    int j = vi.x, k = vi.y;
                    int total_count = 0;
                    for (int i = 0; i < ni; ++i) {
                        total_count += intersection_counts[i, j, k];
                        if (isInsideF(total_count)) { // if parity of intersections so far is odd,
                            distances[i, j, k] = -distances[i, j, k]; // we are inside the mesh
                        }
                    }
                });

            } else {

                for (int k = 0; k < nk; ++k) {
                    if (CancelF())
                        return;

                    for (int j = 0; j < nj; ++j) {
                        int total_count = 0;
                        for (int i = 0; i < ni; ++i) {
                            total_count += intersection_counts[i, j, k];
                            if (isInsideF(total_count)) { // if parity of intersections so far is odd,
                                distances[i, j, k] = -distances[i, j, k]; // we are inside the mesh
                            }
                        }
                    }
                }
            }
        }

        







        // find distance x0 is from segment x1-x2
        static public float point_segment_distance(ref Vector3f x0, ref Vector3f x1, ref Vector3f x2)
        {
            Vector3f dx = x2 - x1;
            float m2 = dx.LengthSquared;
            // find parameter value of closest point on segment
            float s12 = (dx.Dot(x2 - x0) / m2);
            if (s12 < 0) {
                s12 = 0;
            } else if (s12 > 1) {
                s12 = 1;
            }
            // and find the distance
            return x0.Distance(s12 * x1 + (1 - s12) * x2);
        }


        // find distance x0 is from segment x1-x2
        static public double point_segment_distance(ref Vector3d x0, ref Vector3d x1, ref Vector3d x2)
        {
            Vector3d dx = x2 - x1;
            double m2 = dx.LengthSquared;
            // find parameter value of closest point on segment
            double s12 = (dx.Dot(x2 - x0) / m2);
            if (s12 < 0) {
                s12 = 0;
            } else if (s12 > 1) {
                s12 = 1;
            }
            // and find the distance
            return x0.Distance(s12 * x1 + (1 - s12) * x2);
        }



        // find distance x0 is from triangle x1-x2-x3
        static public float point_triangle_distance(ref Vector3f x0, ref Vector3f x1, ref Vector3f x2, ref Vector3f x3)
        {
            // first find barycentric coordinates of closest point on infinite plane
            Vector3f x13 = (x1 - x3);
            Vector3f x23 = (x2 - x3);
            Vector3f x03 = (x0 - x3);
            float m13 = x13.LengthSquared, m23 = x23.LengthSquared, d = x13.Dot(x23);
            float invdet = 1.0f / Math.Max(m13 * m23 - d * d, 1e-30f);
            float a = x13.Dot(x03), b = x23.Dot(x03);
            // the barycentric coordinates themselves
            float w23 = invdet * (m23 * a - d * b);
            float w31 = invdet * (m13 * b - d * a);
            float w12 = 1 - w23 - w31;
            if (w23 >= 0 && w31 >= 0 && w12 >= 0) { // if we're inside the triangle
                return x0.Distance(w23 * x1 + w31 * x2 + w12 * x3);
            } else { // we have to clamp to one of the edges
                if (w23 > 0) // this rules out edge 2-3 for us
                    return Math.Min(point_segment_distance(ref x0, ref x1, ref x2), point_segment_distance(ref x0, ref x1, ref x3));
                else if (w31 > 0) // this rules out edge 1-3
                    return Math.Min(point_segment_distance(ref x0, ref x1, ref x2), point_segment_distance(ref x0, ref x2, ref x3));
                else // w12 must be >0, ruling out edge 1-2
                    return Math.Min(point_segment_distance(ref x0, ref x1, ref x3), point_segment_distance(ref x0, ref x2, ref x3));
            }
        }


        // find distance x0 is from triangle x1-x2-x3
        static public double point_triangle_distance(ref Vector3d x0, ref Vector3d x1, ref Vector3d x2, ref Vector3d x3)
        {
            // first find barycentric coordinates of closest point on infinite plane
            Vector3d x13 = (x1 - x3);
            Vector3d x23 = (x2 - x3);
            Vector3d x03 = (x0 - x3);
            double m13 = x13.LengthSquared, m23 = x23.LengthSquared, d = x13.Dot(ref x23);
            double invdet = 1.0 / Math.Max(m13 * m23 - d * d, 1e-30);
            double a = x13.Dot(ref x03), b = x23.Dot(ref x03);
            // the barycentric coordinates themselves
            double w23 = invdet * (m23 * a - d * b);
            double w31 = invdet * (m13 * b - d * a);
            double w12 = 1 - w23 - w31;
            if (w23 >= 0 && w31 >= 0 && w12 >= 0) { // if we're inside the triangle
                return x0.Distance(w23 * x1 + w31 * x2 + w12 * x3);
            } else { // we have to clamp to one of the edges
                if (w23 > 0) // this rules out edge 2-3 for us
                    return Math.Min(point_segment_distance(ref x0, ref x1, ref x2), point_segment_distance(ref x0, ref x1, ref x3));
                else if (w31 > 0) // this rules out edge 1-3
                    return Math.Min(point_segment_distance(ref x0, ref x1, ref x2), point_segment_distance(ref x0, ref x2, ref x3));
                else // w12 must be >0, ruling out edge 1-2
                    return Math.Min(point_segment_distance(ref x0, ref x1, ref x3), point_segment_distance(ref x0, ref x2, ref x3));
            }
        }




        // calculate twice signed area of triangle (0,0)-(x1,y1)-(x2,y2)
        // return an SOS-determined sign (-1, +1, or 0 only if it's a truly degenerate triangle)
        static public int orientation(double x1, double y1, double x2, double y2, out double twice_signed_area)
        {
            twice_signed_area = y1 * x2 - x1 * y2;
            if (twice_signed_area > 0) return 1;
            else if (twice_signed_area < 0) return -1;
            else if (y2 > y1) return 1;
            else if (y2 < y1) return -1;
            else if (x1 > x2) return 1;
            else if (x1 < x2) return -1;
            else return 0; // only true when x1==x2 and y1==y2
        }


        // robust test of (x0,y0) in the triangle (x1,y1)-(x2,y2)-(x3,y3)
        // if true is returned, the barycentric coordinates are set in a,b,c.
        static public bool point_in_triangle_2d(double x0, double y0,
                                         double x1, double y1, double x2, double y2, double x3, double y3,
                                         out double a, out double b, out double c)
        {
            a = b = c = 0;
            x1 -= x0; x2 -= x0; x3 -= x0;
            y1 -= y0; y2 -= y0; y3 -= y0;
            int signa = orientation(x2, y2, x3, y3, out a);
            if (signa == 0) return false;
            int signb = orientation(x3, y3, x1, y1, out b);
            if (signb != signa) return false;
            int signc = orientation(x1, y1, x2, y2, out c);
            if (signc != signa) return false;
            double sum = a + b + c;
            // if the SOS signs match and are nonzero, there's no way all of a, b, and c are zero.
            if (sum == 0)
                throw new Exception("MakeNarrowBandLevelSet.point_in_triangle_2d: badness!");
            a /= sum;
            b /= sum;
            c /= sum;
            return true;
        }










    }
}
