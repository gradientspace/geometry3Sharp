using System;

namespace g3
{

    /// <summary>
    /// interface that maps between double real-space coords and integer grid goords
    /// </summary>
    public interface IGridWorldIndexer3
    {
        // map "world" coords to integer coords
        Vector3i ToGrid(Vector3d pointf);

        // map "world" coords to real-valued grid coords
        Vector3d ToGridf(Vector3d pointf);

        // map integer coords back to "world"
        Vector3d FromGrid(Vector3i gridpoint);

        // map real-valued coordinate *in integer coord space* back to "world"
        Vector3d FromGrid(Vector3d gridpointf);
    }




    public struct GridLevelIndex
    {
        public Vector3i block_index;
        public Vector3i local_index;
    }


    /// <summary>
    /// interface that maps between integer grid coords and 'blocks' of those
    /// coordinates, ie for multigrid-like structures
    /// </summary>
    public interface IMultigridIndexer3
    {
        /// <summary>
        /// maps from denser outer-grid indices to pairs of (block_index, local_index_in_block)
        /// this should just be the combined result of [ ToBlockIndex() , ToBlockLocal() ]
        /// </summary>
        GridLevelIndex ToBlock(Vector3i outer_index);

        /// <summary>
        /// map from outer-grid indices to block index (ie divide)
        /// </summary>
        Vector3i ToBlockIndex(Vector3i outer_index);

        /// <summary>
        /// map from outer-grid indices to block-local index (ie modulo)
        /// </summary>
        Vector3i ToBlockLocal(Vector3i outer_index);

        /// <summary>
        /// Map from block index to outer grid index at min-corner of the block.
        /// (add block-local coord to get specific outer-grid index)
        /// </summary>
        Vector3i FromBlock(Vector3i block_idx);
    }




	/// <summary>
	/// Map to/from grid coords
	/// </summary>
	public struct ScaleGridIndexer3 : IGridWorldIndexer3
	{
		public double CellSize;

		public ScaleGridIndexer3(double cellSize)
		{
			CellSize = cellSize;
		}

		public Vector3i ToGrid(Vector3d point) {
			return new Vector3i(
				(int)(point.x / CellSize),
				(int)(point.y / CellSize),
				(int)(point.z / CellSize));
		}

        public Vector3d ToGridf(Vector3d point) {
            return new Vector3d(
                (point.x / CellSize),
                (point.y / CellSize),
                (point.z / CellSize));
        }

        public Vector3d FromGrid(Vector3i gridpoint) {
			return new Vector3d(
				((double)gridpoint.x * CellSize),
				((double)gridpoint.y * CellSize),
				((double)gridpoint.z * CellSize));
		}

		public Vector3d FromGrid(Vector3d gridpointf) {
			return new Vector3d(
				(gridpointf.x * CellSize),
				(gridpointf.y * CellSize),
				(gridpointf.z * CellSize));
		}
	}



	/// <summary>
	/// Map to/from grid coords, where grid is translated from origin
	/// </summary>
	public struct ShiftGridIndexer3 : IGridWorldIndexer3
	{
		public Vector3d Origin;
		public double CellSize;

		public ShiftGridIndexer3(Vector3d origin, double cellSize) {
			Origin = origin;
			CellSize = cellSize;
		}

		public Vector3i ToGrid(Vector3d point) {
			return new Vector3i(
				(int)((point.x - Origin.x) / CellSize),
				(int)((point.y - Origin.y) / CellSize),
				(int)((point.z - Origin.z) / CellSize) );
		}

        public Vector3d ToGridf(Vector3d point) {
            return new Vector3d(
                ((point.x - Origin.x) / CellSize),
                ((point.y - Origin.y) / CellSize),
                ((point.z - Origin.z) / CellSize));
        }

        public Vector3d FromGrid(Vector3i gridpoint) {
			return new Vector3d(
				((double)gridpoint.x * CellSize) + Origin.x,
				((double)gridpoint.y * CellSize) + Origin.y,
				((double)gridpoint.z * CellSize) + Origin.z);
		}

		public Vector3d FromGrid(Vector3d gridpointf) {
			return new Vector3d(
				(gridpointf.x * CellSize) + Origin.x,
				(gridpointf.y * CellSize) + Origin.y,
				(gridpointf.z * CellSize) + Origin.z);
		}
	}




    /// <summary>
    /// Map to/from grid coords, where grid is relative to frame coords/axes
    /// </summary>
    public struct FrameGridIndexer3 : IGridWorldIndexer3
    {
        public Frame3f GridFrame;
        public Vector3f CellSize;

        public FrameGridIndexer3(Frame3f frame, Vector3f cellSize)
        {
            GridFrame = frame;
            CellSize = cellSize;
        }

        public Vector3i ToGrid(Vector3d point) {
            Vector3f pointf = (Vector3f)point;
            pointf = GridFrame.ToFrameP(ref pointf);
            return (Vector3i)(pointf / CellSize);
        }

        public Vector3d ToGridf(Vector3d point) {
            point = GridFrame.ToFrameP(ref point);
            point.x /= CellSize.x; point.y /= CellSize.y; point.z /= CellSize.z;
            return point;
        }

        public Vector3d FromGrid(Vector3i gridpoint)
        {
            Vector3f pointf = CellSize * (Vector3f)gridpoint;
            return (Vector3d)GridFrame.FromFrameP(ref pointf);
        }

        public Vector3d FromGrid(Vector3d gridpointf)
        {
            gridpointf *= CellSize;
            return (Vector3d)GridFrame.FromFrameP(ref gridpointf);
        }
    }




    /// <summary>
    /// map between "outer" (ie higher-res) grid coordinates and 
    /// "blocks" of those coordinates.
    /// </summary>
    public struct MultigridIndexer3 : IMultigridIndexer3
    {
        public Vector3i OuterShift;
        public Vector3i BlockSize;
        public Vector3i BlockShift;

        public MultigridIndexer3(Vector3i blockSize)
        {
            BlockSize = blockSize;
            OuterShift = BlockShift = Vector3i.Zero;
        }

        public Vector3i ToBlockIndex(Vector3i outer_index)
        {
            Vector3i block_index = outer_index - OuterShift;
            block_index.x = (block_index.x >= 0) ? (int)(block_index.x / BlockSize.x) : ((int)(block_index.x / BlockSize.x) - 1);
            block_index.y = (block_index.y >= 0) ? (int)(block_index.y / BlockSize.y) : ((int)(block_index.y / BlockSize.y) - 1);
            block_index.z = (block_index.z >= 0) ? (int)(block_index.z / BlockSize.z) : ((int)(block_index.z / BlockSize.z) - 1);
            return block_index - BlockShift;
        }

        public Vector3i ToBlockLocal(Vector3i outer_index)
        {
            Vector3i block_idx = ToBlockIndex(outer_index);
            return outer_index - block_idx * BlockSize;
        }

        public GridLevelIndex ToBlock(Vector3i outer_index)
        {
            Vector3i block_index = outer_index - OuterShift;
            block_index.x = (block_index.x >= 0) ? (int)(block_index.x / BlockSize.x) : ((int)(block_index.x / BlockSize.x) - 1);
            block_index.y = (block_index.y >= 0) ? (int)(block_index.y / BlockSize.y) : ((int)(block_index.y / BlockSize.y) - 1);
            block_index.z = (block_index.z >= 0) ? (int)(block_index.z / BlockSize.z) : ((int)(block_index.z / BlockSize.z) - 1);
            block_index -= BlockShift;
            return new GridLevelIndex() {
                block_index = block_index,
                local_index = outer_index - block_index * BlockSize
            };
        }


        public Vector3i FromBlock(Vector3i block_idx)
        {
            Vector3i outer_idx =  (block_idx + BlockShift) * BlockSize;
            return outer_idx + OuterShift;
        }


    }



}
