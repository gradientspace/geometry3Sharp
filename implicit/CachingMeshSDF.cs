// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;



namespace g3
{

    /// <summary>
    /// [RMS] this is variant of MeshSignedDistanceGrid that does lazy evaluation of actual distances,
    /// using mesh spatial data structure. This is much faster if we are doing continuation-method
    /// marching cubes as only values on surface will be computed!
    /// 
    /// 
    /// 
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
    public class CachingMeshSDF
    {
        public DMesh3 Mesh;
        public DMeshAABBTree3 Spatial;
        public float CellSize;

        // Bounds of grid will be expanded this much in positive and negative directions.
        // Useful for if you want field to extend outwards.
        public Vector3d ExpandBounds = Vector3d.Zero;

        // max distance away from surface that we might need to evaluate
        public float MaxOffsetDistance = 0;

        // Most of this parallelizes very well, makes a huge speed difference
        public bool UseParallel = true;

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

        public CachingMeshSDF(DMesh3 mesh, double cellSize, DMeshAABBTree3 spatial)
        {
            Mesh = mesh;
            CellSize = (float)cellSize;
            Spatial = spatial;
        }


        float UpperBoundDistance;
        double MaxDistQueryDist;


        public void Initialize()
        {
            // figure out origin & dimensions
            AxisAlignedBox3d bounds = Mesh.CachedBounds;

            float fBufferWidth = (float)Math.Max(4*CellSize, 2*MaxOffsetDistance + 2*CellSize);
            grid_origin = (Vector3f)bounds.Min - fBufferWidth * Vector3f.One - (Vector3f)ExpandBounds;
            Vector3f max = (Vector3f)bounds.Max + fBufferWidth * Vector3f.One + (Vector3f)ExpandBounds;
            int ni = (int)((max.x - grid_origin.x) / CellSize) + 1;
            int nj = (int)((max.y - grid_origin.y) / CellSize) + 1;
            int nk = (int)((max.z - grid_origin.z) / CellSize) + 1;

            UpperBoundDistance = (float)((ni+nj+nk) * CellSize);
            grid = new DenseGrid3f(ni, nj, nk, UpperBoundDistance);

            MaxDistQueryDist = MaxOffsetDistance + (2*CellSize*MathUtil.SqrtTwo);

            // closest triangle id for each grid cell
            if ( WantClosestTriGrid )
                closest_tri_grid = new DenseGrid3i(ni, nj, nk, -1);

            // intersection_count(i,j,k) is # of tri intersections in (i-1,i]x{j}x{k}
            DenseGrid3i intersection_count = new DenseGrid3i(ni, nj, nk, 0);


            if (ComputeSigns == true) {
                compute_intersections(grid_origin, CellSize, ni, nj, nk, intersection_count);
                if (CancelF())
                    return;

                // then figure out signs (inside/outside) from intersection counts
                compute_signs(ni, nj, nk, grid, intersection_count);
                if (CancelF())
                    return;

                if (WantIntersectionsGrid)
                    intersections_grid = intersection_count;
            }
        }


        public float GetValue(Vector3i idx)
        {
            float f = grid[idx];
            if ( f == UpperBoundDistance || f == -UpperBoundDistance ) {
                Vector3d p = cell_center(idx);

                float sign = Math.Sign(f);

                double dsqr;
                int near_tid = Spatial.FindNearestTriangle(p, out dsqr, MaxDistQueryDist);
                //int near_tid = Spatial.FindNearestTriangle(p, out dsqr);
                if ( near_tid == DMesh3.InvalidID ) {
                    f += 0.0001f;
                } else {
                    f = sign * (float)Math.Sqrt(dsqr);
                }

                grid[idx] = f;
                if (closest_tri_grid != null)
                    closest_tri_grid[idx] = near_tid;
            }
            return f;
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












    /// <summary>
    /// Tri-linear interpolant for a 3D dense grid. Supports grid translation
    /// via GridOrigin, but does not support scaling or rotation. If you need those,
    /// you can wrap this in something that does the xform.
    /// </summary>
	public class CachingMeshSDFImplicit : BoundedImplicitFunction3d
    {
        public CachingMeshSDF SDF;
        public double CellSize;
        public Vector3d GridOrigin;

        // value to return if query point is outside grid (in an SDF
        // outside is usually positive). Need to do math with this value,
        // so don't use double.MaxValue or square will overflow
        public double Outside = Math.Sqrt(Math.Sqrt(double.MaxValue));

        public CachingMeshSDFImplicit(CachingMeshSDF sdf)
        {
            SDF = sdf;
            GridOrigin = sdf.GridOrigin;
            CellSize = sdf.CellSize;
        }

        public AxisAlignedBox3d Bounds()
        {
            return new AxisAlignedBox3d(
                GridOrigin.x, GridOrigin.y, GridOrigin.z,
                GridOrigin.x + CellSize * SDF.Grid.ni,
                GridOrigin.y + CellSize * SDF.Grid.nj,
                GridOrigin.z + CellSize * SDF.Grid.nk);
        }


        public double Value(ref Vector3d pt)
        {
            Vector3d gridPt = new Vector3d(
                ((pt.x - GridOrigin.x) / CellSize),
                ((pt.y - GridOrigin.y) / CellSize),
                ((pt.z - GridOrigin.z) / CellSize));

            // compute integer coordinates
            int x0 = (int)gridPt.x;
            int y0 = (int)gridPt.y, y1 = y0 + 1;
            int z0 = (int)gridPt.z, z1 = z0 + 1;

            // clamp to grid
            if (x0 < 0 || (x0 + 1) >= SDF.Grid.ni ||
                y0 < 0 || y1 >= SDF.Grid.nj ||
                z0 < 0 || z1 >= SDF.Grid.nk)
                return Outside;

            // convert double coords to [0,1] range
            double fAx = gridPt.x - (double)x0;
            double fAy = gridPt.y - (double)y0;
            double fAz = gridPt.z - (double)z0;
            double OneMinusfAx = 1.0 - fAx;

            // compute trilinear interpolant. The code below tries to do this with the fewest 
            // number of variables, in hopes that optimizer will be clever about re-using registers, etc.
            // Commented code at bottom is fully-expanded version.
            // [TODO] it is possible to implement lerps here as a+(b-a)*t, saving a multiply and a variable.
            //   This is numerically worse, but since the grid values are floats and
            //   we are computing in doubles, does it matter?
            double xa, xb;

            get_value_pair(x0, y0, z0, out xa, out xb);
            double yz = (1 - fAy) * (1 - fAz);
            double sum = (OneMinusfAx * xa + fAx * xb) * yz;

            get_value_pair(x0, y0, z1, out xa, out xb);
            yz = (1 - fAy) * (fAz);
            sum += (OneMinusfAx * xa + fAx * xb) * yz;

            get_value_pair(x0, y1, z0, out xa, out xb);
            yz = (fAy) * (1 - fAz);
            sum += (OneMinusfAx * xa + fAx * xb) * yz;

            get_value_pair(x0, y1, z1, out xa, out xb);
            yz = (fAy) * (fAz);
            sum += (OneMinusfAx * xa + fAx * xb) * yz;

            return sum;

            // fV### is grid cell corner index
            //return
            //    fV000 * (1 - fAx) * (1 - fAy) * (1 - fAz) +
            //    fV001 * (1 - fAx) * (1 - fAy) * (fAz) +
            //    fV010 * (1 - fAx) * (fAy) * (1 - fAz) +
            //    fV011 * (1 - fAx) * (fAy) * (fAz) +
            //    fV100 * (fAx) * (1 - fAy) * (1 - fAz) +
            //    fV101 * (fAx) * (1 - fAy) * (fAz) +
            //    fV110 * (fAx) * (fAy) * (1 - fAz) +
            //    fV111 * (fAx) * (fAy) * (fAz);
        }



        void get_value_pair(int i, int j, int k, out double a, out double b)
        {
            a = SDF.GetValue(new Vector3i(i,j,k));
            b = SDF.GetValue(new Vector3i(i+1,j,k));
        }



        public Vector3d Gradient(ref Vector3d pt)
        {
            Vector3d gridPt = new Vector3d(
                ((pt.x - GridOrigin.x) / CellSize),
                ((pt.y - GridOrigin.y) / CellSize),
                ((pt.z - GridOrigin.z) / CellSize));

            // clamp to grid
            if (gridPt.x < 0 || gridPt.x >= SDF.Grid.ni - 1 ||
                gridPt.y < 0 || gridPt.y >= SDF.Grid.nj - 1 ||
                gridPt.z < 0 || gridPt.z >= SDF.Grid.nk - 1)
                return Vector3d.Zero;

            // compute integer coordinates
            int x0 = (int)gridPt.x;
            int y0 = (int)gridPt.y, y1 = y0 + 1;
            int z0 = (int)gridPt.z, z1 = z0 + 1;

            // convert double coords to [0,1] range
            double fAx = gridPt.x - (double)x0;
            double fAy = gridPt.y - (double)y0;
            double fAz = gridPt.z - (double)z0;

            double fV000, fV100;
            get_value_pair(x0, y0, z0, out fV000, out fV100);
            double fV010, fV110;
            get_value_pair(x0, y1, z0, out fV010, out fV110);
            double fV001, fV101;
            get_value_pair(x0, y0, z1, out fV001, out fV101);
            double fV011, fV111;
            get_value_pair(x0, y1, z1, out fV011, out fV111);

            // [TODO] can re-order this to vastly reduce number of ops!
            double gradX =
                -fV000 * (1 - fAy) * (1 - fAz) +
                -fV001 * (1 - fAy) * (fAz) +
                -fV010 * (fAy) * (1 - fAz) +
                -fV011 * (fAy) * (fAz) +
                 fV100 * (1 - fAy) * (1 - fAz) +
                 fV101 * (1 - fAy) * (fAz) +
                 fV110 * (fAy) * (1 - fAz) +
                 fV111 * (fAy) * (fAz);

            double gradY =
                -fV000 * (1 - fAx) * (1 - fAz) +
                -fV001 * (1 - fAx) * (fAz) +
                 fV010 * (1 - fAx) * (1 - fAz) +
                 fV011 * (1 - fAx) * (fAz) +
                -fV100 * (fAx) * (1 - fAz) +
                -fV101 * (fAx) * (fAz) +
                 fV110 * (fAx) * (1 - fAz) +
                 fV111 * (fAx) * (fAz);

            double gradZ =
                -fV000 * (1 - fAx) * (1 - fAy) +
                 fV001 * (1 - fAx) * (1 - fAy) +
                -fV010 * (1 - fAx) * (fAy) +
                 fV011 * (1 - fAx) * (fAy) +
                -fV100 * (fAx) * (1 - fAy) +
                 fV101 * (fAx) * (1 - fAy) +
                -fV110 * (fAx) * (fAy) +
                 fV111 * (fAx) * (fAy);

            return new Vector3d(gradX, gradY, gradZ);
        }

    }



}
