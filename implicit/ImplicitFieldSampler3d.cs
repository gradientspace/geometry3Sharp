using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// Sample implicit fields into a dense grid
    /// </summary>
    public class ImplicitFieldSampler3d
    {
        public DenseGrid3f Grid;
        public double CellSize;
        public Vector3d GridOrigin;
        public ShiftGridIndexer3 Indexer;
        public AxisAlignedBox3i GridBounds;

        public float BackgroundValue;


        public enum CombineModes
        {
            DistanceMinUnion = 0
        }
        public CombineModes CombineMode = CombineModes.DistanceMinUnion;


        public ImplicitFieldSampler3d(AxisAlignedBox3d fieldBounds, double cellSize)
        {
            CellSize = cellSize;
            GridOrigin = fieldBounds.Min;
            Indexer = new ShiftGridIndexer3(GridOrigin, CellSize);

            Vector3d max = fieldBounds.Max; max += cellSize;
            int ni = (int)((max.x - GridOrigin.x) / CellSize) + 1;
            int nj = (int)((max.y - GridOrigin.y) / CellSize) + 1;
            int nk = (int)((max.z - GridOrigin.z) / CellSize) + 1;

            GridBounds = new AxisAlignedBox3i(0, 0, 0, ni, nj, nk);

            BackgroundValue = (float)((ni + nj + nk) * CellSize);
            Grid = new DenseGrid3f(ni, nj, nk, BackgroundValue);
        }



        public DenseGridTrilinearImplicit ToImplicit() {
            return new DenseGridTrilinearImplicit(Grid, GridOrigin, CellSize);
        }



        public void Clear(float f)
        {
            BackgroundValue = f;
            Grid.assign(BackgroundValue);
        }



        public void Sample(BoundedImplicitFunction3d f, double expandRadius = 0)
        {
            AxisAlignedBox3d bounds = f.Bounds();

            Vector3d expand = expandRadius * Vector3d.One;
            Vector3i gridMin = Indexer.ToGrid(bounds.Min-expand), 
                     gridMax = Indexer.ToGrid(bounds.Max+expand) + Vector3i.One;
            gridMin = GridBounds.ClampExclusive(gridMin);
            gridMax = GridBounds.ClampExclusive(gridMax);

            AxisAlignedBox3i gridbox = new AxisAlignedBox3i(gridMin, gridMax);
            switch (CombineMode) {
                case CombineModes.DistanceMinUnion:
                    sample_min(f, gridbox.IndicesInclusive());
                    break;
            }
        }


        void sample_min(BoundedImplicitFunction3d f, IEnumerable<Vector3i> indices)
        {
            gParallel.ForEach(indices, (idx) => {
                Vector3d v = Indexer.FromGrid(idx);
                double d = f.Value(ref v);
                Grid.set_min(ref idx, (float)d);
            });
        }

    }
}
