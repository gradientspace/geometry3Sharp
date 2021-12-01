using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace g3
{
    public class Bitmap3 : IBinaryVoxelGrid, IGridElement3, IFixedGrid3
    {
        private readonly Vector3i _dimensions;
        private SpinLock _bitLock = new SpinLock();
        private readonly int _rowSize;
        private readonly int _slabSize;

        public BitArray Bits;
        public Vector3i Dimensions => _dimensions;

        public Bitmap3(Vector3i dims)
        {
            int size = dims.x * dims.y * dims.z;
            Bits = new BitArray(size);

            _dimensions = dims;
            _rowSize = dims.x;
            _slabSize = dims.x * dims.y;
        }

        public Bitmap3(Bitmap3 bitmapToCopy)
        {
            Bits = (BitArray) bitmapToCopy.Bits.Clone();
            _dimensions = bitmapToCopy.Dimensions;
            _rowSize = bitmapToCopy._rowSize;
            _slabSize = bitmapToCopy._slabSize;
        }

        public AxisAlignedBox3i GridBounds => new AxisAlignedBox3i(Vector3i.Zero, Dimensions);

        public bool this[int i]
        {
            get => Bits[i];
            set => Bits[i] = value;
        }

        public bool this[Vector3i idx]
        {
            get
            {
                int i = idx.z * _slabSize + idx.y * _rowSize + idx.x;
                return Bits[i];
            }
            set
            {
                int i = idx.z * _slabSize + idx.y * _rowSize + idx.x;
                Bits[i] = value;
            }
        }

        public void Set(Vector3i idx, bool val)
        {
            int i = idx.z * _slabSize + idx.y * _rowSize + idx.x;
            Bits[i] = val;
        }

        public void SafeSet(Vector3i idx, bool val)
        {
            bool taken = false;
            _bitLock.Enter(ref taken);
            int i = idx.z * _slabSize + idx.y * _rowSize + idx.x;
            Bits[i] = val;
            _bitLock.Exit();
        }

        public bool Get(in Vector3i index)
        {
            int i = index.z * _slabSize + index.y * _rowSize + index.x;
            return Bits[i];
        }

        public Vector3i ToIndex(int i)
        {
            int c = i / _slabSize;
            i -= c * _slabSize;
            int b = i / _rowSize;
            i -= b * _rowSize;
            return new Vector3i(i, b, c);
        }

        public int ToLinear(Vector3i idx)
        {
            return idx.z * _slabSize + idx.y * _rowSize + idx.x;
        }

        public IEnumerable<Vector3i> Indices()
        {
            for (int z = 0; z < Dimensions.z; ++z)
            {
                for (int y = 0; y < Dimensions.y; ++y)
                {
                    for (int x = 0; x < Dimensions.x; ++x)
                        yield return new Vector3i(x, y, z);
                }
            }
        }

        public IEnumerable<Vector3i> NonZeros()
        {
            for (int i = 0; i < Bits.Count; ++i)
            {
                if (Bits[i])
                    yield return ToIndex(i);
            }
        }

        /// <summary>
        /// count 6-neighbours of each voxel, discard if count <= minNbrs
        /// </summary>
        public void Filter(int nMinNeighbours)
        {
            AxisAlignedBox3i bounds = GridBounds;
            bounds.Max -= Vector3i.One;

            for (int i = 0; i < Bits.Length; ++i)
            {
                if (Bits[i] == false)
                    continue;
                Vector3i idx = ToIndex(i);
                int nc = 0;
                for (int k = 0; k < 6 && nc <= nMinNeighbours; ++k)
                {
                    Vector3i nbr = idx + gIndices.GridOffsets6[k];
                    if (bounds.Contains(nbr) == false)
                        continue;
                    if (Get(nbr))
                        nc++;
                }

                if (nc <= nMinNeighbours)
                    Bits[i] = false;
            }
        }

        // IGridElement interface, creates a new object and potentially copies this one
        public virtual IGridElement3 CreateNewGridElement(bool bCopy)
        {
            Bitmap3 copy = new Bitmap3(Dimensions);
            if (bCopy)
                throw new NotImplementedException();
            return copy;
        }
    }
}