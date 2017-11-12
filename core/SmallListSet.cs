using System;
using System.Collections.Generic;

namespace g3
{
    public class SmallListSet
    {
        const int Null = -1;
        const int LinkedIDType = 1;

        DVector<int> list_heads;

        DVector<int> linked_store;
        int free_head;


        public SmallListSet()
        {
            list_heads = new DVector<int>();
            linked_store = new DVector<int>();
            free_head = Null;
        }


        public SmallListSet(SmallListSet copy)
        {
            linked_store = new DVector<int>(copy.linked_store);
            free_head = copy.free_head;
            list_heads = new DVector<int>(copy.list_heads);
        }


        public void AllocateAt(int idx)
        {
            if ( idx >= list_heads.size ) {
                list_heads.insert(Null, idx);
            } else {
                if (list_heads[idx] != Null)
                    throw new Exception("SmallListSet: list at " + idx + " is not empty!");
            }
        }

        public int Size {
            get { return list_heads.size; }
        }

        public void Resize(int new_size)
        {
            int cur_size = list_heads.size;
            if (new_size > cur_size) {
                list_heads.resize(new_size);
                for (int k = cur_size; k < new_size; ++k)
                    list_heads[k] = Null;
            }
        }


        public void Prepend(int list_index, int val)
        {
            if ( free_head == Null ) {
                int new_ptr = linked_store.size;
                linked_store.Add(val);
                linked_store.Add(list_heads[list_index]);
                list_heads[list_index] = new_ptr;
            } else { 
                int free_ptr = free_head;
                free_head = linked_store[free_ptr+1];

                linked_store[free_ptr] = val;
                linked_store[free_ptr+1] = list_heads[list_index];
                list_heads[list_index] = free_ptr;
            }
        }


        public bool Remove(int list_index, int val)
        {
            int cur_ptr = list_heads[list_index];
            int prev_ptr = Null;
            while ( cur_ptr != Null ) {
                if ( linked_store[cur_ptr] == val ) {
                    int next_ptr = linked_store[cur_ptr + 1];

                    if ( prev_ptr == Null ) {
                        list_heads[list_index] = next_ptr;
                    } else {
                        linked_store[prev_ptr + 1] = next_ptr;
                    }
                    add_free_link(cur_ptr);
                    return true;
                }
                prev_ptr = cur_ptr;
                cur_ptr = linked_store[cur_ptr + 1];
            }

            return false;
        }



        public void Clear(int list_index)
        {
            int cur_ptr = list_heads[list_index];
            while (cur_ptr != Null) {
                int free_ptr = cur_ptr; 
                cur_ptr = linked_store[cur_ptr + 1];
                add_free_link(free_ptr);
            }
            list_heads[list_index] = Null;
        }


        public int Count(int list_index)
        {
            int cur_ptr = list_heads[list_index];
            int n = 0;
            while (cur_ptr != Null) {
                n++;
                cur_ptr = linked_store[cur_ptr + 1];
            }
            return n;
        }


        public bool Contains(int list_index, int val)
        {
            int cur_ptr = list_heads[list_index];
            while (cur_ptr != Null) {
                if (linked_store[cur_ptr] == val)
                    return true;
                cur_ptr = linked_store[cur_ptr + 1];
            }
            return false;
        }


        public int First(int list_index)
        {
            return linked_store[list_heads[list_index]];
        }


        public IEnumerable<int> ValueItr(int list_index)
        {
            int cur_ptr = list_heads[list_index];
            while (cur_ptr != Null) {
                yield return linked_store[cur_ptr];
                cur_ptr = linked_store[cur_ptr + 1];
            }
        }


        void add_free_link(int ptr)
        {
            linked_store[ptr + 1] = free_head;
            free_head = ptr;
        }



    }
}
