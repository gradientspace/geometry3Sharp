// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Threading;
using g3;

namespace gs
{

    /// <summary>
    /// Sample a scalar function on a discrete grid. Can sample full grid, or
    /// compute values around a specific iso-contour and then fill in rest of grid
    /// with correctly-signed values via fast sweeping (this is the default)
    /// 
    /// TODO: 
    ///   - I think we are over-exploring the grid most of the time. eg along an x-ray that
    ///     intersects the surface, we only need at most 2 cells, but we are computing at least 3,
    ///     and possibly 5. 
    ///   - it may be better to use something like bloomenthal polygonizer continuation? where we 
    ///     are keeping track of active edges instead of active cells?
    /// 
    /// </summary>
    public class MeshScalarSamplingGrid
    {
        public DMesh3 Mesh;
        public Func<Vector3d, double> ScalarF;

        // size of cubes in the grid
        public double CellSize;

        // how many cells around border should we keep
        public int BufferCells = 1;

        // Should we compute values at all grid cells (expensive!!) or only in narrow band.
        // In narrow-band mode, we guess rest of values by propagating along x-rows
        public enum ComputeModes
        {
            FullGrid = 0,
            NarrowBand = 1
        }
        public ComputeModes ComputeMode = ComputeModes.NarrowBand;

        // in narrow-band mode, if mesh is not closed, we will explore space around this iso-value
        public float IsoValue = 0.5f;

        // in NarrowBand mode, we compute mesh SDF grid, if true then it can be accessed
        // via SDFGrid property after Compute()
        public bool WantMeshSDFGrid = true;

        /// <summary> if this function returns true, we should abort calculation </summary>
        public Func<bool> CancelF = () => { return false; };

        public bool DebugPrint = false;

        // computed results
        Vector3f grid_origin;
        DenseGrid3f scalar_grid;

        // sdf grid we compute in narrow-band mode
        MeshSignedDistanceGrid mesh_sdf;


        public MeshScalarSamplingGrid(DMesh3 mesh, double cellSize, Func<Vector3d, double> scalarF)
        {
            Mesh = mesh;
            ScalarF = scalarF;
            CellSize = cellSize;
        }


        public void Compute()
        {
            // figure out origin & dimensions
            AxisAlignedBox3d bounds = Mesh.CachedBounds;

            float fBufferWidth = 2 * BufferCells * (float)CellSize;
            grid_origin = (Vector3f)bounds.Min - fBufferWidth * Vector3f.One;
            Vector3f max = (Vector3f)bounds.Max + fBufferWidth * Vector3f.One;
            int ni = (int)((max.x - grid_origin.x) / (float)CellSize) + 1;
            int nj = (int)((max.y - grid_origin.y) / (float)CellSize) + 1;
            int nk = (int)((max.z - grid_origin.z) / (float)CellSize) + 1;

            scalar_grid = new DenseGrid3f();
            if ( ComputeMode == ComputeModes.FullGrid )
                make_grid_dense(grid_origin, (float)CellSize, ni, nj, nk, scalar_grid);
            else
                make_grid(grid_origin, (float)CellSize, ni, nj, nk, scalar_grid);
        }



        public Vector3i Dimensions {
            get { return new Vector3i(scalar_grid.ni, scalar_grid.nj, scalar_grid.nk); }
        }

        /// <summary>
        /// scalar-values grid available after calling Compute()
        /// </summary>
        public DenseGrid3f Grid {
            get { return scalar_grid; }
        }

        /// <summary>
        /// Origin of the grid, in same coordinates as mesh
        /// </summary>
        public Vector3f GridOrigin {
            get { return grid_origin; }
        }


        /// <summary>
        /// If ComputeMode==NarrowBand, then we internally compute a signed-distance grid,
        /// which will hang onto 
        /// </summary>
        public MeshSignedDistanceGrid SDFGrid {
            get { return mesh_sdf; }
        }


        public float this[int i, int j, int k] {
            get { return scalar_grid[i, j, k]; }
        }

        public Vector3f CellCenter(int i, int j, int k)
        {
            return new Vector3f((float)i * CellSize + grid_origin.x,
                                (float)j * CellSize + grid_origin.y,
                                (float)k * CellSize + grid_origin.z);
        }




        void make_grid(Vector3f origin, float dx,
                             int ni, int nj, int nk,
                             DenseGrid3f scalars)
        {
            scalars.resize(ni, nj, nk);
            scalars.assign(float.MaxValue); // sentinel

            if (DebugPrint) System.Console.WriteLine("start");

            // Ok, because the whole idea is that the surface might have holes, we are going to 
            // compute values along known triangles and then propagate the computed region outwards
            // until any iso-sign-change is surrounded.
            // To seed propagation, we compute unsigned SDF and then compute values for any voxels
            // containing surface (ie w/ distance smaller than cellsize)

            // compute unsigned SDF
            MeshSignedDistanceGrid sdf = new MeshSignedDistanceGrid(Mesh, CellSize) { ComputeSigns = false };
            sdf.CancelF = this.CancelF;
            sdf.Compute();
            if (CancelF())
                return;

            DenseGrid3f distances = sdf.Grid;
            if (WantMeshSDFGrid)
                mesh_sdf = sdf;
            if (DebugPrint) System.Console.WriteLine("done initial sdf");

            // compute values at surface voxels
            double ox = (double)origin[0], oy = (double)origin[1], oz = (double)origin[2];
            gParallel.ForEach(gIndices.Grid3IndicesYZ(nj, nk), (jk) => {
                if (CancelF())
                    return;
                for (int i = 0; i < ni; ++i) {
                    Vector3i ijk = new Vector3i(i, jk.y, jk.z);
                    float dist = distances[ijk];
                    // this could be tighter? but I don't think it matters...
                    if (dist < CellSize) {
                        Vector3d gx = new Vector3d((float)ijk.x * dx + origin[0], (float)ijk.y * dx + origin[1], (float)ijk.z * dx + origin[2]);
                        scalars[ijk] = (float)ScalarF(gx);
                    }
                }
            });
            if (CancelF())
                return;

            if (DebugPrint) System.Console.WriteLine("done narrow-band");

            // Now propagate outwards from computed voxels.
            // Current procedure is to check 26-neighbours around each 'front' voxel,
            // and if there are any sign changes, that neighbour is added to front.
            // Front is initialized w/ all voxels we computed above

            AxisAlignedBox3i bounds = scalars.Bounds;
            bounds.Max -= Vector3i.One;

            // since we will be computing new values as necessary, we cannot use
            // grid to track whether a voxel is 'new' or not. 
            // So, using 3D bitmap intead - is updated at end of each pass.
            Bitmap3 bits = new Bitmap3(new Vector3i(ni, nj, nk));
            List<Vector3i> cur_front = new List<Vector3i>();
            foreach (Vector3i ijk in scalars.Indices()) {
                if (scalars[ijk] != float.MaxValue) {
                    cur_front.Add(ijk);
                    bits[ijk] = true;
                }
            }
            if (CancelF())
                return;

            // Unique set of 'new' voxels to compute in next iteration.
            HashSet<Vector3i> queue = new HashSet<Vector3i>();
            SpinLock queue_lock = new SpinLock();

            while (true) {
                if (CancelF())
                    return;

                // can process front voxels in parallel
                bool abort = false;  int iter_count = 0;
                gParallel.ForEach(cur_front, (ijk) => {
                    Interlocked.Increment(ref iter_count);
                    if (iter_count % 100 == 0)
                        abort = CancelF();
                    if (abort)
                        return;

                    float val = scalars[ijk];

                    // check 26-neighbours to see if we have a crossing in any direction
                    for (int k = 0; k < 26; ++k) {
                        Vector3i nijk = ijk + gIndices.GridOffsets26[k];
                        if (bounds.Contains(nijk) == false)
                            continue;
                        float val2 = scalars[nijk];
                        if (val2 == float.MaxValue) {
                            Vector3d gx = new Vector3d((float)nijk.x * dx + origin[0], (float)nijk.y * dx + origin[1], (float)nijk.z * dx + origin[2]);
                            val2 = (float)ScalarF(gx);
                            scalars[nijk] = val2;
                        }
                        if (bits[nijk] == false) {
                            // this is a 'new' voxel this round.
                            // If we have an iso-crossing, add it to the front next round
                            bool crossing = (val < IsoValue && val2 > IsoValue) ||
                                            (val > IsoValue && val2 < IsoValue);
                            if (crossing) {
                                bool taken = false;
                                queue_lock.Enter(ref taken);
                                queue.Add(nijk);
                                queue_lock.Exit();
                            }
                        }
                    }
                });
                if (DebugPrint) System.Console.WriteLine("front has {0} voxels", queue.Count);
                if (queue.Count == 0)
                    break;

                // update known-voxels list and create front for next iteration
                foreach (Vector3i idx in queue)
                    bits[idx] = true;
                cur_front.Clear();
                cur_front.AddRange(queue);
                queue.Clear();
            }
            if (DebugPrint) System.Console.WriteLine("done front-prop");

            if (DebugPrint) {
                int filled = 0;
                foreach (Vector3i ijk in scalars.Indices()) {
                    if (scalars[ijk] != float.MaxValue)
                        filled++;
                }
                System.Console.WriteLine("filled: {0} / {1}  -  {2}%", filled, ni * nj * nk,
                                    (double)filled / (double)(ni * nj * nk) * 100.0);
            }

            if (CancelF())
                return;

            // fill in the rest of the grid by propagating know values
            fill_spans(ni, nj, nk, scalars);

            if (DebugPrint) System.Console.WriteLine("done sweep");


        }










        void make_grid_dense(Vector3f origin, float dx,
                             int ni, int nj, int nk,
                             DenseGrid3f scalars)
        {
            scalars.resize(ni, nj, nk);

            bool abort = false; int count = 0;
            gParallel.ForEach(scalars.Indices(), (ijk) => {
                Interlocked.Increment(ref count);
                if (count % 100 == 0)
                    abort = CancelF();
                if (abort)
                    return;

                Vector3d gx = new Vector3d((float)ijk.x * dx + origin[0], (float)ijk.y * dx + origin[1], (float)ijk.z * dx + origin[2]);
                scalars[ijk] = (float)ScalarF(gx);
            });

        }   // end make_level_set_3





        void fill_spans(int ni, int nj, int nk, DenseGrid3f scalars)
        {
            gParallel.ForEach(gIndices.Grid3IndicesYZ(nj, nk), (idx) => {
                int j = idx.y, k = idx.z;
                float last = scalars[0, j, k];
                if (last == float.MaxValue)
                    last = 0;
                for (int i = 0; i < ni; ++i) {
                    if (scalars[i, j, k] == float.MaxValue) {
                        scalars[i, j, k] = last;
                    } else {
                        last = scalars[i, j, k];
                        if (last < IsoValue)   // propagate zeros on outside
                            last = 0;
                    }
                }
            });
        }




        
        




    }
}
