using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
namespace g3
{

    public interface IBinaryVoxelGrid
    {
        AxisAlignedBox3i GridBounds { get; }   // bounds are lower-inclusive, upper-exclusive
        bool Get(Vector3i i);
        IEnumerable<Vector3i> NonZeros();
    }



    public class Bitmap3 : IBinaryVoxelGrid, IGridElement3, IFixedGrid3
    {
        public BitArray Bits;

        Vector3i dimensions;
        public Vector3i Dimensions {
            get { return dimensions; }
        }

        int row_size, slab_size;

        SpinLock bit_lock = new SpinLock();

        public Bitmap3(Vector3i dims)
        {
            int size = dims.x * dims.y * dims.z;
            Bits = new BitArray(size);

            dimensions = dims;
            row_size = dims.x;
            slab_size = dims.x * dims.y;
        }


        public AxisAlignedBox3i GridBounds {
            get { return new AxisAlignedBox3i(Vector3i.Zero, Dimensions); }
        }

        public bool this[int i]
        {
            get { return Bits[i]; }
            set { Bits[i] = value; }
        }


        public bool this[Vector3i idx]
        {
            get {
                int i = idx.z * slab_size + idx.y * row_size + idx.x;
                return Bits[i];
            }
            set {
                int i = idx.z * slab_size + idx.y * row_size + idx.x;
                Bits[i] = value;
            }
        }

        public void Set(Vector3i idx, bool val)
        {
            int i = idx.z * slab_size + idx.y * row_size + idx.x;
            Bits[i] = val;
        }

        public void SafeSet(Vector3i idx, bool val)
        {
            bool taken = false;
            bit_lock.Enter(ref taken);
            int i = idx.z * slab_size + idx.y * row_size + idx.x;
            Bits[i] = val;
            bit_lock.Exit();
        }

        public bool Get(Vector3i idx)
        {
            int i = idx.z * slab_size + idx.y * row_size + idx.x;
            return Bits[i];
        }


        public Vector3i ToIndex(int i) {
            int c = i / slab_size;
            i -= c * slab_size;
            int b = i / row_size;
            i -= b * row_size;
            return new Vector3i(i, b, c);
        }
        public int ToLinear(Vector3i idx) {
            return idx.z * slab_size + idx.y * row_size + idx.x;
        }



        public IEnumerable<Vector3i> Indices()
        {
            for ( int z = 0; z < Dimensions.z; ++z) {
                for ( int y = 0; y < Dimensions.y; ++y ) {
                    for (int x = 0; x < Dimensions.x; ++x)
                        yield return new Vector3i(x, y, z);
                }
            }
        }


        public IEnumerable<Vector3i> NonZeros()
        {
            for ( int i = 0; i < Bits.Count; ++i ) {
                if (Bits[i])
                    yield return ToIndex(i);
            }
        }






		/// <summary>
		/// count 6-nbrs of each voxel, discard if count <= minNbrs
		/// </summary>
		public void Filter(int nMinNbrs) {
			AxisAlignedBox3i bounds = GridBounds;
			bounds.Max -= Vector3i.One;

			for (int i = 0; i < Bits.Length; ++i ) {
				if (Bits[i] == false)
					continue;
				Vector3i idx = ToIndex(i);
				int nc = 0;
				for (int k = 0; k < 6 && nc <= nMinNbrs; ++k) {
					Vector3i nbr = idx + gIndices.GridOffsets6[k];
					if (bounds.Contains(nbr) == false)
						continue;
					if (Get(nbr))
						nc++;
				}
				if (nc <= nMinNbrs)
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
