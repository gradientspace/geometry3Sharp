using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// BiGrid3 is a two-level multiresolution grid data structure. You provide
    /// exemplar object that implements suitable interfaces, and the class
    /// automatically generates necessary data structures. 
    /// Functions to act on parent/child grids are in-progress...
    /// </summary>
    public class BiGrid3<BlockType> where BlockType : class, IGridElement3, IFixedGrid3
    {
        Vector3i block_size;
        MultigridIndexer3 indexer;

        public Vector3i BlockSize {
            get { return block_size; }
        }
        public MultigridIndexer3 Indexer {
            get { return indexer; }
        }


        DSparseGrid3<BlockType> sparse_grid;
        public DSparseGrid3<BlockType> BlockGrid {
            get { return sparse_grid; }
        }


        public BiGrid3( BlockType exemplar )
        {
            block_size = exemplar.Dimensions;
            indexer = new MultigridIndexer3(block_size);
            sparse_grid = new DSparseGrid3<BlockType>(exemplar);
        }


        /// <summary>
        /// map index into correct block and let client update that block at the correct local index
        /// </summary>
        public void Update(Index3i index, Action<BlockType,Vector3i> UpdateF )
        {
            GridLevelIndex bidx = Indexer.ToBlock(index);
            BlockType block = sparse_grid.Get(bidx.block_index, true);
            UpdateF(block, bidx.local_index);
        }



        public IEnumerable<KeyValuePair<Vector3i,BlockType>> AllocatedBlocks()
        {
            return sparse_grid.Allocated();
        }

    }
}
