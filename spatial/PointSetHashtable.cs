// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using g3;

namespace gs
{
	public class PointSetHashtable
	{
		IPointSet Points;
		DSparseGrid3<PointList> Grid;
		ShiftGridIndexer3 indexF;

		Vector3d Origin;
		double CellSize;

		public PointSetHashtable(IPointSet points)
		{
			this.Points = points;
		}


		public void Build(int maxAxisSubdivs = 64) {
			AxisAlignedBox3d bounds = BoundsUtil.Bounds(Points);
			double cellsize = bounds.MaxDim / (double)maxAxisSubdivs;
			Build(cellsize, bounds.Min);
		}

		public void Build(double cellSize, Vector3d origin) {
			Origin = origin;
			CellSize = cellSize;
			indexF = new ShiftGridIndexer3(Origin, CellSize);

			Grid = new DSparseGrid3<PointList>(new PointList());

			foreach ( int vid in Points.VertexIndices() ) {
				Vector3d v = Points.GetVertex(vid);
				Vector3i idx = indexF.ToGrid(v);
                PointList cell = Grid.Get(idx);
				cell.Add(vid);
			}
		}



		public bool FindInBall(Vector3d pt, double r, int[] buffer, out int buffer_count ) {
			buffer_count = 0;

            double halfCell = CellSize * 0.5;
			Vector3i idx = indexF.ToGrid(pt);
            Vector3d center = indexF.FromGrid(idx) + halfCell * Vector3d.One;

			if (r > CellSize)
				throw new ArgumentException("PointSetHashtable.FindInBall: large radius unsupported");

			double r2 = r * r;

			// check all in this cell
			PointList center_cell = Grid.Get(idx, false);
			if (center_cell != null) {
				foreach (int vid in center_cell) {
					if (pt.DistanceSquared(Points.GetVertex(vid)) < r2) {
						if (buffer_count == buffer.Length)
							return false;
						buffer[buffer_count++] = vid;
					}
				}
			}

			// if we are close enough to cell border we need to check nbrs
			// [TODO] could iterate over fewer cells here, if r is bounded by CellSize,
            // then we should only ever need to look at 3, depending on which octant we are in.
			if ( (pt-center).MaxAbs + r > halfCell ) {
				for (int ci = 0; ci < 26; ++ci) {
					Vector3i ioffset = gIndices.GridOffsets26[ci];

                    // if we are within r from face, we need to look into it
                    Vector3d ptToFaceCenter = new Vector3d(
                        center.x + halfCell * ioffset.x - pt.x,
                        center.y + halfCell * ioffset.y - pt.y,
                        center.z + halfCell * ioffset.z - pt.z);
                    if (ptToFaceCenter.MinAbs > r)
                        continue;

					PointList ncell = Grid.Get(idx + ioffset, false);
					if ( ncell != null ) {
						foreach (int vid in ncell) {
							if (pt.DistanceSquared(Points.GetVertex(vid)) < r2) {
								if (buffer_count == buffer.Length)
									return false;
								buffer[buffer_count++] = vid;
							}
						}						
					}
				}
			}

			return true;
		}






		public class PointList : List<int>, IGridElement3 {
			public IGridElement3 CreateNewGridElement(bool bCopy) {
				return new PointList();
			}
		}

	}
}
