using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;


namespace g3
{
    /// <summary>
    /// HBitArray is a hierarchical variant of BitArray. Basically the idea
    /// is to make a tree of 32-bit blocks, where at level N, a '0' means that
    /// no bits are true in level N-1. This means we can more efficiently iterate
    /// over the bit set. 
    /// 
    /// Uses more memory than BitArray, but each tree level is divided by 32, so
    /// it is better than NlogN
    /// </summary>
    public class HBitArray : IEnumerable<int>
    {
        struct MyBitVector32
        {
            int bits;
            public bool this[int i]
            {
                get { return (bits & (1 << i)) != 0; }
                set {
                        if (value)
                            bits |= (1 << i);
                        else
                            bits &= ~(1 << i);
                    }
            }
            public int Data { get { return bits; } }
        }


        MyBitVector32[] bits;

        struct Layer
        {
            public MyBitVector32[] layer_bits;
        }
        Layer[] layers;
        int layerCount;
        int max_index;
        int count;

        public HBitArray(int maxIndex)
        {
            max_index = maxIndex;
            int base_count = (maxIndex / 32);
            if (maxIndex % 32 != 0)
                base_count++;
            bits = new MyBitVector32[base_count];
            count = 0;

            layerCount = 2;
            layers = new Layer[layerCount];

            int prev_size = bits.Length;
            for ( int i = 0; i < layerCount; ++i ) {
                int cur_size = (prev_size / 32);
                if (prev_size % 32 != 0)
                    cur_size++;
                layers[i].layer_bits = new MyBitVector32[cur_size];
                prev_size = cur_size;
            }
        }


        public bool this[int i]
        {
            get { return Get(i); }
            set { Set(i, value); }
        }


        public int Count
        {
            get { return max_index; }
        }

        public int TrueCount
        {
            get { return count; }
        }


        public bool Contains(int i)
        {
            return Get(i) == true;
        }

        public void Add(int i)
        {
            Set(i, true);
        }

        public void Set(int i, bool value)
        {
            int byte_i = i / 32;
            int byte_o = i - (32 * byte_i);

            Debug.Assert(byte_o < 32);

            if (value == true) {
                if (bits[byte_i][byte_o] == false) {
                    bits[byte_i][byte_o] = true;
                    count++;

                    // [TODO] only need to propagate up if our current field was zero
                    for (int li = 0; li < layerCount; ++li) {
                        int layer_i = byte_i / 32;
                        int layer_o = byte_i - (32 * layer_i);
                        layers[li].layer_bits[layer_i][layer_o] = true;
                        byte_i = layer_i;
                    }
                }

            } else {
                if (bits[byte_i][byte_o] == true) {
                    bits[byte_i][byte_o] = false;
                    count--;

                    // [RMS] [June 6 2017] not sure if this comment is still true or not. Need to experiment.
                    // [TODO] only need to propagate up if our current field becomes zero
                    //ACK NO THIS IS WRONG! only clear parent bit if our entire bit is zero!

                    for (int li = 0; li < layerCount; ++li) {
                        int layer_i = byte_i / 32;
                        int layer_o = byte_i - (32 * layer_i);
                        layers[li].layer_bits[layer_i][layer_o] = false;
                        byte_i = layer_i;
                    }
                }
            }
        }


        public bool Get(int i)
        {
            int byte_i = i / 32;
            int byte_o = i - (32 * byte_i);
            return bits[byte_i][byte_o];
        }
 


        public IEnumerator<int> GetEnumerator()
        {
            if (count > max_index / 3) {
                for ( int bi = 0; bi < bits.Length; ++bi ) {
                    int d = bits[bi].Data;
                    int dmask = 1;
                    int maxj = (bi == bits.Length - 1) ? max_index % 32 : 32;
                    for (int j = 0; j < maxj; ++j) {
                        if ((d & dmask) != 0)
                            yield return bi * 32 + j;
                        dmask <<= 1;
                    }
                }

            } else {
                for (int ai = 0; ai < layers[1].layer_bits.Length; ++ai) {
                    if (layers[1].layer_bits[ai].Data == 0)
                        continue;
                    for (int aj = 0; aj < 32; aj++) {
                        if (layers[1].layer_bits[ai][aj]) {

                            int bi = ai * 32 + aj;
                            Debug.Assert(layers[0].layer_bits[bi].Data != 0);
                            for (int bj = 0; bj < 32; bj++) {
                                if (layers[0].layer_bits[bi][bj]) {
                                    int i = bi * 32 + bj;

                                    int d = bits[i].Data;
                                    int dmask = 1;
                                    for (int j = 0; j < 32; ++j) {
                                        if ((d & dmask) != 0)
                                            yield return i * 32 + j;
                                        dmask <<= 1;
                                    }

                                    // this is more expensive, but good for testing...
                                    //for ( int j = 0; j < 32; ++j ) {
                                    //    if (bits[i][j] == true)
                                    //        yield return i * 32 + j;
                                    //}
                                }
                            }
                        }
                    }
                }
            }
        }



        IEnumerator IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }


    }
}
