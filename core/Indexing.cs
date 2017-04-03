using System;
using System.Collections.Generic;
using System.Collections;


namespace g3
{

    // An enumerator that enumerates over integers [start, start+count)
    // (useful when you need to do things like iterate over indices of an array rather than values)
    public class IndexRangeEnumerator : IEnumerable<int>
    {
        int Start = 0;
        int Count = 0;
        public IndexRangeEnumerator(int count) { Count = count; }
        public IndexRangeEnumerator(int start, int count) { Start = start; Count = count; }
        public IEnumerator<int> GetEnumerator() {
            for (int i = 0; i < Count; ++i)
                yield return Start + i;
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }
    }




    // Add true/false operator[] to integer HashSet
    public class IndexHashSet : HashSet<int>
    {
        public bool this[int key]
        {
            get {
                return Contains(key);
            }
            set {
                if (value == true)
                    Add(key);
                else if (value == false && Contains(key))
                    Remove(key);
            }
        }
    }





    // This class provides a similar interface to BitArray, but can optionally
    // use a HashSet (or perhaps some other DS) if the fraction of the index space 
    // required is small
    public class IndexFlagSet
    {
        BitArray bits;
        HashSet<int> hash;


        public IndexFlagSet(bool bForceSparse, int MaxIndex = -1)
        {
            if (bForceSparse) {
                hash = new HashSet<int>();
            } else {
                bits = new BitArray(MaxIndex);
            }
        }

        public IndexFlagSet(int MaxIndex, int SubsetCountEst)
        {
            bool bSmall = MaxIndex < 128000;        // 16k in bits is a pretty small buffer?
            float fPercent = (float)SubsetCountEst / (float)MaxIndex;
            float fPercentThresh = 0.05f;           

            if (bSmall || fPercent > fPercentThresh) { 
                bits = new BitArray(MaxIndex);
            } else 
                hash = new HashSet<int>();
        }

        public bool this[int key]
        {
            get {
                return (bits != null) ? bits[key] : hash.Contains(key);
            }
            set {
                if (bits != null) {
                    bits[key] = value;
                } else {
                    if (value == true)
                        hash.Add(key);
                    else if (value == false && hash.Contains(key))
                        hash.Remove(key);
                }
            }
        }

    }



	// basic interface that allows mapping an index to another index
	public interface IIndexMap
	{
		int this[int index] { get; }
	}


	// i = i index map
	public class IdentityIndexMap : IIndexMap
	{
		public int this[int index] {
			get { return index; }
		}
	}


	// i = i + constant index map
	public class ShiftIndexMap : IIndexMap
	{
		public int Shift;

		public ShiftIndexMap(int n) {
			Shift = n;
		}

		public int this[int index] {
			get { return index + Shift; }
		}
	}


	// i = constant index map
	public class ConstantIndexMap : IIndexMap
	{
		public int Constant;

		public ConstantIndexMap(int c) {
			Constant = c;
		}

		public int this[int index] {
			get { return Constant; }
		}
	}



    // dense or sparse index map
	public class IndexMap : IIndexMap
    {
        // this is returned if sparse map doesn't contain value
        public readonly int InvalidIndex = int.MinValue;


        int[] dense_map;
        Dictionary<int, int> sparse_map;
        int MaxIndex;

        public IndexMap(bool bForceSparse, int MaxIndex = -1)
        {
            if (bForceSparse) {
                sparse_map = new Dictionary<int, int>();
            } else {
                dense_map = new int[MaxIndex];
            }
            this.MaxIndex = MaxIndex;
        }

        public IndexMap(int MaxIndex, int SubsetCountEst)
        {
            bool bSmall = MaxIndex < 32000;        // if buffer is less than 128k, just use dense map
            float fPercent = (float)SubsetCountEst / (float)MaxIndex;
            float fPercentThresh = 0.1f;

            if (bSmall || fPercent > fPercentThresh) {
                dense_map = new int[MaxIndex];
            } else {
                sparse_map = new Dictionary<int, int>();
            }
            this.MaxIndex = MaxIndex;
        }


        // no effect on sparse map
        public void SetToInvalid()
        {
            if ( dense_map != null ) {
                for (int i = 0; i < dense_map.Length; ++i)
                    dense_map.SetValue(i, InvalidIndex);
            }
        }


        // dense variant: returns true unless you have set index to InvalidIndex (eg via SetToInvalid)
        // sparse variant: returns true if index is in map
        // either: returns false if index is out-of-bounds
        public bool Contains(int index)
        {
            if (MaxIndex > 0 && index >= MaxIndex)
                return false;
            if (dense_map != null)
                return dense_map[index] != InvalidIndex;
            else
                return sparse_map.ContainsKey(index);
        }



        public int this[int index]
        {
            get {
                if (dense_map != null)
                    return dense_map[index];
                else {
                    int to;
                    if (sparse_map.TryGetValue(index, out to))
                        return to;
                    return InvalidIndex;
                }
            }
            set {
                if (dense_map != null) {
                    dense_map[index] = value;
                } else {
                    sparse_map[index] = value;
                }
            }
        }

    }



}
