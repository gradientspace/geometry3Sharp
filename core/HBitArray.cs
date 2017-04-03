using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;


namespace g3
{
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


        public HBitArray(int maxIndex)
        {
            int base_count = (maxIndex / 32);
            if (maxIndex % 32 != 0)
                base_count++;
            bits = new MyBitVector32[base_count];

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


        public void Set(int i, bool value)
        {
            int byte_i = i / 32;
            int byte_o = i - (32 * byte_i);

            Debug.Assert(byte_o < 32);

            if (value == true) {
                bits[byte_i][byte_o] = true;

                // [TODO] only need to propagate up if our current field was zero
                for (int li = 0; li < layerCount; ++li) {
                    int layer_i = byte_i / 32;
                    int layer_o = byte_i - (32 * layer_i);
                    layers[li].layer_bits[layer_i][layer_o] = true;
                    byte_i = layer_i;
                }

            } else {
                bits[byte_i][byte_o] = false;

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


        public bool Get(int i)
        {
            int byte_i = i / 32;
            int byte_o = i - (32 * byte_i);
            return bits[byte_i][byte_o];
        }
 


        public IEnumerator<int> GetEnumerator()
        {
            for ( int ai = 0; ai < layers[1].layer_bits.Length; ++ai ) {
                if (layers[1].layer_bits[ai].Data == 0)
                    continue;
                for (int aj = 0; aj < 32; aj++ ) {
                    if ( layers[1].layer_bits[ai][aj] ) {

                        int bi = ai * 32 + aj;
                        Debug.Assert(layers[0].layer_bits[bi].Data != 0);
                        for ( int bj = 0; bj < 32; bj++ ) {
                            if ( layers[0].layer_bits[bi][bj] ) {
                                int i = bi * 32 + bj;

                                int d = bits[i].Data;
                                int dmask = 1;
                                for ( int j = 0; j < 32; ++j ) {
                                    if ( (d & dmask) != 0 )
                                        yield return i * 32 + j;
                                    dmask <<= 1;
                                }

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



        IEnumerator IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }


    }
}
