// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;


namespace g3
{

    /// <summary>
    /// [RMS] variant of DenseGridTrilinearImplicit that does lazy evaluation
    /// of Grid values.
    /// 
    /// Tri-linear interpolant for a 3D dense grid. Supports grid translation
    /// via GridOrigin, but does not support scaling or rotation. If you need those,
    /// you can wrap this in something that does the xform.
    /// </summary>
	public class CachingDenseGridTrilinearImplicit : BoundedImplicitFunction3d
    {
        public DenseGrid3f Grid;
        public double CellSize;
        public Vector3d GridOrigin;

        public ImplicitFunction3d AnalyticF;

        // value to return if query point is outside grid (in an SDF
        // outside is usually positive). Need to do math with this value,
        // so don't use double.MaxValue or square will overflow
        public double Outside = Math.Sqrt(Math.Sqrt(double.MaxValue));

        public float Invalid = float.MaxValue;

        public CachingDenseGridTrilinearImplicit(Vector3d gridOrigin, double cellSize, Vector3i gridDimensions)
        {
            Grid = new DenseGrid3f(gridDimensions.x, gridDimensions.y, gridDimensions.z, Invalid);
            GridOrigin = gridOrigin;
            CellSize = cellSize;
        }

        public AxisAlignedBox3d Bounds()
		{
			return new AxisAlignedBox3d(
				GridOrigin.x, GridOrigin.y, GridOrigin.z,
				GridOrigin.x + CellSize * Grid.ni, 
				GridOrigin.y + CellSize * Grid.nj, 
				GridOrigin.z + CellSize * Grid.nk);
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
            if (x0 < 0 || (x0+1) >= Grid.ni ||
                y0 < 0 || y1 >= Grid.nj ||
                z0 < 0 || z1 >= Grid.nk)
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
            float fa, fb;
            Grid.get_x_pair(i, j, k, out fa, out fb);

            if (fa == Invalid) {
                Vector3d p = grid_position(i, j, k);
                a = AnalyticF.Value(ref p);
                Grid[i, j, k] = (float)a;
            } else
                a = fa;

            if (fb == Invalid) {
                Vector3d p = grid_position(i+1, j, k);
                b = AnalyticF.Value(ref p);
                Grid[i+1, j, k] = (float)b;
            } else
                b = fb;
        }


        Vector3d grid_position(int i, int j, int k) {
            return new Vector3d( GridOrigin.x + CellSize * i, GridOrigin.y + CellSize * j, GridOrigin.z + CellSize*k );
        }


        public Vector3d Gradient(ref Vector3d pt)
        {
            Vector3d gridPt = new Vector3d(
                ((pt.x - GridOrigin.x) / CellSize),
                ((pt.y - GridOrigin.y) / CellSize),
                ((pt.z - GridOrigin.z) / CellSize));

            // clamp to grid
            if (gridPt.x < 0 || gridPt.x >= Grid.ni - 1 ||
                gridPt.y < 0 || gridPt.y >= Grid.nj - 1 ||
                gridPt.z < 0 || gridPt.z >= Grid.nk - 1)
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
