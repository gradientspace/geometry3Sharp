using System;

namespace g3
{

    /// <summary>
    /// 
    /// </summary>
    public class BlockSupportGenerator
    {
        public DMesh3 Mesh;

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
        /// subtract mesh from generated support
        /// </summary>
        public bool SubtractMesh = false;

        /// <summary>
        /// offset applied to subtracted mesh. Note that this is uniform offset, and so
        /// it leaves space at top and bottom of support as well!
        /// </summary>
        public double SubtractMeshOffset = 0.05;


        /// <summary> if this function returns true, we should abort calculation </summary>
        public Func<bool> CancelF = () => { return false; };

        public bool DebugPrint = false;

        // computed results
        AxisAlignedBox3d grid_bounds;
        Vector3f grid_origin;
        DenseGrid3f volume_grid;
        MeshSignedDistanceGrid sdf;

        /*
         *  Results
         */

        public DMesh3 SupportMesh;


        public BlockSupportGenerator(DMesh3 mesh, double cellSize)
        {
            Mesh = mesh;
            CellSize = cellSize;
        }


        public BlockSupportGenerator(DMesh3 mesh, int grid_resolution)
        {
            Mesh = mesh;

            double maxdim = Math.Max(Mesh.CachedBounds.Width, Mesh.CachedBounds.Height);
            CellSize = maxdim / grid_resolution;
        }


        public void Generate()
        {
            // figure out origin & dimensions
            grid_bounds = Mesh.CachedBounds;
            if (ForceMinY != float.MaxValue )
                grid_bounds.Min.y = ForceMinY;

            // expand grid so we have some border space in x and z
            float fBufferWidth = 2 * (float)CellSize;
            Vector3f b = new Vector3f(fBufferWidth, 0, fBufferWidth);
            grid_origin = (Vector3f)grid_bounds.Min - b;

            // need zero isovalue to be at y=0. right now we set yi=0 voxels to be -1, 
            // so if we nudge up half a cell, then interpolation with boundary outside 
            // value should be 0 right at cell border (seems to be working?)
            grid_origin.y += (float)CellSize * 0.5f;

            Vector3f max = (Vector3f)grid_bounds.Max + b;
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



        const float SUPPORT_TIP_TOP = -1.0f;

        void generate_support(Vector3f origin, float dx,
                             int ni, int nj, int nk,
                             DenseGrid3f supportGrid)
        {
            supportGrid.resize(ni, nj, nk);
            supportGrid.assign(1); // sentinel

            bool CHECKERBOARD = false;

            // compute unsigned SDF
            int exact_band = 1;
            if ( SubtractMesh && SubtractMeshOffset > 0 ) {
                int offset_band = (int)(SubtractMeshOffset / CellSize) + 1;
                exact_band = Math.Max(exact_band, offset_band);
            }
            sdf = new MeshSignedDistanceGrid(Mesh, CellSize) { ComputeSigns = true, ExactBandWidth = exact_band };
            sdf.CancelF = this.CancelF;
            sdf.Compute();
            if (CancelF())
                return;
            var distanceField = new DenseGridTrilinearImplicit(sdf.Grid, sdf.GridOrigin, sdf.CellSize);


            double angle = MathUtil.Clamp(OverhangAngleDeg, 0.01, 89.99);
            double cos_thresh = Math.Cos( angle * MathUtil.Deg2Rad );

            // Compute narrow-band distances. For each triangle, we find its grid-coord-bbox,
            // and compute exact distances within that box. The intersection_count grid
            // is also filled in this computation
            double ddx = (double)dx;
            double ox = (double)origin[0], oy = (double)origin[1], oz = (double)origin[2];
            Vector3d va = Vector3d.Zero, vb = Vector3d.Zero, vc = Vector3d.Zero;
            foreach (int tid in Mesh.TriangleIndices()) {
                if (tid % 100 == 0 && CancelF())
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
                int extra_band = 0;
                int i0 = MathUtil.Clamp(((int)MathUtil.Min(fip, fiq, fir)) - extra_band, 0, ni - 1);
                int i1 = MathUtil.Clamp(((int)MathUtil.Max(fip, fiq, fir)) + extra_band + 1, 0, ni - 1);
                int j0 = MathUtil.Clamp(((int)MathUtil.Min(fjp, fjq, fjr)) - extra_band, 0, nj - 1);
                int j1 = MathUtil.Clamp(((int)MathUtil.Max(fjp, fjq, fjr)) + extra_band + 1, 0, nj - 1);
                int k0 = MathUtil.Clamp(((int)MathUtil.Min(fkp, fkq, fkr)) - extra_band, 0, nk - 1);
                int k1 = MathUtil.Clamp(((int)MathUtil.Max(fkp, fkq, fkr)) + extra_band + 1, 0, nk - 1);

                // don't put into y=0 plane
                //if (j0 == 0)
                //    j0 = 1;

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
                                supportGrid[i, j, k] = SUPPORT_TIP_TOP;
                            }
                        }
                    }
                }
            }
            if (CancelF())
                return;

            fill_vertical_spans(supportGrid, distanceField);
            generate_mesh(supportGrid, distanceField);
        }




        Vector3d get_cell_center(Vector3i ijk) {
            return new Vector3d(ijk.x * CellSize, ijk.y * CellSize, ijk.z * CellSize) + this.GridOrigin;
        }
        Vector3d get_cell_center(int i, int j, int k) {
            return new Vector3d(i * CellSize, j * CellSize, k * CellSize) + this.GridOrigin;
        }



        void fill_vertical_spans(DenseGrid3f supportGrid, DenseGridTrilinearImplicit distanceField)
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
                            Vector3d cell_center = get_cell_center(i, j, k);
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

        }


        void generate_mesh(DenseGrid3f supportGrid, DenseGridTrilinearImplicit distanceField)
        {
            DenseGridTrilinearImplicit volume = new DenseGridTrilinearImplicit(
                supportGrid, GridOrigin, CellSize);
            BoundedImplicitFunction3d inputF = volume;

            if (SubtractMesh) {
                BoundedImplicitFunction3d sub = distanceField;
                if (SubtractMeshOffset > 0)
                    sub = new ImplicitOffset3d() { A = distanceField, Offset = SubtractMeshOffset };
                ImplicitDifference3d subtract = new ImplicitDifference3d() { A = volume, B = sub };
                inputF = subtract;
            }

            ImplicitHalfSpace3d cutPlane = new ImplicitHalfSpace3d() {
                Origin = Vector3d.Zero, Normal = Vector3d.AxisY
            };
            ImplicitDifference3d cut = new ImplicitDifference3d() { A = inputF, B = cutPlane };

            MarchingCubes mc = new MarchingCubes() { Implicit = cut, Bounds = grid_bounds, CubeSize = CellSize };
            mc.Bounds.Min.y = -2 * mc.CubeSize;
            mc.Bounds.Min.x -= 2 * mc.CubeSize; mc.Bounds.Min.z -= 2 * mc.CubeSize;
            mc.Bounds.Max.x += 2 * mc.CubeSize; mc.Bounds.Max.z += 2 * mc.CubeSize;
            mc.Generate();

            SupportMesh = mc.Mesh;
        }



    }





}
