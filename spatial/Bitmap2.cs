using System;
using System.Collections;
using System.Collections.Generic;

namespace g3
{

    public class Bitmap2
    {
        public BitArray Bits;

        Vector2i dimensions;
        public Vector2i Dimensions {
            get { return dimensions; }
        }

        int row_size;

        public Bitmap2(Vector2i dims) {
			Resize(dims);
        }
		public Bitmap2(int Width, int Height)
		{
			Resize(new Vector2i(Width, Height));
		}

		public void Resize(Vector2i dims) {
			int size = dims.x * dims.y;
			Bits = new BitArray(size);
			dimensions = dims;
			row_size = dims.x;			
		}


        public AxisAlignedBox2i GridBounds {
            get { return new AxisAlignedBox2i(Vector2i.Zero, Dimensions); }
        }

        public bool this[int i] {
            get { return Bits[i]; }
            set { Bits[i] = value; }
        }

		public bool this[int r, int c] {
			get { return Bits[r * row_size + c]; }
			set { Bits[r * row_size + c] = value; }
		}

        public bool this[Vector2i idx]
        {
            get {
                int i = idx.y * row_size + idx.x;
                return Bits[i];
            }
            set {
                int i = idx.y * row_size + idx.x;
                Bits[i] = value;
            }
        }

        public void Set(Vector2i idx, bool val)
        {
            int i = idx.y * row_size + idx.x;
            Bits[i] = val;
        }

        public bool Get(Vector2i idx)
        {
            int i = idx.y * row_size + idx.x;
            return Bits[i];
        }


        public Vector2i ToIndex(int i) {
            int b = i / row_size;
            i -= b * row_size;
            return new Vector2i(i, b);
        }
        public int ToLinear(Vector2i idx) {
            return idx.y * row_size + idx.x;
        }



        public IEnumerable<Vector2i> Indices()
        {
            for ( int y = 0; y < Dimensions.y; ++y ) {
                for (int x = 0; x < Dimensions.x; ++x)
                    yield return new Vector2i(x, y);
            }
        }


        public IEnumerable<Vector2i> NonZeros()
        {
            for ( int i = 0; i < Bits.Count; ++i ) {
                if (Bits[i])
                    yield return ToIndex(i);
            }
        }






    }
}
