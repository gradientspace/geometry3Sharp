using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


// based on https://github.com/christopherbatty/SDFGen


namespace g3
{
 



    public class NarrowBandLevelSet
    {
        public DMesh3 Mesh;
        public float CellSize;
        public int ExactBandWidth = 1;


        Vector3f grid_origin;
        DenseGrid3f grid;


        public NarrowBandLevelSet(DMesh3 mesh, float cellSize)
        {
            Mesh = mesh;
            CellSize = cellSize;
        }



        public void Compute()
        {
            // figure out origin & dimensions
            AxisAlignedBox3d bounds = Mesh.CachedBounds;

            float fBufferWidth = 2 * ExactBandWidth * CellSize;
            grid_origin = (Vector3f)bounds.Min - fBufferWidth * Vector3f.One;
            Vector3f max = (Vector3f)bounds.Max + fBufferWidth * Vector3f.One;
            int ni = (int)((max.x - grid_origin.x) / CellSize) + 1;
            int nj = (int)((max.y - grid_origin.y) / CellSize) + 1;
            int nk = (int)((max.z - grid_origin.z) / CellSize) + 1;

            grid = new DenseGrid3f();
            make_level_set3(Mesh, grid_origin, CellSize, ni, nj, nk, grid, ExactBandWidth);
        }



        public Vector3i Dimensions {
            get { return new Vector3i(grid.ni, grid.nj, grid.nk); }
        }
        public float this[int i, int j, int k] {
            get { return grid[i, j, k]; }
        }


        public Vector3f CellCenter(int i, int j, int k)
        {
            return new Vector3f((float)i * CellSize + grid_origin.x, 
                                (float)j * CellSize + grid_origin.y, 
                                (float)k * CellSize + grid_origin.z);
        }




        void make_level_set3(DMesh3 mesh, /*const std::vector<Vec3ui> &tri, const std::vector<Vec3f> &x*/
                        Vector3f origin, float dx,
                        int ni, int nj, int nk,
                        DenseGrid3f phi, int exact_band)
        {
            phi.resize(ni, nj, nk);
            phi.assign((ni + nj + nk) * dx); // upper bound on distance
            DenseGrid3i closest_tri = new DenseGrid3i(ni, nj, nk, -1);
            DenseGrid3i intersection_count = new DenseGrid3i(ni, nj, nk, 0); // intersection_count(i,j,k) is # of tri intersections in (i-1,i]x{j}x{k}
                                                                             // we begin by initializing distances near the mesh, and figuring out intersection counts

            System.Console.WriteLine("start");

            //Vector3f ijkmin, ijkmax;  // [RMS] unused in original code
            double ddx = (double)dx;
            double ox = (double)origin[0], oy = (double)origin[1], oz = (double)origin[2];
            foreach (int t in mesh.TriangleIndices()) {
                Index3i triangle = mesh.GetTriangle(t);
                int p = triangle.a, q = triangle.b, r = triangle.c;

                Vector3d xp = mesh.GetVertex(p);
                Vector3d xq = mesh.GetVertex(q);
                Vector3d xr = mesh.GetVertex(r);

                // coordinates in grid to high precision
                double fip = (xp[0] - ox) / ddx, fjp = (xp[1] - oy) / ddx, fkp = (xp[2] - oz) / ddx;
                double fiq = (xq[0] - ox) / ddx, fjq = (xq[1] - oy) / ddx, fkq = (xq[2] - oz) / ddx;
                double fir = (xr[0] - ox) / ddx, fjr = (xr[1] - oy) / ddx, fkr = (xr[2] - oz) / ddx;
                // do distances nearby
                int i0 = MathUtil.Clamp(((int)MathUtil.Min(fip, fiq, fir)) - exact_band, 0, ni - 1);
                int i1 = MathUtil.Clamp(((int)MathUtil.Max(fip, fiq, fir)) + exact_band + 1, 0, ni - 1);
                int j0 = MathUtil.Clamp(((int)MathUtil.Min(fjp, fjq, fjr)) - exact_band, 0, nj - 1);
                int j1 = MathUtil.Clamp(((int)MathUtil.Max(fjp, fjq, fjr)) + exact_band + 1, 0, nj - 1);
                int k0 = MathUtil.Clamp(((int)MathUtil.Min(fkp, fkq, fkr)) - exact_band, 0, nk - 1);
                int k1 = MathUtil.Clamp(((int)MathUtil.Max(fkp, fkq, fkr)) + exact_band + 1, 0, nk - 1);

                for (int k = k0; k <= k1; ++k) {
                    for (int j = j0; j <= j1; ++j) {
                        for (int i = i0; i <= i1; ++i) {
                            Vector3f gx = new Vector3f((float)i * dx + origin[0], (float)j * dx + origin[1], (float)k * dx + origin[2]);
                            float d = point_triangle_distance(gx, (Vector3f)xp, (Vector3f)xq, (Vector3f)xr);
                            if (d < phi[i, j, k]) {
                                phi[i, j, k] = d;
                                closest_tri[i, j, k] = t;
                            }
                        }
                    }
                }


                // and do intersection counts
                j0 = MathUtil.Clamp((int)Math.Ceiling(MathUtil.Min(fjp, fjq, fjr)), 0, nj - 1);
                j1 = MathUtil.Clamp((int)Math.Floor(MathUtil.Max(fjp, fjq, fjr)), 0, nj - 1);
                k0 = MathUtil.Clamp((int)Math.Ceiling(MathUtil.Min(fkp, fkq, fkr)), 0, nk - 1);
                k1 = MathUtil.Clamp((int)Math.Floor(MathUtil.Max(fkp, fkq, fkr)), 0, nk - 1);
                for (int k = k0; k <= k1; ++k) {
                    for (int j = j0; j <= j1; ++j) {
                        double a, b, c;
                        if (point_in_triangle_2d(j, k, fjp, fkp, fjq, fkq, fjr, fkr, out a, out b, out c)) {
                            double fi = a * fip + b * fiq + c * fir; // intersection i coordinate
                            int i_interval = (int)(Math.Ceiling(fi)); // intersection is in (i_interval-1,i_interval]
                            if (i_interval < 0)
                                intersection_count.increment(0, j, k); // we enlarge the first interval to include everything to the -x direction
                            else if (i_interval < ni)
                                intersection_count.increment(i_interval, j, k);
                            // we ignore intersections that are beyond the +x side of the grid
                        }
                    }
                }
            }

            System.Console.WriteLine("done narrow-band");

            // and now we fill in the rest of the distances with fast sweeping
            for (int pass = 0; pass < 2; ++pass) {
                sweep(mesh, phi, closest_tri, origin, dx, +1, +1, +1);
                sweep(mesh, phi, closest_tri, origin, dx, -1, -1, -1);
                sweep(mesh, phi, closest_tri, origin, dx, +1, +1, -1);
                sweep(mesh, phi, closest_tri, origin, dx, -1, -1, +1);
                sweep(mesh, phi, closest_tri, origin, dx, +1, -1, +1);
                sweep(mesh, phi, closest_tri, origin, dx, -1, +1, -1);
                sweep(mesh, phi, closest_tri, origin, dx, +1, -1, -1);
                sweep(mesh, phi, closest_tri, origin, dx, -1, +1, +1);
            }

            System.Console.WriteLine("done sweeping");

            // then figure out signs (inside/outside) from intersection counts
            for (int k = 0; k < nk; ++k) {
                for (int j = 0; j < nj; ++j) {
                    int total_count = 0;
                    for (int i = 0; i < ni; ++i) {
                        total_count += intersection_count[i, j, k];
                        if (total_count % 2 == 1) { // if parity of intersections so far is odd,
                            phi[i, j, k] = -phi[i, j, k]; // we are inside the mesh
                        }
                    }
                }
            }

            System.Console.WriteLine("done signs");

        }   // end make_level_set_3





        public class DenseGrid3f
        {
            public float[] Buffer;
            public int ni, nj, nk;

            public void resize(int ni, int nj, int nk)
            {
                Buffer = new float[ni * nj * nk];
                this.ni = ni; this.nj = nj; this.nk = nk;
            }

            public void assign(float value)
            {
                for (int i = 0; i < Buffer.Length; ++i)
                    Buffer[i] = value;
            }

            public float this[int i, int j, int k]
            {
                get { return Buffer[i + ni * (j + nj * k)]; }
                set { Buffer[i + ni * (j + nj * k)] = value; }
            }

        }



        public class DenseGrid3i
        {
            public int[] Buffer;
            public int ni, nj, nk;

            public DenseGrid3i(int ni, int nj, int nk, int initialValue)
            {
                resize(ni, nj, nk);
                assign(initialValue);
            }

            public void resize(int ni, int nj, int nk)
            {
                Buffer = new int[ni * nj * nk];
                this.ni = ni; this.nj = nj; this.nk = nk;
            }

            public void assign(int value)
            {
                for (int i = 0; i < Buffer.Length; ++i)
                    Buffer[i] = value;
            }

            public int this[int i, int j, int k]
            {
                get { return Buffer[i + ni * (j + nj * k)]; }
                set { Buffer[i + ni * (j + nj * k)] = value; }
            }

            public void increment(int i, int j, int k)
            {
                Buffer[i + ni * (j + nj * k)]++;
            }
        }





        // find distance x0 is from segment x1-x2
        static float point_segment_distance(Vector3f x0, Vector3f x1, Vector3f x2)
        {
            Vector3f dx = x2 - x1;
            double m2 = dx.LengthSquared;
            // find parameter value of closest point on segment
            float s12 = (float)(dx.Dot(x2 - x0) / m2);
            if (s12 < 0) {
                s12 = 0;
            } else if (s12 > 1) {
                s12 = 1;
            }
            // and find the distance
            return x0.Distance(s12 * x1 + (1 - s12) * x2);
        }




        // find distance x0 is from triangle x1-x2-x3
        static float point_triangle_distance(Vector3f x0, Vector3f x1, Vector3f x2, Vector3f x3)
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
                    return Math.Min(point_segment_distance(x0, x1, x2), point_segment_distance(x0, x1, x3));
                else if (w31 > 0) // this rules out edge 1-3
                    return Math.Min(point_segment_distance(x0, x1, x2), point_segment_distance(x0, x2, x3));
                else // w12 must be >0, ruling out edge 1-2
                    return Math.Min(point_segment_distance(x0, x1, x3), point_segment_distance(x0, x2, x3));
            }
        }




        // calculate twice signed area of triangle (0,0)-(x1,y1)-(x2,y2)
        // return an SOS-determined sign (-1, +1, or 0 only if it's a truly degenerate triangle)
        static int orientation(double x1, double y1, double x2, double y2, out double twice_signed_area)
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
        static bool point_in_triangle_2d(double x0, double y0,
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
            // if the SOS signs match and are nonkero, there's no way all of a, b, and c are zero.
            if (sum == 0)
                throw new Exception("MakeNarrowBandLevelSet.point_in_triangle_2d: badness!");
            a /= sum;
            b /= sum;
            c /= sum;
            return true;
        }



        static void check_neighbour(DMesh3 mesh,
                                    DenseGrid3f phi, DenseGrid3i closest_tri,
                                    Vector3f gx, int i0, int j0, int k0, int i1, int j1, int k1)
        {
            if (closest_tri[i1, j1, k1] >= 0) {
                Index3i tri = mesh.GetTriangle(closest_tri[i1, j1, k1]);
                int p = tri.a, q = tri.b, r = tri.c;
                Vector3f xp = (Vector3f)mesh.GetVertex(p);
                Vector3f xq = (Vector3f)mesh.GetVertex(q);
                Vector3f xr = (Vector3f)mesh.GetVertex(r);
                float d = point_triangle_distance(gx, xp, xq, xr);
                if (d < phi[i0, j0, k0]) {
                    phi[i0, j0, k0] = d;
                    closest_tri[i0, j0, k0] = closest_tri[i1, j1, k1];
                }
            }
        }



        static void sweep(DMesh3 mesh,
                          DenseGrid3f phi, DenseGrid3i closest_tri, Vector3f origin, float dx,
                          int di, int dj, int dk)
        {
            int i0, i1;
            if (di > 0) { i0 = 1; i1 = phi.ni; } else { i0 = phi.ni - 2; i1 = -1; }
            int j0, j1;
            if (dj > 0) { j0 = 1; j1 = phi.nj; } else { j0 = phi.nj - 2; j1 = -1; }
            int k0, k1;
            if (dk > 0) { k0 = 1; k1 = phi.nk; } else { k0 = phi.nk - 2; k1 = -1; }
            for (int k = k0; k != k1; k += dk) {
                for (int j = j0; j != j1; j += dj) {
                    for (int i = i0; i != i1; i += di) {
                        Vector3f gx = new Vector3f(i * dx + origin[0], j * dx + origin[1], k * dx + origin[2]);
                        check_neighbour(mesh, phi, closest_tri, gx, i, j, k, i - di, j, k);
                        check_neighbour(mesh, phi, closest_tri, gx, i, j, k, i, j - dj, k);
                        check_neighbour(mesh, phi, closest_tri, gx, i, j, k, i - di, j - dj, k);
                        check_neighbour(mesh, phi, closest_tri, gx, i, j, k, i, j, k - dk);
                        check_neighbour(mesh, phi, closest_tri, gx, i, j, k, i - di, j, k - dk);
                        check_neighbour(mesh, phi, closest_tri, gx, i, j, k, i, j - dj, k - dk);
                        check_neighbour(mesh, phi, closest_tri, gx, i, j, k, i - di, j - dj, k - dk);
                    }
                }
            }
        }




    }
}
