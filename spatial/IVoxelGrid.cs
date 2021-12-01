using System.Collections.Generic;

namespace g3
{
    /// <summary>
    /// Voxel grid of T elements
    /// </summary>
    public interface IVoxelGrid<out T>
    {
        /// <summary>
        /// Bounds are lower-inclusive, upper-exclusive
        /// </summary>
        AxisAlignedBox3i GridBounds { get; }

        /// <summary>
        /// Get a T-element by index in the voxel grid
        /// </summary>
        T Get(in Vector3i index);
    }

    public interface IBinaryVoxelGrid : IVoxelGrid<bool>
    {
        IEnumerable<Vector3i> NonZeros();
    }
}