using System;

namespace g3
{

    /// <summary>
    /// interface that maps between double real-space coords and integer grid goords
    /// </summary>
    public interface IGridWorldIndexer2
    {
        // map "world" coords to integer coords
        Vector2i ToGrid(Vector2d pointf);

        // map integer coords back to "world"
        Vector2d FromGrid(Vector2i gridpoint);

        // map real-valued coordinate *in integer coord space* back to "world"
        Vector2d FromGrid(Vector2d gridpointf);
    }




    public struct GridLevelIndex2
    {
        public Vector2i block_index;
        public Vector2i local_index;
    }


    /// <summary>
    /// interface that maps between integer grid coords and 'blocks' of those
    /// coordinates, ie for multigrid-like structures
    /// </summary>
    public interface IMultigridIndexer2
    {
        /// <summary>
        /// maps from denser outer-grid indices to pairs of (block_index, local_index_in_block)
        /// this should just be the combined result of [ ToBlockIndex() , ToBlockLocal() ]
        /// </summary>
        GridLevelIndex2 ToBlock(Vector2i outer_index);

        /// <summary>
        /// map from outer-grid indices to block index (ie divide)
        /// </summary>
        Vector2i ToBlockIndex(Vector2i outer_index);

        /// <summary>
        /// map from outer-grid indices to block-local index (ie modulo)
        /// </summary>
        Vector2i ToBlockLocal(Vector2i outer_index);

        /// <summary>
        /// Map from block index to outer grid index at min-corner of the block.
        /// (add block-local coord to get specific outer-grid index)
        /// </summary>
        Vector2i FromBlock(Vector2i block_idx);
    }




	/// <summary>
	/// Map to/from grid coords
	/// </summary>
	public struct ScaleGridIndexer2 : IGridWorldIndexer2
	{
		public double CellSize;

		public ScaleGridIndexer2(double cellSize)
		{
			CellSize = cellSize;
		}

		public Vector2i ToGrid(Vector2d point) {
			return new Vector2i(
				(int)(point.x / CellSize),
				(int)(point.y / CellSize));
		}

		public Vector2d FromGrid(Vector2i gridpoint) {
			return new Vector2d(
				((double)gridpoint.x * CellSize),
				((double)gridpoint.y * CellSize));
		}

		public Vector2d FromGrid(Vector2d gridpointf) {
			return new Vector2d(
				((double)gridpointf.x * CellSize),
				((double)gridpointf.y * CellSize));
		}
	}



	/// <summary>
	/// Map to/from grid coords, where grid is translated from origin
	/// </summary>
	public struct ShiftGridIndexer2 : IGridWorldIndexer2
	{
		public Vector2d Origin;
		public double CellSize;

		public ShiftGridIndexer2(Vector2d origin, double cellSize) {
			Origin = origin;
			CellSize = cellSize;
		}

		public Vector2i ToGrid(Vector2d point) {
			return new Vector2i(
				(int)((point.x - Origin.x) / CellSize),
				(int)((point.y - Origin.y) / CellSize) );
		}

		public Vector2d FromGrid(Vector2i gridpoint) {
			return new Vector2d(
				((double)gridpoint.x * CellSize) + Origin.x,
				((double)gridpoint.y * CellSize) + Origin.y);
		}

		public Vector2d FromGrid(Vector2d gridpointf) {
			return new Vector2d(
				((double)gridpointf.x * CellSize) + Origin.x,
				((double)gridpointf.y * CellSize) + Origin.y);
		}
	}



    /// <summary>
    /// map between "outer" (ie higher-res) grid coordinates and 
    /// "blocks" of those coordinates.
    /// </summary>
    public struct MultigridIndexer2 : IMultigridIndexer2
    {
        public Vector2i OuterShift;
        public Vector2i BlockSize;
        public Vector2i BlockShift;

        public MultigridIndexer2(Vector2i blockSize)
        {
            BlockSize = blockSize;
            OuterShift = BlockShift = Vector2i.Zero;
        }

        public Vector2i ToBlockIndex(Vector2i outer_index)
        {
            Vector2i block_index = outer_index - OuterShift;
            block_index.x = (block_index.x >= 0) ? (int)(block_index.x / BlockSize.x) : ((int)(block_index.x / BlockSize.x) - 1);
            block_index.y = (block_index.y >= 0) ? (int)(block_index.y / BlockSize.y) : ((int)(block_index.y / BlockSize.y) - 1);
            return block_index - BlockShift;
        }

        public Vector2i ToBlockLocal(Vector2i outer_index)
        {
            Vector2i block_idx = ToBlockIndex(outer_index);
            return outer_index - block_idx * BlockSize;
        }

        public GridLevelIndex2 ToBlock(Vector2i outer_index)
        {
            Vector2i block_index = outer_index - OuterShift;
            block_index.x = (block_index.x >= 0) ? (int)(block_index.x / BlockSize.x) : ((int)(block_index.x / BlockSize.x) - 1);
            block_index.y = (block_index.y >= 0) ? (int)(block_index.y / BlockSize.y) : ((int)(block_index.y / BlockSize.y) - 1);
            block_index -= BlockShift;
            return new GridLevelIndex2() {
                block_index = block_index,
                local_index = outer_index - block_index * BlockSize
            };
        }


        public Vector2i FromBlock(Vector2i block_idx)
        {
            Vector2i outer_idx =  (block_idx + BlockShift) * BlockSize;
            return outer_idx + OuterShift;
        }


    }



}
