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
            pointf = GridFrame.ToFrameP(pointf);
            return (Vector3i)(pointf / CellSize);
        }

        public Vector3d FromGrid(Vector3i gridpoint)
        {
            Vector3f pointf = CellSize * (Vector3f)gridpoint;
            return (Vector3d)GridFrame.FromFrameP(pointf);
        }

        public Vector3d FromGrid(Vector3d gridpointf)
        {
            gridpointf *= CellSize;
            return (Vector3d)GridFrame.FromFrameP(gridpointf);
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
