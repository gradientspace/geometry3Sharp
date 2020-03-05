// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using g3;

namespace gs
{
    public class MarchingCubesPro
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



        public MarchingCubesPro()
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

            int nx = (int)(Bounds.Width / CubeSize) + 1;
            int ny = (int)(Bounds.Height / CubeSize) + 1;
            int nz = (int)(Bounds.Depth / CubeSize) + 1;
            CellDimensions = new Vector3i(nx, ny, nz);
            GridBounds = new AxisAlignedBox3i(Vector3i.Zero, CellDimensions);

            corner_values_grid = new DenseGrid3f(nx+1, ny+1, nz+1, float.MaxValue);
            edge_vertices = new Dictionary<long, int>();
            corner_values = new Dictionary<long, double>();

            if (ParallelCompute) {
                generate_parallel();
            } else {
                generate_basic();
            }
        }


        public void GenerateContinuation(IEnumerable<Vector3d> seeds)
        {
            Mesh = new DMesh3();

            int nx = (int)(Bounds.Width / CubeSize) + 1;
            int ny = (int)(Bounds.Height / CubeSize) + 1;
            int nz = (int)(Bounds.Depth / CubeSize) + 1;
            CellDimensions = new Vector3i(nx, ny, nz);
            GridBounds = new AxisAlignedBox3i(Vector3i.Zero, CellDimensions);

            if (LastGridBounds != GridBounds) {
                corner_values_grid = new DenseGrid3f(nx + 1, ny + 1, nz + 1, float.MaxValue);
                edge_vertices = new Dictionary<long, int>();
                corner_values = new Dictionary<long, double>();
                if (ParallelCompute)
                    done_cells = new DenseGrid3i(CellDimensions.x, CellDimensions.y, CellDimensions.z, 0);
            } else {
                edge_vertices.Clear();
                corner_values.Clear();
                corner_values_grid.assign(float.MaxValue);
                if (ParallelCompute)
                    done_cells.assign(0);
            }

            if (ParallelCompute) {
                generate_continuation_parallel(seeds);
            } else {
                generate_continuation(seeds);
            }

            LastGridBounds = GridBounds;
        }



        AxisAlignedBox3i GridBounds;
        AxisAlignedBox3i LastGridBounds;


        // we pass Cells around, this makes code cleaner
        class GridCell
        {
            public Vector3i[] i;    // indices of corners of cell
            public double[] f;      // field values at corners

            public GridCell()
            {
                // TODO we do not actually need to store i, we just need the min-corner!
                i = new Vector3i[8];
                f = new double[8];
            }

        }



        void corner_pos(ref Vector3i ijk, ref Vector3d p)
        {
            p.x = Bounds.Min.x + CubeSize * ijk.x;
            p.y = Bounds.Min.y + CubeSize * ijk.y;
            p.z = Bounds.Min.z + CubeSize * ijk.z;
        }
        Vector3d corner_pos(ref Vector3i ijk)
        {
            return new Vector3d(Bounds.Min.x + CubeSize * ijk.x,
                                 Bounds.Min.y + CubeSize * ijk.y,
                                 Bounds.Min.z + CubeSize * ijk.z);
        }
        Vector3i cell_index(Vector3d pos)
        {
            return new Vector3i(
                (int)((pos.x - Bounds.Min.x) / CubeSize),
                (int)((pos.y - Bounds.Min.y) / CubeSize),
                (int)((pos.z - Bounds.Min.z) / CubeSize));
        }



        //
        // corner and edge hash functions, these pack the coordinate
        // integers into 16-bits, so max of 65536 in any dimension.
        //


        long corner_hash(ref Vector3i idx) {
            return ((long)idx.x&0xFFFF) | (((long)idx.y&0xFFFF) << 16) | (((long)idx.z&0xFFFF) << 32);
        }
        long corner_hash(int x, int y, int z)
        {
            return ((long)x & 0xFFFF) | (((long)y & 0xFFFF) << 16) | (((long)z & 0xFFFF) << 32);
        }

        const int EDGE_X = 1 << 60;
        const int EDGE_Y = 1 << 61;
        const int EDGE_Z = 1 << 62;

        long edge_hash(ref Vector3i idx1, ref Vector3i idx2)
        {
            if ( idx1.x != idx2.x ) {
                int xlo = Math.Min(idx1.x, idx2.x);
                return corner_hash(xlo, idx1.y, idx1.z) | EDGE_X;
            } else if ( idx1.y != idx2.y ) {
                int ylo = Math.Min(idx1.y, idx2.y);
                return corner_hash(idx1.x, ylo, idx1.z) | EDGE_Y;
            } else {
                int zlo = Math.Min(idx1.z, idx2.z);
                return corner_hash(idx1.x, idx1.y, zlo) | EDGE_Z;
            }
        }



        //
        // Hash table for edge vertices
        //

        Dictionary<long, int> edge_vertices = new Dictionary<long, int>();
        SpinLock edge_vertices_lock = new SpinLock();

        int edge_vertex_id(ref Vector3i idx1, ref Vector3i idx2, double f1, double f2)
        {
            long hash = edge_hash(ref idx1, ref idx2);

            int vid = DMesh3.InvalidID;
            bool taken = false;
            edge_vertices_lock.Enter(ref taken);
            bool found = edge_vertices.TryGetValue(hash, out vid);
            edge_vertices_lock.Exit();

            if (found) 
                return vid;

            // ok this is a bit messy. We do not want to lock the entire hash table 
            // while we do find_iso. However it is possible that during this time we
            // are unlocked we have re-entered with the same edge. So when we
            // re-acquire the lock we need to check again that we have not already
            // computed this edge, otherwise we will end up with duplicate vertices!

            Vector3d pa = Vector3d.Zero, pb = Vector3d.Zero;
            corner_pos(ref idx1, ref pa);
            corner_pos(ref idx2, ref pb);
            Vector3d pos = Vector3d.Zero;
            find_iso(ref pa, ref pb, f1, f2, ref pos);

            taken = false;
            edge_vertices_lock.Enter(ref taken);
            if (edge_vertices.TryGetValue(hash, out vid) == false) {
                vid = append_vertex(pos);
                edge_vertices[hash] = vid;
            }
            edge_vertices_lock.Exit();

            return vid;
        }






        //
        // Store corner values in hash table. This doesn't make
        // sense if we are evaluating entire grid, way too slow.
        //

        Dictionary<long, double> corner_values = new Dictionary<long, double>();
        SpinLock corner_values_lock = new SpinLock();

        double corner_value(ref Vector3i idx)
        {
            long hash = corner_hash(ref idx);
            double value = 0;

            if ( corner_values.TryGetValue(hash, out value) == false) {
                Vector3d v = corner_pos(ref idx);
                value = Implicit.Value(ref v);
                corner_values[hash] = value;
            } 
            return value;
        }
        void initialize_cell_values(GridCell cell, bool shift)
        {
            bool taken = false;
            corner_values_lock.Enter(ref taken);

            if ( shift ) {
                cell.f[1] = corner_value(ref cell.i[1]);
                cell.f[2] = corner_value(ref cell.i[2]);
                cell.f[5] = corner_value(ref cell.i[5]);
                cell.f[6] = corner_value(ref cell.i[6]);
            } else {
                for (int i = 0; i < 8; ++i)
                    cell.f[i] = corner_value(ref cell.i[i]);
            }

            corner_values_lock.Exit();
        }



        //
        // store corner values in pre-allocated grid that has
        // float.MaxValue as sentinel. 
        // (note this is float grid, not double...)
        //

        DenseGrid3f corner_values_grid;

        double corner_value_grid(ref Vector3i idx)
        {
            double val = corner_values_grid[idx];
            if (val != float.MaxValue)
                return val;

            Vector3d v = corner_pos(ref idx);
            val = Implicit.Value(ref v);
            corner_values_grid[idx] = (float)val;
            return val;
        }
        void initialize_cell_values_grid(GridCell cell, bool shift)
        {
            if (shift) {
                cell.f[1] = corner_value_grid(ref cell.i[1]);
                cell.f[2] = corner_value_grid(ref cell.i[2]);
                cell.f[5] = corner_value_grid(ref cell.i[5]);
                cell.f[6] = corner_value_grid(ref cell.i[6]);
            } else {
                for (int i = 0; i < 8; ++i)
                    cell.f[i] = corner_value_grid(ref cell.i[i]);
            }
        }



        //
        // explicitly compute corner values as necessary
        //
        //

        double corner_value_nohash(ref Vector3i idx) {
            Vector3d v = corner_pos(ref idx);
            return Implicit.Value(ref v);
        }
        void initialize_cell_values_nohash(GridCell cell, bool shift)
        {
            if (shift) {
                cell.f[1] = corner_value_nohash(ref cell.i[1]);
                cell.f[2] = corner_value_nohash(ref cell.i[2]);
                cell.f[5] = corner_value_nohash(ref cell.i[5]);
                cell.f[6] = corner_value_nohash(ref cell.i[6]);
            } else {
                for (int i = 0; i < 8; ++i)
                    cell.f[i] = corner_value_nohash(ref cell.i[i]);
            }
        }





        /// <summary>
        /// compute 3D corner-positions and field values for cell at index
        /// </summary>
        void initialize_cell(GridCell cell, ref Vector3i idx)
        {
            cell.i[0] = new Vector3i(idx.x + 0, idx.y + 0, idx.z + 0);
            cell.i[1] = new Vector3i(idx.x + 1, idx.y + 0, idx.z + 0);
            cell.i[2] = new Vector3i(idx.x + 1, idx.y + 0, idx.z + 1);
            cell.i[3] = new Vector3i(idx.x + 0, idx.y + 0, idx.z + 1);
            cell.i[4] = new Vector3i(idx.x + 0, idx.y + 1, idx.z + 0);
            cell.i[5] = new Vector3i(idx.x + 1, idx.y + 1, idx.z + 0);
            cell.i[6] = new Vector3i(idx.x + 1, idx.y + 1, idx.z + 1);
            cell.i[7] = new Vector3i(idx.x + 0, idx.y + 1, idx.z + 1);

            //initialize_cell_values(cell, false);
            initialize_cell_values_grid(cell, false);
            //initialize_cell_values_nohash(cell, false);
        }


        // assume we just want to slide cell at xi-1 to cell at xi, while keeping
        // yi and zi constant. Then only x-coords change, and we have already 
        // computed half the values
        void shift_cell_x(GridCell cell, int xi)
        {
            cell.f[0] = cell.f[1];
            cell.f[3] = cell.f[2];
            cell.f[4] = cell.f[5];
            cell.f[7] = cell.f[6];

            cell.i[0].x = xi; cell.i[1].x = xi+1; cell.i[2].x = xi+1; cell.i[3].x = xi;
            cell.i[4].x = xi; cell.i[5].x = xi+1; cell.i[6].x = xi+1; cell.i[7].x = xi;

            //initialize_cell_values(cell, true);
            initialize_cell_values_grid(cell, true);
            //initialize_cell_values_nohash(cell, true);
        }


        bool parallel_mesh_access = false;
        SpinLock mesh_lock;

        /// <summary>
        /// processing z-slabs of cells in parallel
        /// </summary>
        void generate_parallel()
        {
            mesh_lock = new SpinLock();
            parallel_mesh_access = true;

            // [TODO] maybe shouldn't alway use Z axis here?
            gParallel.ForEach(Interval1i.Range(CellDimensions.z), (zi) => {
                GridCell cell = new GridCell();
                int[] vertlist = new int[12];
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


            parallel_mesh_access = false;
        }




        /// <summary>
        /// fully sequential version, no threading
        /// </summary>
        void generate_basic()
        {
            GridCell cell = new GridCell();
            int[] vertlist = new int[12];

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
        /// fully sequential version, no threading
        /// </summary>
        void generate_continuation(IEnumerable<Vector3d> seeds)
        {
            GridCell cell = new GridCell();
            int[] vertlist = new int[12];

            done_cells = new DenseGrid3i(CellDimensions.x, CellDimensions.y, CellDimensions.z, 0);

            List<Vector3i> stack = new List<Vector3i>();

            foreach (Vector3d seed in seeds) {
                Vector3i seed_idx = cell_index(seed);
                if (done_cells[seed_idx] == 1)
                    continue;
                stack.Add(seed_idx);
                done_cells[seed_idx] = 1;

                while ( stack.Count > 0 ) {
                    Vector3i idx = stack[stack.Count-1]; 
                    stack.RemoveAt(stack.Count-1);
                    if (CancelF())
                        return;

                    initialize_cell(cell, ref idx);
                    if ( polygonize_cell(cell, vertlist) ) {     // found crossing
                        foreach ( Vector3i o in gIndices.GridOffsets6 ) {
                            Vector3i nbr_idx = idx + o;
                            if (GridBounds.Contains(nbr_idx) && done_cells[nbr_idx] == 0) {
                                stack.Add(nbr_idx);
                                done_cells[nbr_idx] = 1;
                            }
                        }
                    }
                }
            }
        }




        /// <summary>
        /// parallel seed evaluation
        /// </summary>
        void generate_continuation_parallel(IEnumerable<Vector3d> seeds)
        {
            mesh_lock = new SpinLock();
            parallel_mesh_access = true;

            gParallel.ForEach(seeds, (seed) => {
                Vector3i seed_idx = cell_index(seed);
                if (set_cell_if_not_done(ref seed_idx) == false)
                    return;

                GridCell cell = new GridCell();
                int[] vertlist = new int[12];

                List<Vector3i> stack = new List<Vector3i>();
                stack.Add(seed_idx);

                while (stack.Count > 0) {
                    Vector3i idx = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    if (CancelF())
                        return;

                    initialize_cell(cell, ref idx);
                    if (polygonize_cell(cell, vertlist)) {     // found crossing
                        foreach (Vector3i o in gIndices.GridOffsets6) {
                            Vector3i nbr_idx = idx + o;
                            if (GridBounds.Contains(nbr_idx)) {
                                if (set_cell_if_not_done(ref nbr_idx) == true) { 
                                    stack.Add(nbr_idx);
                                }
                            }
                        }
                    }
                }
            });

            parallel_mesh_access = false;
        }



        DenseGrid3i done_cells;
        SpinLock done_cells_lock = new SpinLock();

        bool set_cell_if_not_done(ref Vector3i idx)
        {
            bool was_set = false;
            bool taken = false;
            done_cells_lock.Enter(ref taken);
            if (done_cells[idx] == 0) {
                done_cells[idx] = 1;
                was_set = true;
            }
            done_cells_lock.Exit();
            return was_set;
        }










        /// <summary>
        /// find edge crossings and generate triangles for this cell
        /// </summary>
        bool polygonize_cell(GridCell cell, int[] vertIndexList)
        {
            // construct bits of index into edge table, where bit for each
            // corner is 1 if that value is < isovalue.
            // This tell us which edges have sign-crossings, and the int value
            // of the bitmap is an index into the edge and triangle tables
            int cubeindex = 0, shift = 1;
            for (int i = 0; i < 8; ++i) {
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
            Vector3d pa = Vector3d.Zero, pb = Vector3d.Zero;
            for (int i = 0; i <= 11; i++) {
                if ((edgeTable[cubeindex] & shift) != 0) {
                    int a = edge_indices[i, 0], b = edge_indices[i, 1];
                    vertIndexList[i] = edge_vertex_id(ref cell.i[a], ref cell.i[b], cell.f[a], cell.f[b]);
                }
                shift <<= 1;
            }

            // now iterate through the set of triangles in triTable for this cube,
            // and emit triangles using the vertices we found.
            int tri_count = 0;
            for (int i = 0; triTable[cubeindex, i] != -1; i += 3) {
                int ta = triTable[cubeindex, i];
                int tb = triTable[cubeindex, i + 1];
                int tc = triTable[cubeindex, i + 2];
                int a = vertIndexList[ta], b = vertIndexList[tb], c = vertIndexList[tc];

                // if a corner is within tolerance of isovalue, then some triangles
                // will be degenerate, and we can skip them w/o resulting in cracks (right?)
                // !! this should never happen anymore...artifact of old hashtable impl
                if (a == b || a == c || b == c)
                    continue;

                /*int tid = */
                append_triangle(a, b, c);
                tri_count++;
            }

            return (tri_count > 0);
        }




        /// <summary>
        /// add vertex to mesh, with locking if we are computing in parallel
        /// </summary>
        int append_vertex(Vector3d v)
        {
            bool lock_taken = false;
            if (parallel_mesh_access) {
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
            if (parallel_mesh_access) {
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
                pIso = dt * p1 + (1.0 - dt) * p2;
                return;
            }
            if (Math.Abs(IsoValue - valp2) < 0.00001) {
                pIso = (dt) * p2 + (1.0 - dt) * p1;
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


        static readonly int[,] triTable = new int[256, 16]
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

    }
}
