using System;
using System.Collections.Generic;

namespace g3
{
    public class SmallListSet
    {
        const int Null = -1;
        const int LinkedIDType = 1;

        DVector<int> list_heads;

        const int BLOCKSIZE = 8;
        const int BLOCK_LIST_OFFSET = BLOCKSIZE + 1;

        DVector<int> block_store;
        DVector<int> free_blocks;
        int allocated_count = 0;

        DVector<int> linked_store;
        int free_head;

        public SmallListSet()
        {
            list_heads = new DVector<int>();
            linked_store = new DVector<int>();
            free_head = Null;
            block_store = new DVector<int>();
            free_blocks = new DVector<int>();
        }


        public SmallListSet(SmallListSet copy)
        {
            linked_store = new DVector<int>(copy.linked_store);
            free_head = copy.free_head;
            list_heads = new DVector<int>(copy.list_heads);
            block_store = new DVector<int>(copy.block_store);
            free_blocks = new DVector<int>(copy.free_blocks);
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


        public void AllocateAt(int idx)
        {
            if (idx >= list_heads.size) {
                list_heads.insert(Null, idx);
            } else {
                if (list_heads[idx] != Null)
                    throw new Exception("SmallListSet: list at " + idx + " is not empty!");
            }
        }



        public void Insert(int list_index, int val)
        {
            int block_ptr = list_heads[list_index];
            if ( block_ptr == Null ) {
                block_ptr = allocate_block();
                block_store[block_ptr] = 0;
                list_heads[list_index] = block_ptr;
            }

            int N = block_store[block_ptr];
            if (N < BLOCKSIZE) {
                block_store[block_ptr + N + 1] = val;
            } else {
                // spill to linked list
                int cur_head = block_store[block_ptr + BLOCK_LIST_OFFSET];

                if (free_head == Null) {
                    // allocate new linkedlist node
                    int new_ptr = linked_store.size;
                    linked_store.Add(val);
                    linked_store.Add(cur_head);
                    block_store[block_ptr + BLOCK_LIST_OFFSET] = new_ptr;
                } else {
                    // pull from free list
                    int free_ptr = free_head;
                    free_head = linked_store[free_ptr + 1];
                    linked_store[free_ptr] = val;
                    linked_store[free_ptr + 1] = cur_head;
                    block_store[block_ptr + BLOCK_LIST_OFFSET] = free_ptr;
                }
            }

            // count new element
            block_store[block_ptr] += 1;
        }


        public bool Remove(int list_index, int val)
        {
            int block_ptr = list_heads[list_index];
            int N = block_store[block_ptr];


            int iEnd = block_ptr + Math.Min(N, BLOCKSIZE);
            for ( int i = block_ptr+1; i <= iEnd; ++i ) {

                if ( block_store[i] == val ) {
                    for ( int j = i+1; j <= iEnd; ++j )     // shift left
                        block_store[j-1] = block_store[j];
                    //block_store[iEnd] = -2;     // OPTIONAL

                    if (N > BLOCKSIZE) {
                        int cur_ptr = block_store[block_ptr + BLOCK_LIST_OFFSET];
                        block_store[block_ptr + BLOCK_LIST_OFFSET] = linked_store[cur_ptr + 1];  // point to cur->next
                        block_store[iEnd] = linked_store[cur_ptr];
                        add_free_link(cur_ptr); 
                    }

                    block_store[block_ptr] -= 1;
                    return true;
                }

            }

            // search list
            if ( N > BLOCKSIZE ) {
                int cur_ptr = block_store[block_ptr + BLOCK_LIST_OFFSET];
                if ( remove_from_list(block_ptr, cur_ptr, val) ) {
                    block_store[block_ptr] -= 1;
                    return true;
                }
            }

            return false;
        }


        bool remove_from_list(int block_ptr, int cur_ptr, int val)
        {
            int prev_ptr = Null;
            while (cur_ptr != Null) {
                if (linked_store[cur_ptr] == val) {
                    int next_ptr = linked_store[cur_ptr + 1];
                    if (prev_ptr == Null) {
                        block_store[block_ptr + BLOCK_LIST_OFFSET] = next_ptr;
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
            int block_ptr = list_heads[list_index];
            if (block_ptr != Null) {
                int N = block_store[block_ptr];

                // if we have spilled to linked-list, free nodes
                if ( N > BLOCKSIZE ) {
                    int cur_ptr = block_store[block_ptr + BLOCK_LIST_OFFSET];
                    while (cur_ptr != Null) {
                        int free_ptr = cur_ptr;
                        cur_ptr = linked_store[cur_ptr + 1];
                        add_free_link(free_ptr);
                    }
                    block_store[block_ptr + BLOCK_LIST_OFFSET] = Null;
                }

                // free our block
                block_store[block_ptr] = 0;
                free_blocks.push_back(block_ptr);
                list_heads[list_index] = Null;
            }

        }


        public int Count(int list_index)
        {
            int block_ptr = list_heads[list_index];
            return (block_ptr == Null) ? 0 : block_store[block_ptr];
        }


        //public bool Contains(int list_index, int val)
        //{
        //    int cur_ptr = list_heads[list_index];
        //    while (cur_ptr != Null) {
        //        if (linked_store[cur_ptr] == val)
        //            return true;
        //        cur_ptr = linked_store[cur_ptr + 1];
        //    }
        //    return false;
        //}


        public int First(int list_index)
        {
            int block_ptr = list_heads[list_index];
            return block_store[block_ptr+1];

            //return linked_store[list_heads[list_index]];
        }


        public IEnumerable<int> ValueItr(int list_index)
        {
            int block_ptr = list_heads[list_index];
            if (block_ptr != Null) {
                int N = block_store[block_ptr];
                if ( N < BLOCKSIZE ) {
                    int iEnd = block_ptr + N;
                    for (int i = block_ptr + 1; i <= iEnd; ++i)
                        yield return block_store[i];
                } else {
                    // we spilled to linked list, have to iterate through it as well
                    int iEnd = block_ptr + BLOCKSIZE;
                    for (int i = block_ptr + 1; i <= iEnd; ++i)
                        yield return block_store[i];
                    int cur_ptr = block_store[block_ptr + BLOCK_LIST_OFFSET];
                    while (cur_ptr != Null) {
                        yield return linked_store[cur_ptr];
                        cur_ptr = linked_store[cur_ptr + 1];
                    }
                }
            }


        }


        int allocate_block()
        {
            int nfree = free_blocks.size;
            if ( nfree > 0 ) {
                int ptr = free_blocks[nfree - 1];
                free_blocks.pop_back();
                return ptr;
            }
            int nsize = block_store.size;
            block_store.insert(Null, nsize + BLOCK_LIST_OFFSET);
            block_store[nsize] = 0;
            allocated_count++;
            return nsize;
        }


        void add_free_link(int ptr)
        {
            linked_store[ptr + 1] = free_head;
            free_head = ptr;
        }




        public string MemoryUsage
        {
            get {
                return string.Format("ListSize {0}  Blocks Count {1} Free {2} Mem {3}kb  Linked Mem {4}kb",
                    list_heads.size, allocated_count, free_blocks.size * sizeof(int) / 1024, block_store.size, linked_store.size * sizeof(int) / 1024);
            }
        }


    }
}
