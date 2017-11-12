using System;
using System.Collections.Generic;

namespace g3
{
    public class SmallListSet
    {
        public struct List
        {
            public int id;
            public int ptr;
        }

        const int Null = -1;
        const int LinkedIDType = 1;


        DVector<int> linked_store;
        int free_head;


        public SmallListSet()
        {
            linked_store = new DVector<int>();
            free_head = Null;
        }


        public SmallListSet(SmallListSet copy)
        {
            linked_store = new DVector<int>(copy.linked_store);
            free_head = copy.free_head;
        }


        public void AllocateList(out List list)
        {
            list = new List();
            list.id = LinkedIDType;
            list.ptr = Null;
        }

        public void Prepend(ref List list, int val)
        {
            if ( free_head == Null ) {
                int new_ptr = linked_store.size;
                linked_store.Add(val);
                linked_store.Add(list.ptr);
                list.ptr = new_ptr;
            } else { 
                int free_ptr = free_head;
                free_head = linked_store[free_ptr+1];

                linked_store[free_ptr] = val;
                linked_store[free_ptr+1] = list.ptr;
                list.ptr = free_ptr;
            }
        }


        public bool Remove(ref List list, int val)
        {
            int cur_ptr = list.ptr;
            int prev_ptr = Null;
            while ( cur_ptr != Null ) {
                if ( linked_store[cur_ptr] == val ) {
                    int next_ptr = linked_store[cur_ptr + 1];

                    if ( prev_ptr == Null ) {
                        list.ptr = next_ptr;
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



        public void Clear(ref List list)
        {
            int cur_ptr = list.ptr;
            while (cur_ptr != Null) {
                int free_ptr = cur_ptr; 
                cur_ptr = linked_store[cur_ptr + 1];
                add_free_link(free_ptr);
            }
            list.ptr = Null;
        }


        public int Count(ref List list)
        {
            int cur_ptr = list.ptr;
            int n = 0;
            while (cur_ptr != Null) {
                n++;
                cur_ptr = linked_store[cur_ptr + 1];
            }
            return n;
        }


        public bool Contains(ref List list, int val)
        {
            int cur_ptr = list.ptr;
            while (cur_ptr != Null) {
                if (linked_store[cur_ptr] == val)
                    return true;
                cur_ptr = linked_store[cur_ptr + 1];
            }
            return false;
        }


        public int First(List list)
        {
            return linked_store[list.ptr];
        }
        public int First(ref List list)
        {
            return linked_store[list.ptr];
        }


        public IEnumerable<int> ValueItr(List list)
        {
            int cur_ptr = list.ptr;
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
