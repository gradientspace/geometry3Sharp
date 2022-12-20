using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using g3;

namespace g3
{
    /// <summary>
    /// Basic implementation of marching cubes mesh generation, which can be applied to
    /// arbitrary Implicit function. Multi-threading enabled by default.
    /// 
    /// [TODO] support locking on Implicit.Value()? May not be thread-safe!!
    /// [TODO] extension that tracks set of triangles in each cube, so we can do partial updates?
    /// [TODO] is hash table on vertex x/y/z the best idea?
    /// [TODO] hash table for edge vtx-indices instead, like old polygonizer? (how did we index edges?!?)
    /// 
    /// </summary>
    public class MarchingCubes
    {
        /// <summary>
        /// this is the function we will evaluate
        /// </summary>
        public ImplicitFunction3d Implicit;

        /// <summary>
        /// mesh surface will be at this isovalue. Normally 0 unless you want
        /// offset surface or field is not a distance-field.
        /// </summary>
        public double IsoValue = 0;

        /// <summary> bounding-box we will mesh inside of. We use the min-corner and
        /// the width/height/depth, but do not clamp vertices to stay within max-corner,
        /// we may spill one cell over </summary>
        public AxisAlignedBox3d Bounds;

        /// <summary>
        /// Length of edges of cubes that are marching.
        /// currently, # of cells along axis = (int)(bounds_dimension / CellSize) + 1
        /// </summary>
        public double CubeSize = 0.1;

        /// <summary>
        /// Use multi-threading? Generally a good idea unless problem is very small or
        /// you are multi-threading at a higher level (which may be more efficient as
        /// we currently use very fine-grained spinlocks to synchronize)
        /// </summary>
        public bool ParallelCompute = true;


        public enum RootfindingModes { SingleLerp, LerpSteps, Bisection }

        /// <summary>
        /// Which rootfinding method will be used to converge on surface along edges
        /// </summary>
        public RootfindingModes RootMode = RootfindingModes.SingleLerp;

        /// <summary>
        /// number of iterations of rootfinding method (ignored for SingleLerp)
        /// </summary>
        public int RootModeSteps = 5;


        /// <summary> if this function returns true, we should abort calculation </summary>
        public Func<bool> CancelF = () => { return false; };

        /*
         * Outputs
         */

        // cube indices range from [Origin,CellDimensions)   
        public Vector3i CellDimensions;

        // computed mesh
        public DMesh3 Mesh;

        // Vertices of mesh are stored in this hash table, to ensure uniqueness.
        // Not sure what you would do with this, but it is exposed anyway.
        public Dictionary<Vector3d, int> VertexHash;



        public MarchingCubes()
        {
            // initialize w/ a basic sphere example
            Implicit = new ImplicitSphere3d();
            Bounds = new AxisAlignedBox3d(Vector3d.Zero, 8);
            CubeSize = 0.25;
        }



        /// <summary>
        /// Run MC algorithm and generate Output mesh
        /// </summary>
        public void Generate()
        {
            Mesh = new DMesh3();

            VertexHash = new Dictionary<Vector3d, int>();

            int nx = (int)(Bounds.Width / CubeSize) + 1;
            int ny = (int)(Bounds.Height / CubeSize) + 1;
            int nz = (int)(Bounds.Depth / CubeSize) + 1;
            CellDimensions = new Vector3i(nx, ny, nz);

            if (ParallelCompute) {
                generate_parallel();
            } else {
                generate_basic();
            }
        }


        // we pass Cells around, this makes code cleaner
        class GridCell {
            public Vector3d[] p;    // corners of cell
            public double[] f;      // field values at corners

            public GridCell() {
                p = new Vector3d[8];
                f = new double[8];
            }
        }

        // currently unused
        void GridToPos(int x, int y, int z, ref Vector3d p) {
            p.x = Bounds.Min.x + CubeSize * x;
            p.y = Bounds.Min.y + CubeSize * y;
            p.z = Bounds.Min.z + CubeSize * z;
        }



        /// <summary>
        /// compute 3D corner-positions and field values for cell at index
        /// </summary>
        void initialize_cell(GridCell cell, ref Vector3i idx)
        {
            // [RMS] don't just add CellSize to x0 because then we
            //   get different numerical values for same point at different cells,
            //   which breaks our hash table...
            double x0 = Bounds.Min.x + CubeSize * idx.x;
            double y0 = Bounds.Min.y + CubeSize * idx.y;
            double z0 = Bounds.Min.z + CubeSize * idx.z;
            double x1 = Bounds.Min.x + CubeSize * (idx.x+1);
            double y1 = Bounds.Min.y + CubeSize * (idx.y+1);
            double z1 = Bounds.Min.z + CubeSize * (idx.z+1);

            cell.p[0].x = x0; cell.p[0].y = y0; cell.p[0].z = z0;
            cell.p[1].x = x1; cell.p[1].y = y0; cell.p[1].z = z0;
            cell.p[2].x = x1; cell.p[2].y = y0; cell.p[2].z = z1;
            cell.p[3].x = x0; cell.p[3].y = y0; cell.p[3].z = z1;

            cell.p[4].x = x0; cell.p[4].y = y1; cell.p[4].z = z0;
            cell.p[5].x = x1; cell.p[5].y = y1; cell.p[5].z = z0;
            cell.p[6].x = x1; cell.p[6].y = y1; cell.p[6].z = z1;
            cell.p[7].x = x0; cell.p[7].y = y1; cell.p[7].z = z1;

            for (int i = 0; i < 8; ++i)
                cell.f[i] = Implicit.Value(ref cell.p[i]);
        }


        // assume we just want to slide cell at xi-1 to cell at xi, while keeping
        // yi and zi constant. Then only x-coords change, and we have already 
        // computed half the values
        void shift_cell_x(GridCell cell, int xi)
        {
            double xPrev = cell.p[1].x;
            double x1 = Bounds.Min.x + CubeSize * (xi + 1);

            cell.p[0].x = xPrev; 
            cell.p[1].x = x1; 
            cell.p[2].x = x1; 
            cell.p[3].x = xPrev; 

            cell.p[4].x = xPrev; 
            cell.p[5].x = x1; 
            cell.p[6].x = x1; 
            cell.p[7].x = xPrev;

            cell.f[0] = cell.f[1];
            cell.f[3] = cell.f[2];
            cell.f[4] = cell.f[5];
            cell.f[7] = cell.f[6];

            cell.f[1] = Implicit.Value(ref cell.p[1]);
            cell.f[2] = Implicit.Value(ref cell.p[2]);
            cell.f[5] = Implicit.Value(ref cell.p[5]);
            cell.f[6] = Implicit.Value(ref cell.p[6]);
        }


        bool bParallel = false;
        SpinLock hash_lock;
        SpinLock mesh_lock;

        /// <summary>
        /// processing z-slabs of cells in parallel
        /// </summary>
        void generate_parallel()
        {
            hash_lock = new SpinLock();
            mesh_lock = new SpinLock();
            bParallel = true;

            // [TODO] maybe shouldn't alway use Z axis here?
            gParallel.ForEach(Interval1i.Range(CellDimensions.z), (zi) => {
                GridCell cell = new GridCell();
                Vector3d[] vertlist = new Vector3d[12];
                for (int yi = 0; yi < CellDimensions.y; ++yi) {
                    if (CancelF())
                        return;
                    // compute full cell at x=0, then slide along x row, which saves half of value computes
                    Vector3i idx = new Vector3i(0, yi, zi);
                    initialize_cell(cell, ref idx);
                    polygonize_cell(cell, vertlist);
                    for (int xi = 1; xi < CellDimensions.x; ++xi) {
                        shift_cell_x(cell, xi);
                        polygonize_cell(cell, vertlist);
                    }
                }
            });


            bParallel = false;
        }




        /// <summary>
        /// fully sequential version, no threading
        /// </summary>
        void generate_basic()
        {
            GridCell cell = new GridCell();
            Vector3d[] vertlist = new Vector3d[12];

            for (int zi = 0; zi < CellDimensions.z; ++zi) {
                for (int yi = 0; yi < CellDimensions.y; ++yi) {
                    if (CancelF())
                        return;
                    // compute full cell at x=0, then slide along x row, which saves half of value computes
                    Vector3i idx = new Vector3i(0, yi, zi);
                    initialize_cell(cell, ref idx);
                    polygonize_cell(cell, vertlist);
                    for (int xi = 1; xi < CellDimensions.x; ++xi) {
                        shift_cell_x(cell, xi);
                        polygonize_cell(cell, vertlist);
                    }

                }
            }
        }



        /// <summary>
        /// find edge crossings and generate triangles for this cell
        /// </summary>
        bool polygonize_cell(GridCell cell, Vector3d[] vertList)
        {
            // construct bits of index into edge table, where bit for each
            // corner is 1 if that value is < isovalue.
            // This tell us which edges have sign-crossings, and the int value
            // of the bitmap is an index into the edge and triangle tables
            int cubeindex = 0, shift = 1;
            for ( int i = 0; i < 8; ++i ) {
                if (cell.f[i] < IsoValue)
                    cubeindex |= shift;
                shift <<= 1;
            }

            // no crossings!
            if (edgeTable[cubeindex] == 0)
                return false;

            // check each bit of value in edge table. If it is 1, we
            // have a crossing on that edge. Look up the indices of this
            // edge and find the intersection point along it
            shift = 1;
            for (int i = 0; i <= 11; i++) {
                if ( (edgeTable[cubeindex] & shift) != 0 ) {
                    int a = edge_indices[i, 0], b = edge_indices[i, 1];
                    find_iso(ref cell.p[a], ref cell.p[b], cell.f[a], cell.f[b], ref vertList[i]);
                }
                shift <<= 1;
            }
            
            // now iterate through the set of triangles in triTable for this cube,
            // and emit triangles using the vertices we found.
            int tri_count = 0;
            for (int i = 0; triTable[cubeindex,i] != -1; i += 3) {
                int ta = triTable[cubeindex, i];
                int a = find_or_append_vertex(ref vertList[ta]);

                int tb = triTable[cubeindex, i+1];
                int b = find_or_append_vertex(ref vertList[tb]);

                int tc = triTable[cubeindex, i + 2];
                int c = find_or_append_vertex(ref vertList[tc]);

                // if a corner is within tolerance of isovalue, then some triangles
                // will be degenerate, and we can skip them w/o resulting in cracks (right?)
                if (a == b || a == c || b == c)
                    continue;  

                /*int tid = */append_triangle(a, b, c);
                tri_count++;
            }

            return (tri_count > 0);
        }



        /// <summary>
        /// check if vertex is in hash table, and if not add new one, with locking if computing in-parallel
        /// </summary>
        int find_or_append_vertex(ref Vector3d pos)
        {
            bool lock_taken = false;
            if ( bParallel ) {
                hash_lock.Enter(ref lock_taken);
            }

            int vid;
            if (VertexHash.TryGetValue(pos, out vid) == false) {
                vid = append_vertex(pos);
                VertexHash[pos] = vid;
            }

            if (lock_taken)
                hash_lock.Exit();

            return vid;
        }



        /// <summary>
        /// add vertex to mesh, with locking if we are computing in parallel
        /// </summary>
        int append_vertex(Vector3d v)
        {
            bool lock_taken = false;
            if (bParallel) {
                mesh_lock.Enter(ref lock_taken);
            }

            int vid = Mesh.AppendVertex(v);

            if (lock_taken)
                mesh_lock.Exit();

            return vid;
        }



        /// <summary>
        /// add triangle to mesh, with locking if we are computing in parallel
        /// </summary>
        int append_triangle(int a, int b, int c)
        {
            bool lock_taken = false;
            if (bParallel) {
                mesh_lock.Enter(ref lock_taken);
            }

            int tid = Mesh.AppendTriangle(a, b, c);

            if (lock_taken)
                mesh_lock.Exit();

            return tid;
        }



        /// <summary>
        /// root-find the intersection along edge from f(p1)=valp1 to f(p2)=valp2
        /// </summary>
        void find_iso(ref Vector3d p1, ref Vector3d p2, double valp1, double valp2, ref Vector3d pIso)
        {
            // Ok, this is a bit hacky but seems to work? If both isovalues
            // are the same, we just return the midpoint. If one is nearly zero, we can
            // but assume that's where the surface is. *However* if we return that point exactly,
            // we can get nonmanifold vertices, because multiple fans may connect there. 
            // Since DMesh3 disallows that, it results in holes. So we pull 
            // slightly towards the other point along this edge. This means we will get
            // repeated nearly-coincident vertices, but the mesh will be manifold.
            const double dt = 0.999999;
            if (Math.Abs(valp1 - valp2) < 0.00001) {
                pIso = (p1 + p2) * 0.5;
                return;
            }
            if (Math.Abs(IsoValue - valp1) < 0.00001) {
                pIso = dt*p1 + (1.0-dt)*p2;
                return;
            }
            if (Math.Abs(IsoValue - valp2) < 0.00001) {
                pIso = (dt)*p2 + (1.0-dt)*p1;
                return;
            }

            // [RMS] if we don't maintain min/max order here, then numerical error means
            //   that hashing on point x/y/z doesn't work
            Vector3d a = p1, b = p2;
            double fa = valp1, fb = valp2;
            if (valp2 < valp1) {
                a = p2; b = p1;
                fb = valp1; fa = valp2;
            }

            // converge on root
            if (RootMode == RootfindingModes.Bisection) {
                for (int k = 0; k < RootModeSteps; ++k) {
                    pIso.x = (a.x + b.x) * 0.5; pIso.y = (a.y + b.y) * 0.5; pIso.z = (a.z + b.z) * 0.5;
                    double mid_f = Implicit.Value(ref pIso);
                    if (mid_f < IsoValue) {
                        a = pIso; fa = mid_f;
                    } else {
                        b = pIso; fb = mid_f;
                    }
                }
                pIso = Vector3d.Lerp(a, b, 0.5);

            } else {
                double mu = 0;
                if (RootMode == RootfindingModes.LerpSteps) {
                    for (int k = 0; k < RootModeSteps; ++k) {
                        mu = (IsoValue - fa) / (fb - fa);
                        pIso.x = a.x + mu * (b.x - a.x);
                        pIso.y = a.y + mu * (b.y - a.y);
                        pIso.z = a.z + mu * (b.z - a.z);
                        double mid_f = Implicit.Value(ref pIso);
                        if (mid_f < IsoValue) {
                            a = pIso; fa = mid_f;
                        } else {
                            b = pIso; fb = mid_f;
                        }
                    }
                }

                // final lerp
                mu = (IsoValue - fa) / (fb - fa);
                pIso.x = a.x + mu * (b.x - a.x);
                pIso.y = a.y + mu * (b.y - a.y);
                pIso.z = a.z + mu * (b.z - a.z);
            }
        }




        /*
         * Below here are standard marching-cubes tables. 
         */


        static readonly int[,] edge_indices = new int[,] {
            {0,1}, {1,2}, {2,3}, {3,0}, {4,5}, {5,6}, {6,7}, {7,4}, {0,4}, {1,5}, {2,6}, {3,7}
        };

        static readonly int[] edgeTable = new int[256] {
            0x0  , 0x109, 0x203, 0x30a, 0x406, 0x50f, 0x605, 0x70c,
            0x80c, 0x905, 0xa0f, 0xb06, 0xc0a, 0xd03, 0xe09, 0xf00,
            0x190, 0x99 , 0x393, 0x29a, 0x596, 0x49f, 0x795, 0x69c,
            0x99c, 0x895, 0xb9f, 0xa96, 0xd9a, 0xc93, 0xf99, 0xe90,
            0x230, 0x339, 0x33 , 0x13a, 0x636, 0x73f, 0x435, 0x53c,
            0xa3c, 0xb35, 0x83f, 0x936, 0xe3a, 0xf33, 0xc39, 0xd30,
            0x3a0, 0x2a9, 0x1a3, 0xaa , 0x7a6, 0x6af, 0x5a5, 0x4ac,
            0xbac, 0xaa5, 0x9af, 0x8a6, 0xfaa, 0xea3, 0xda9, 0xca0,
            0x460, 0x569, 0x663, 0x76a, 0x66 , 0x16f, 0x265, 0x36c,
            0xc6c, 0xd65, 0xe6f, 0xf66, 0x86a, 0x963, 0xa69, 0xb60,
            0x5f0, 0x4f9, 0x7f3, 0x6fa, 0x1f6, 0xff , 0x3f5, 0x2fc,
            0xdfc, 0xcf5, 0xfff, 0xef6, 0x9fa, 0x8f3, 0xbf9, 0xaf0,
            0x650, 0x759, 0x453, 0x55a, 0x256, 0x35f, 0x55 , 0x15c,
            0xe5c, 0xf55, 0xc5f, 0xd56, 0xa5a, 0xb53, 0x859, 0x950,
            0x7c0, 0x6c9, 0x5c3, 0x4ca, 0x3c6, 0x2cf, 0x1c5, 0xcc ,
            0xfcc, 0xec5, 0xdcf, 0xcc6, 0xbca, 0xac3, 0x9c9, 0x8c0,
            0x8c0, 0x9c9, 0xac3, 0xbca, 0xcc6, 0xdcf, 0xec5, 0xfcc,
            0xcc , 0x1c5, 0x2cf, 0x3c6, 0x4ca, 0x5c3, 0x6c9, 0x7c0,
            0x950, 0x859, 0xb53, 0xa5a, 0xd56, 0xc5f, 0xf55, 0xe5c,
            0x15c, 0x55 , 0x35f, 0x256, 0x55a, 0x453, 0x759, 0x650,
            0xaf0, 0xbf9, 0x8f3, 0x9fa, 0xef6, 0xfff, 0xcf5, 0xdfc,
            0x2fc, 0x3f5, 0xff , 0x1f6, 0x6fa, 0x7f3, 0x4f9, 0x5f0,
            0xb60, 0xa69, 0x963, 0x86a, 0xf66, 0xe6f, 0xd65, 0xc6c,
            0x36c, 0x265, 0x16f, 0x66 , 0x76a, 0x663, 0x569, 0x460,
            0xca0, 0xda9, 0xea3, 0xfaa, 0x8a6, 0x9af, 0xaa5, 0xbac,
            0x4ac, 0x5a5, 0x6af, 0x7a6, 0xaa , 0x1a3, 0x2a9, 0x3a0,
            0xd30, 0xc39, 0xf33, 0xe3a, 0x936, 0x83f, 0xb35, 0xa3c,
            0x53c, 0x435, 0x73f, 0x636, 0x13a, 0x33 , 0x339, 0x230,
            0xe90, 0xf99, 0xc93, 0xd9a, 0xa96, 0xb9f, 0x895, 0x99c,
            0x69c, 0x795, 0x49f, 0x596, 0x29a, 0x393, 0x99 , 0x190,
            0xf00, 0xe09, 0xd03, 0xc0a, 0xb06, 0xa0f, 0x905, 0x80c,
            0x70c, 0x605, 0x50f, 0x406, 0x30a, 0x203, 0x109, 0x0   };


        static readonly int[,] triTable = new int[256,16]
            {{-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1},
            {3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1},
            {3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1},
            {3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1},
            {9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1},
            {9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
            {2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1},
            {8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1},
            {9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
            {4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1},
            {3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1},
            {1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1},
            {4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1},
            {4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1},
            {9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
            {5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1},
            {2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1},
            {9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
            {0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
            {2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1},
            {10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1},
            {4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1},
            {5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1},
            {5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1},
            {9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1},
            {0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1},
            {1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1},
            {10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1},
            {8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1},
            {2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1},
            {7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1},
            {9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1},
            {2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1},
            {11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1},
            {9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1},
            {5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1},
            {11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1},
            {11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
            {1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1},
            {9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1},
            {5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1},
            {2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
            {0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
            {5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1},
            {6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1},
            {3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1},
            {6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1},
            {5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1},
            {1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
            {10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1},
            {6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1},
            {8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1},
            {7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1},
            {3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
            {5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1},
            {0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1},
            {9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1},
            {8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1},
            {5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1},
            {0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1},
            {6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1},
            {10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1},
            {10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1},
            {8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1},
            {1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1},
            {3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1},
            {0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1},
            {10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1},
            {3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1},
            {6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1},
            {9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1},
            {8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1},
            {3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1},
            {6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1},
            {0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1},
            {10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1},
            {10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1},
            {2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1},
            {7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1},
            {7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1},
            {2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1},
            {1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1},
            {11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1},
            {8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1},
            {0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1},
            {7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
            {10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
            {2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
            {6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1},
            {7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1},
            {2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1},
            {1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1},
            {10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1},
            {10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1},
            {0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1},
            {7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1},
            {6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1},
            {8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1},
            {9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1},
            {6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1},
            {4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1},
            {10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1},
            {8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1},
            {0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1},
            {1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1},
            {8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1},
            {10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1},
            {4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1},
            {10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
            {5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
            {11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1},
            {9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
            {6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1},
            {7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1},
            {3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1},
            {7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1},
            {9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1},
            {3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1},
            {6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1},
            {9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1},
            {1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1},
            {4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1},
            {7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1},
            {6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1},
            {3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1},
            {0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1},
            {6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1},
            {0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1},
            {11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1},
            {6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1},
            {5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1},
            {9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1},
            {1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1},
            {1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1},
            {10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1},
            {0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1},
            {5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1},
            {10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1},
            {11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1},
            {9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1},
            {7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1},
            {2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1},
            {8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1},
            {9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1},
            {9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1},
            {1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1},
            {9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1},
            {9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1},
            {5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1},
            {0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1},
            {10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1},
            {2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1},
            {0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1},
            {0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1},
            {9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1},
            {5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1},
            {3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1},
            {5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1},
            {8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1},
            {0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1},
            {9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1},
            {1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1},
            {3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1},
            {4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1},
            {9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1},
            {11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1},
            {11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1},
            {2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1},
            {9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1},
            {3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1},
            {1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1},
            {4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1},
            {4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1},
            {0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1},
            {3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1},
            {3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1},
            {0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1},
            {9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1},
            {1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}};



        // [RMS] alternative triangle table proposed in: http://paulbourke.net/geometry/polygonise/table2.txt
        //  max row is 3 shorter, so this saves a triangle somewhere? 
        //  not currently in use (so far have not found a case where it produces a different result)
/*
        static int[,] triTable2 = new int[256, 13] {
            {-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 8, 3, 0,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 9, 0, 1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 8, 3, 1, 8, 1, 9,-1,-1,-1,-1,-1,-1,-1},
            {10, 1, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 8, 3, 0, 1, 2,10,-1,-1,-1,-1,-1,-1,-1},
            { 9, 0, 2, 9, 2,10,-1,-1,-1,-1,-1,-1,-1},
            { 3, 2, 8, 2,10, 8, 8,10, 9,-1,-1,-1,-1},
            {11, 2, 3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            {11, 2, 0,11, 0, 8,-1,-1,-1,-1,-1,-1,-1},
            {11, 2, 3, 0, 1, 9,-1,-1,-1,-1,-1,-1,-1},
            { 2, 1,11, 1, 9,11,11, 9, 8,-1,-1,-1,-1},
            {10, 1, 3,10, 3,11,-1,-1,-1,-1,-1,-1,-1},
            { 1, 0,10, 0, 8,10,10, 8,11,-1,-1,-1,-1},
            { 0, 3, 9, 3,11, 9, 9,11,10,-1,-1,-1,-1},
            { 8,10, 9, 8,11,10,-1,-1,-1,-1,-1,-1,-1},
            { 8, 4, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 3, 0, 4, 3, 4, 7,-1,-1,-1,-1,-1,-1,-1},
            { 1, 9, 0, 8, 4, 7,-1,-1,-1,-1,-1,-1,-1},
            { 9, 4, 1, 4, 7, 1, 1, 7, 3,-1,-1,-1,-1},
            {10, 1, 2, 8, 4, 7,-1,-1,-1,-1,-1,-1,-1},
            { 2,10, 1, 0, 4, 7, 0, 7, 3,-1,-1,-1,-1},
            { 4, 7, 8, 0, 2,10, 0,10, 9,-1,-1,-1,-1},
            { 2, 7, 3, 2, 9, 7, 7, 9, 4, 2,10, 9,-1},
            { 2, 3,11, 7, 8, 4,-1,-1,-1,-1,-1,-1,-1},
            { 7,11, 4,11, 2, 4, 4, 2, 0,-1,-1,-1,-1},
            { 3,11, 2, 4, 7, 8, 9, 0, 1,-1,-1,-1,-1},
            { 2, 7,11, 2, 1, 7, 1, 4, 7, 1, 9, 4,-1},
            { 8, 4, 7,11,10, 1,11, 1, 3,-1,-1,-1,-1},
            {11, 4, 7, 1, 4,11, 1,11,10, 1, 0, 4,-1},
            { 3, 8, 0, 7,11, 4,11, 9, 4,11,10, 9,-1},
            { 7,11, 4, 4,11, 9,11,10, 9,-1,-1,-1,-1},
            { 9, 5, 4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 3, 0, 8, 4, 9, 5,-1,-1,-1,-1,-1,-1,-1},
            { 5, 4, 0, 5, 0, 1,-1,-1,-1,-1,-1,-1,-1},
            { 4, 8, 5, 8, 3, 5, 5, 3, 1,-1,-1,-1,-1},
            { 2,10, 1, 9, 5, 4,-1,-1,-1,-1,-1,-1,-1},
            { 0, 8, 3, 5, 4, 9,10, 1, 2,-1,-1,-1,-1},
            {10, 5, 2, 5, 4, 2, 2, 4, 0,-1,-1,-1,-1},
            { 3, 4, 8, 3, 2, 4, 2, 5, 4, 2,10, 5,-1},
            {11, 2, 3, 9, 5, 4,-1,-1,-1,-1,-1,-1,-1},
            { 9, 5, 4, 8,11, 2, 8, 2, 0,-1,-1,-1,-1},
            { 3,11, 2, 1, 5, 4, 1, 4, 0,-1,-1,-1,-1},
            { 8, 5, 4, 2, 5, 8, 2, 8,11, 2, 1, 5,-1},
            { 5, 4, 9, 1, 3,11, 1,11,10,-1,-1,-1,-1},
            { 0, 9, 1, 4, 8, 5, 8,10, 5, 8,11,10,-1},
            { 3, 4, 0, 3,10, 4, 4,10, 5, 3,11,10,-1},
            { 4, 8, 5, 5, 8,10, 8,11,10,-1,-1,-1,-1},
            { 9, 5, 7, 9, 7, 8,-1,-1,-1,-1,-1,-1,-1},
            { 0, 9, 3, 9, 5, 3, 3, 5, 7,-1,-1,-1,-1},
            { 8, 0, 7, 0, 1, 7, 7, 1, 5,-1,-1,-1,-1},
            { 1, 7, 3, 1, 5, 7,-1,-1,-1,-1,-1,-1,-1},
            { 1, 2,10, 5, 7, 8, 5, 8, 9,-1,-1,-1,-1},
            { 9, 1, 0,10, 5, 2, 5, 3, 2, 5, 7, 3,-1},
            { 5, 2,10, 8, 2, 5, 8, 5, 7, 8, 0, 2,-1},
            {10, 5, 2, 2, 5, 3, 5, 7, 3,-1,-1,-1,-1},
            {11, 2, 3, 8, 9, 5, 8, 5, 7,-1,-1,-1,-1},
            { 9, 2, 0, 9, 7, 2, 2, 7,11, 9, 5, 7,-1},
            { 0, 3, 8, 2, 1,11, 1, 7,11, 1, 5, 7,-1},
            { 2, 1,11,11, 1, 7, 1, 5, 7,-1,-1,-1,-1},
            { 3, 9, 1, 3, 8, 9, 7,11,10, 7,10, 5,-1},
            { 9, 1, 0,10, 7,11,10, 5, 7,-1,-1,-1,-1},
            { 3, 8, 0, 7,10, 5, 7,11,10,-1,-1,-1,-1},
            {11, 5, 7,11,10, 5,-1,-1,-1,-1,-1,-1,-1},
            {10, 6, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 8, 3, 0,10, 6, 5,-1,-1,-1,-1,-1,-1,-1},
            { 0, 1, 9, 5,10, 6,-1,-1,-1,-1,-1,-1,-1},
            {10, 6, 5, 9, 8, 3, 9, 3, 1,-1,-1,-1,-1},
            { 1, 2, 6, 1, 6, 5,-1,-1,-1,-1,-1,-1,-1},
            { 0, 8, 3, 2, 6, 5, 2, 5, 1,-1,-1,-1,-1},
            { 5, 9, 6, 9, 0, 6, 6, 0, 2,-1,-1,-1,-1},
            { 9, 6, 5, 3, 6, 9, 3, 9, 8, 3, 2, 6,-1},
            { 3,11, 2,10, 6, 5,-1,-1,-1,-1,-1,-1,-1},
            { 6, 5,10, 2, 0, 8, 2, 8,11,-1,-1,-1,-1},
            { 1, 9, 0, 6, 5,10,11, 2, 3,-1,-1,-1,-1},
            { 1,10, 2, 5, 9, 6, 9,11, 6, 9, 8,11,-1},
            {11, 6, 3, 6, 5, 3, 3, 5, 1,-1,-1,-1,-1},
            { 0, 5, 1, 0,11, 5, 5,11, 6, 0, 8,11,-1},
            { 0, 5, 9, 0, 3, 5, 3, 6, 5, 3,11, 6,-1},
            { 5, 9, 6, 6, 9,11, 9, 8,11,-1,-1,-1,-1},
            {10, 6, 5, 4, 7, 8,-1,-1,-1,-1,-1,-1,-1},
            { 5,10, 6, 7, 3, 0, 7, 0, 4,-1,-1,-1,-1},
            { 5,10, 6, 0, 1, 9, 8, 4, 7,-1,-1,-1,-1},
            { 4, 5, 9, 6, 7,10, 7, 1,10, 7, 3, 1,-1},
            { 7, 8, 4, 5, 1, 2, 5, 2, 6,-1,-1,-1,-1},
            { 4, 1, 0, 4, 5, 1, 6, 7, 3, 6, 3, 2,-1},
            { 9, 4, 5, 8, 0, 7, 0, 6, 7, 0, 2, 6,-1},
            { 4, 5, 9, 6, 3, 2, 6, 7, 3,-1,-1,-1,-1},
            { 7, 8, 4, 2, 3,11,10, 6, 5,-1,-1,-1,-1},
            {11, 6, 7,10, 2, 5, 2, 4, 5, 2, 0, 4,-1},
            {11, 6, 7, 8, 0, 3, 1,10, 2, 9, 4, 5,-1},
            { 6, 7,11, 1,10, 2, 9, 4, 5,-1,-1,-1,-1},
            { 6, 7,11, 4, 5, 8, 5, 3, 8, 5, 1, 3,-1},
            { 6, 7,11, 4, 1, 0, 4, 5, 1,-1,-1,-1,-1},
            { 4, 5, 9, 3, 8, 0,11, 6, 7,-1,-1,-1,-1},
            { 9, 4, 5, 7,11, 6,-1,-1,-1,-1,-1,-1,-1},
            {10, 6, 4,10, 4, 9,-1,-1,-1,-1,-1,-1,-1},
            { 8, 3, 0, 9,10, 6, 9, 6, 4,-1,-1,-1,-1},
            { 1,10, 0,10, 6, 0, 0, 6, 4,-1,-1,-1,-1},
            { 8, 6, 4, 8, 1, 6, 6, 1,10, 8, 3, 1,-1},
            { 9, 1, 4, 1, 2, 4, 4, 2, 6,-1,-1,-1,-1},
            { 1, 0, 9, 3, 2, 8, 2, 4, 8, 2, 6, 4,-1},
            { 2, 4, 0, 2, 6, 4,-1,-1,-1,-1,-1,-1,-1},
            { 3, 2, 8, 8, 2, 4, 2, 6, 4,-1,-1,-1,-1},
            { 2, 3,11, 6, 4, 9, 6, 9,10,-1,-1,-1,-1},
            { 0,10, 2, 0, 9,10, 4, 8,11, 4,11, 6,-1},
            {10, 2, 1,11, 6, 3, 6, 0, 3, 6, 4, 0,-1},
            {10, 2, 1,11, 4, 8,11, 6, 4,-1,-1,-1,-1},
            { 1, 4, 9,11, 4, 1,11, 1, 3,11, 6, 4,-1},
            { 0, 9, 1, 4,11, 6, 4, 8,11,-1,-1,-1,-1},
            {11, 6, 3, 3, 6, 0, 6, 4, 0,-1,-1,-1,-1},
            { 8, 6, 4, 8,11, 6,-1,-1,-1,-1,-1,-1,-1},
            { 6, 7,10, 7, 8,10,10, 8, 9,-1,-1,-1,-1},
            { 9, 3, 0, 6, 3, 9, 6, 9,10, 6, 7, 3,-1},
            { 6, 1,10, 6, 7, 1, 7, 0, 1, 7, 8, 0,-1},
            { 6, 7,10,10, 7, 1, 7, 3, 1,-1,-1,-1,-1},
            { 7, 2, 6, 7, 9, 2, 2, 9, 1, 7, 8, 9,-1},
            { 1, 0, 9, 3, 6, 7, 3, 2, 6,-1,-1,-1,-1},
            { 8, 0, 7, 7, 0, 6, 0, 2, 6,-1,-1,-1,-1},
            { 2, 7, 3, 2, 6, 7,-1,-1,-1,-1,-1,-1,-1},
            { 7,11, 6, 3, 8, 2, 8,10, 2, 8, 9,10,-1},
            {11, 6, 7,10, 0, 9,10, 2, 0,-1,-1,-1,-1},
            { 2, 1,10, 7,11, 6, 8, 0, 3,-1,-1,-1,-1},
            { 1,10, 2, 6, 7,11,-1,-1,-1,-1,-1,-1,-1},
            { 7,11, 6, 3, 9, 1, 3, 8, 9,-1,-1,-1,-1},
            { 9, 1, 0,11, 6, 7,-1,-1,-1,-1,-1,-1,-1},
            { 0, 3, 8,11, 6, 7,-1,-1,-1,-1,-1,-1,-1},
            {11, 6, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            {11, 7, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 0, 8, 3,11, 7, 6,-1,-1,-1,-1,-1,-1,-1},
            { 9, 0, 1,11, 7, 6,-1,-1,-1,-1,-1,-1,-1},
            { 7, 6,11, 3, 1, 9, 3, 9, 8,-1,-1,-1,-1},
            { 1, 2,10, 6,11, 7,-1,-1,-1,-1,-1,-1,-1},
            { 2,10, 1, 7, 6,11, 8, 3, 0,-1,-1,-1,-1},
            {11, 7, 6,10, 9, 0,10, 0, 2,-1,-1,-1,-1},
            { 7, 6,11, 3, 2, 8, 8, 2,10, 8,10, 9,-1},
            { 2, 3, 7, 2, 7, 6,-1,-1,-1,-1,-1,-1,-1},
            { 8, 7, 0, 7, 6, 0, 0, 6, 2,-1,-1,-1,-1},
            { 1, 9, 0, 3, 7, 6, 3, 6, 2,-1,-1,-1,-1},
            { 7, 6, 2, 7, 2, 9, 2, 1, 9, 7, 9, 8,-1},
            { 6,10, 7,10, 1, 7, 7, 1, 3,-1,-1,-1,-1},
            { 6,10, 1, 6, 1, 7, 7, 1, 0, 7, 0, 8,-1},
            { 9, 0, 3, 6, 9, 3, 6,10, 9, 6, 3, 7,-1},
            { 6,10, 7, 7,10, 8,10, 9, 8,-1,-1,-1,-1},
            { 8, 4, 6, 8, 6,11,-1,-1,-1,-1,-1,-1,-1},
            {11, 3, 6, 3, 0, 6, 6, 0, 4,-1,-1,-1,-1},
            { 0, 1, 9, 4, 6,11, 4,11, 8,-1,-1,-1,-1},
            { 1, 9, 4,11, 1, 4,11, 3, 1,11, 4, 6,-1},
            {10, 1, 2,11, 8, 4,11, 4, 6,-1,-1,-1,-1},
            {10, 1, 2,11, 3, 6, 6, 3, 0, 6, 0, 4,-1},
            { 0, 2,10, 0,10, 9, 4,11, 8, 4, 6,11,-1},
            { 2,11, 3, 6, 9, 4, 6,10, 9,-1,-1,-1,-1},
            { 3, 8, 2, 8, 4, 2, 2, 4, 6,-1,-1,-1,-1},
            { 2, 0, 4, 2, 4, 6,-1,-1,-1,-1,-1,-1,-1},
            { 1, 9, 0, 3, 8, 2, 2, 8, 4, 2, 4, 6,-1},
            { 9, 4, 1, 1, 4, 2, 4, 6, 2,-1,-1,-1,-1},
            { 8, 4, 6, 8, 6, 1, 6,10, 1, 8, 1, 3,-1},
            { 1, 0,10,10, 0, 6, 0, 4, 6,-1,-1,-1,-1},
            { 8, 0, 3, 9, 6,10, 9, 4, 6,-1,-1,-1,-1},
            {10, 4, 6,10, 9, 4,-1,-1,-1,-1,-1,-1,-1},
            { 9, 5, 4, 7, 6,11,-1,-1,-1,-1,-1,-1,-1},
            { 4, 9, 5, 3, 0, 8,11, 7, 6,-1,-1,-1,-1},
            { 6,11, 7, 4, 0, 1, 4, 1, 5,-1,-1,-1,-1},
            { 6,11, 7, 4, 8, 5, 5, 8, 3, 5, 3, 1,-1},
            { 6,11, 7, 1, 2,10, 9, 5, 4,-1,-1,-1,-1},
            {11, 7, 6, 8, 3, 0, 1, 2,10, 9, 5, 4,-1},
            {11, 7, 6,10, 5, 2, 2, 5, 4, 2, 4, 0,-1},
            { 7, 4, 8, 2,11, 3,10, 5, 6,-1,-1,-1,-1},
            { 4, 9, 5, 6, 2, 3, 6, 3, 7,-1,-1,-1,-1},
            { 9, 5, 4, 8, 7, 0, 0, 7, 6, 0, 6, 2,-1},
            { 4, 0, 1, 4, 1, 5, 6, 3, 7, 6, 2, 3,-1},
            { 7, 4, 8, 5, 2, 1, 5, 6, 2,-1,-1,-1,-1},
            { 4, 9, 5, 6,10, 7, 7,10, 1, 7, 1, 3,-1},
            { 5, 6,10, 0, 9, 1, 8, 7, 4,-1,-1,-1,-1},
            { 5, 6,10, 7, 0, 3, 7, 4, 0,-1,-1,-1,-1},
            {10, 5, 6, 4, 8, 7,-1,-1,-1,-1,-1,-1,-1},
            { 5, 6, 9, 6,11, 9, 9,11, 8,-1,-1,-1,-1},
            { 0, 9, 5, 0, 5, 3, 3, 5, 6, 3, 6,11,-1},
            { 0, 1, 5, 0, 5,11, 5, 6,11, 0,11, 8,-1},
            {11, 3, 6, 6, 3, 5, 3, 1, 5,-1,-1,-1,-1},
            { 1, 2,10, 5, 6, 9, 9, 6,11, 9,11, 8,-1},
            { 1, 0, 9, 6,10, 5,11, 3, 2,-1,-1,-1,-1},
            { 6,10, 5, 2, 8, 0, 2,11, 8,-1,-1,-1,-1},
            { 3, 2,11,10, 5, 6,-1,-1,-1,-1,-1,-1,-1},
            { 9, 5, 6, 3, 9, 6, 3, 8, 9, 3, 6, 2,-1},
            { 5, 6, 9, 9, 6, 0, 6, 2, 0,-1,-1,-1,-1},
            { 0, 3, 8, 2, 5, 6, 2, 1, 5,-1,-1,-1,-1},
            { 1, 6, 2, 1, 5, 6,-1,-1,-1,-1,-1,-1,-1},
            {10, 5, 6, 9, 3, 8, 9, 1, 3,-1,-1,-1,-1},
            { 0, 9, 1, 5, 6,10,-1,-1,-1,-1,-1,-1,-1},
            { 8, 0, 3,10, 5, 6,-1,-1,-1,-1,-1,-1,-1},
            {10, 5, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            {11, 7, 5,11, 5,10,-1,-1,-1,-1,-1,-1,-1},
            { 3, 0, 8, 7, 5,10, 7,10,11,-1,-1,-1,-1},
            { 9, 0, 1,10,11, 7,10, 7, 5,-1,-1,-1,-1},
            { 3, 1, 9, 3, 9, 8, 7,10,11, 7, 5,10,-1},
            { 2,11, 1,11, 7, 1, 1, 7, 5,-1,-1,-1,-1},
            { 0, 8, 3, 2,11, 1, 1,11, 7, 1, 7, 5,-1},
            { 9, 0, 2, 9, 2, 7, 2,11, 7, 9, 7, 5,-1},
            {11, 3, 2, 8, 5, 9, 8, 7, 5,-1,-1,-1,-1},
            {10, 2, 5, 2, 3, 5, 5, 3, 7,-1,-1,-1,-1},
            { 5,10, 2, 8, 5, 2, 8, 7, 5, 8, 2, 0,-1},
            { 9, 0, 1,10, 2, 5, 5, 2, 3, 5, 3, 7,-1},
            { 1,10, 2, 5, 8, 7, 5, 9, 8,-1,-1,-1,-1},
            { 1, 3, 7, 1, 7, 5,-1,-1,-1,-1,-1,-1,-1},
            { 8, 7, 0, 0, 7, 1, 7, 5, 1,-1,-1,-1,-1},
            { 0, 3, 9, 9, 3, 5, 3, 7, 5,-1,-1,-1,-1},
            { 9, 7, 5, 9, 8, 7,-1,-1,-1,-1,-1,-1,-1},
            { 4, 5, 8, 5,10, 8, 8,10,11,-1,-1,-1,-1},
            { 3, 0, 4, 3, 4,10, 4, 5,10, 3,10,11,-1},
            { 0, 1, 9, 4, 5, 8, 8, 5,10, 8,10,11,-1},
            { 5, 9, 4, 1,11, 3, 1,10,11,-1,-1,-1,-1},
            { 8, 4, 5, 2, 8, 5, 2,11, 8, 2, 5, 1,-1},
            { 3, 2,11, 1, 4, 5, 1, 0, 4,-1,-1,-1,-1},
            { 9, 4, 5, 8, 2,11, 8, 0, 2,-1,-1,-1,-1},
            {11, 3, 2, 9, 4, 5,-1,-1,-1,-1,-1,-1,-1},
            { 3, 8, 4, 3, 4, 2, 2, 4, 5, 2, 5,10,-1},
            {10, 2, 5, 5, 2, 4, 2, 0, 4,-1,-1,-1,-1},
            { 0, 3, 8, 5, 9, 4,10, 2, 1,-1,-1,-1,-1},
            { 2, 1,10, 9, 4, 5,-1,-1,-1,-1,-1,-1,-1},
            { 4, 5, 8, 8, 5, 3, 5, 1, 3,-1,-1,-1,-1},
            { 5, 0, 4, 5, 1, 0,-1,-1,-1,-1,-1,-1,-1},
            { 3, 8, 0, 4, 5, 9,-1,-1,-1,-1,-1,-1,-1},
            { 9, 4, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 7, 4,11, 4, 9,11,11, 9,10,-1,-1,-1,-1},
            { 3, 0, 8, 7, 4,11,11, 4, 9,11, 9,10,-1},
            {11, 7, 4, 1,11, 4, 1,10,11, 1, 4, 0,-1},
            { 8, 7, 4,11, 1,10,11, 3, 1,-1,-1,-1,-1},
            { 2,11, 7, 2, 7, 1, 1, 7, 4, 1, 4, 9,-1},
            { 3, 2,11, 4, 8, 7, 9, 1, 0,-1,-1,-1,-1},
            { 7, 4,11,11, 4, 2, 4, 0, 2,-1,-1,-1,-1},
            { 2,11, 3, 7, 4, 8,-1,-1,-1,-1,-1,-1,-1},
            { 2, 3, 7, 2, 7, 9, 7, 4, 9, 2, 9,10,-1},
            { 4, 8, 7, 0,10, 2, 0, 9,10,-1,-1,-1,-1},
            { 2, 1,10, 0, 7, 4, 0, 3, 7,-1,-1,-1,-1},
            {10, 2, 1, 8, 7, 4,-1,-1,-1,-1,-1,-1,-1},
            { 9, 1, 4, 4, 1, 7, 1, 3, 7,-1,-1,-1,-1},
            { 1, 0, 9, 8, 7, 4,-1,-1,-1,-1,-1,-1,-1},
            { 3, 4, 0, 3, 7, 4,-1,-1,-1,-1,-1,-1,-1},
            { 8, 7, 4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 8, 9,10, 8,10,11,-1,-1,-1,-1,-1,-1,-1},
            { 0, 9, 3, 3, 9,11, 9,10,11,-1,-1,-1,-1},
            { 1,10, 0, 0,10, 8,10,11, 8,-1,-1,-1,-1},
            {10, 3, 1,10,11, 3,-1,-1,-1,-1,-1,-1,-1},
            { 2,11, 1, 1,11, 9,11, 8, 9,-1,-1,-1,-1},
            {11, 3, 2, 0, 9, 1,-1,-1,-1,-1,-1,-1,-1},
            {11, 0, 2,11, 8, 0,-1,-1,-1,-1,-1,-1,-1},
            {11, 3, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 3, 8, 2, 2, 8,10, 8, 9,10,-1,-1,-1,-1},
            { 9, 2, 0, 9,10, 2,-1,-1,-1,-1,-1,-1,-1},
            { 8, 0, 3, 1,10, 2,-1,-1,-1,-1,-1,-1,-1},
            {10, 2, 1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 8, 1, 3, 8, 9, 1,-1,-1,-1,-1,-1,-1,-1},
            { 9, 1, 0,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            { 8, 0, 3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            {-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1}};
*/


    }
}
