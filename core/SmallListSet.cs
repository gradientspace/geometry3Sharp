using System;
using System.Collections.Generic;

namespace g3
{
    /// <summary>
    /// SmallListSet stores a set of short integer-valued variable-size lists.
    /// The lists are encoded into a few large DVector buffers, with internal pooling,
    /// so adding/removing lists usually does not involve any new or delete ops.
    /// 
    /// The lists are stored in two parts. The first N elements are stored in a linear
    /// subset of a dvector. If the list spills past these N elements, the extra elements
    /// are stored in a linked list (which is also stored in a flat array).
    /// 
    /// Each list stores its count, so list-size operations are constant time.
    /// All the internal "pointers" are 32-bit.
    /// </summary>
    public class SmallListSet
    {
        const int Null = -1;

        const int BLOCKSIZE = 8;
        const int BLOCK_LIST_OFFSET = BLOCKSIZE + 1;

        DVector<int> list_heads;        // each "list" is stored as index of first element in block-store (like a pointer)

        DVector<int> block_store;       // flat buffer used to store per-list initial block
                                        // blocks are BLOCKSIZE+2 long, elements are [CurrentCount, item0...itemN, LinkedListPtr]

        DVector<int> free_blocks;       // list of free blocks, indices into block_store
        int allocated_count = 0;

        DVector<int> linked_store;      // flat buffer used for linked-list elements,
                                        // each element is [value, next_ptr]

        int free_head_ptr;              // index of first free element in linked_store


        public SmallListSet()
        {
            list_heads = new DVector<int>();
            linked_store = new DVector<int>();
            free_head_ptr = Null;
            block_store = new DVector<int>();
            free_blocks = new DVector<int>();
        }


        public SmallListSet(SmallListSet copy)
        {
            linked_store = new DVector<int>(copy.linked_store);
            free_head_ptr = copy.free_head_ptr;
            list_heads = new DVector<int>(copy.list_heads);
            block_store = new DVector<int>(copy.block_store);
            free_blocks = new DVector<int>(copy.free_blocks);
        }


        /// <summary>
        /// returns largest current list_index
        /// </summary>
        public int Size {
            get { return list_heads.size; }
        }

        /// <summary>
        /// resize the list-of-lists
        /// </summary>
        public void Resize(int new_size)
        {
            int cur_size = list_heads.size;
            if (new_size > cur_size) {
                list_heads.resize(new_size);
                for (int k = cur_size; k < new_size; ++k)
                    list_heads[k] = Null;
            }
        }


        /// <summary>
        /// create a new list at list_index
        /// </summary>
        public void AllocateAt(int list_index)
        {
            if (list_index >= list_heads.size) {
                int j = list_heads.size;
                list_heads.insert(Null, list_index);
                // need to set intermediate values to null! 
                while (j < list_index) {
                    list_heads[j] = Null;
                    j++;
                }
            } else {
                if (list_heads[list_index] != Null)
                    throw new Exception("SmallListSet: list at " + list_index + " is not empty!");
            }
        }


        /// <summary>
        /// insert val into list at list_index. 
        /// </summary>
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

                if (free_head_ptr == Null) {
                    // allocate new linkedlist node
                    int new_ptr = linked_store.size;
                    linked_store.Add(val);
                    linked_store.Add(cur_head);
                    block_store[block_ptr + BLOCK_LIST_OFFSET] = new_ptr;
                } else {
                    // pull from free list
                    int free_ptr = free_head_ptr;
                    free_head_ptr = linked_store[free_ptr + 1];
                    linked_store[free_ptr] = val;
                    linked_store[free_ptr + 1] = cur_head;
                    block_store[block_ptr + BLOCK_LIST_OFFSET] = free_ptr;
                }
            }

            // count new element
            block_store[block_ptr] += 1;
        }



        /// <summary>
        /// remove val from the list at list_index. return false if val was not in list.
        /// </summary>
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
                if ( remove_from_linked_list(block_ptr, val) ) {
                    block_store[block_ptr] -= 1;
                    return true;
                }
            }

            return false;
        }



        /// <summary>
        /// move list at from_index to to_index
        /// </summary>
        public void Move(int from_index, int to_index)
        {
            if (list_heads[to_index] != Null)
                throw new Exception("SmallListSet.MoveTo: list at " + to_index + " is not empty!");
            if (list_heads[from_index] == Null)
                throw new Exception("SmallListSet.MoveTo: list at " + from_index + " is empty!");
            list_heads[to_index] = list_heads[from_index];
            list_heads[from_index] = Null;
        }






        /// <summary>
        /// remove all elements from list at list_index
        /// </summary>
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


        /// <summary>
        /// return size of list at list_index
        /// </summary>
        public int Count(int list_index)
        {
            int block_ptr = list_heads[list_index];
            return (block_ptr == Null) ? 0 : block_store[block_ptr];
        }


        /// <summary>
        /// search for val in list at list_index
        /// </summary>
        public bool Contains(int list_index, int val)
        {
            int block_ptr = list_heads[list_index];
            if (block_ptr != Null) {
                int N = block_store[block_ptr];
                if (N < BLOCKSIZE) {
                    int iEnd = block_ptr + N;
                    for (int i = block_ptr + 1; i <= iEnd; ++i) {
                        if (block_store[i] == val)
                            return true;
                    }
                } else {
                    // we spilled to linked list, have to iterate through it as well
                    int iEnd = block_ptr + BLOCKSIZE;
                    for (int i = block_ptr + 1; i <= iEnd; ++i) {
                        if (block_store[i] == val)
                            return true;
                    }
                    int cur_ptr = block_store[block_ptr + BLOCK_LIST_OFFSET];
                    while (cur_ptr != Null) {
                        if (linked_store[cur_ptr] == val)
                            return true;
                        cur_ptr = linked_store[cur_ptr + 1];
                    }
                }
            }
            return false;
        }


        /// <summary>
        /// return the first item in the list at list_index (no zero-size-list checking)
        /// </summary>
        public int First(int list_index)
        {
            int block_ptr = list_heads[list_index];
            return block_store[block_ptr+1];
        }


        /// <summary>
        /// iterate over the values of list at list_index
        /// </summary>
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


        /// <summary>
        /// search for findF(list_value) == true, of list at list_index, and return list_value
        /// </summary>
        public int Find(int list_index, Func<int,bool> findF, int invalidValue = -1 )
        {
            int block_ptr = list_heads[list_index];
            if (block_ptr != Null) {
                int N = block_store[block_ptr];
                if (N < BLOCKSIZE) {
                    int iEnd = block_ptr + N;
                    for (int i = block_ptr + 1; i <= iEnd; ++i) {
                        int val = block_store[i];
                        if (findF(val))
                            return val;
                    }
                } else {
                    // we spilled to linked list, have to iterate through it as well
                    int iEnd = block_ptr + BLOCKSIZE;
                    for (int i = block_ptr + 1; i <= iEnd; ++i) {
                        int val = block_store[i];
                        if (findF(val))
                            return val;
                    }
                    int cur_ptr = block_store[block_ptr + BLOCK_LIST_OFFSET];
                    while (cur_ptr != Null) {
                        int val = linked_store[cur_ptr];
                        if (findF(val))
                            return val;
                        cur_ptr = linked_store[cur_ptr + 1];
                    }
                }
            }
            return invalidValue;
        }





        /// <summary>
        /// search for findF(list_value) == true, of list at list_index, and replace with new_value.
        /// returns false if not found
        /// </summary>
        public bool Replace(int list_index, Func<int, bool> findF, int new_value)
        {
            int block_ptr = list_heads[list_index];
            if (block_ptr != Null) {
                int N = block_store[block_ptr];
                if (N < BLOCKSIZE) {
                    int iEnd = block_ptr + N;
                    for (int i = block_ptr + 1; i <= iEnd; ++i) {
                        int val = block_store[i];
                        if (findF(val)) {
                            block_store[i] = new_value;
                            return true;
                        }
                    }
                } else {
                    // we spilled to linked list, have to iterate through it as well
                    int iEnd = block_ptr + BLOCKSIZE;
                    for (int i = block_ptr + 1; i <= iEnd; ++i) {
                        int val = block_store[i];
                        if (findF(val)) {
                            block_store[i] = new_value;
                            return true;
                        }
                    }
                    int cur_ptr = block_store[block_ptr + BLOCK_LIST_OFFSET];
                    while (cur_ptr != Null) {
                        int val = linked_store[cur_ptr];
                        if (findF(val)) {
                            linked_store[cur_ptr] = new_value;
                            return true;
                        }
                        cur_ptr = linked_store[cur_ptr + 1];
                    }
                }
            }
            return false;
        }



        // grab a block from the free list, or allocate a new one
        protected int allocate_block()
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


        // push a link-node onto the free list
        void add_free_link(int ptr)
        {
            linked_store[ptr + 1] = free_head_ptr;
            free_head_ptr = ptr;
        }


        // remove val from the linked-list attached to block_ptr
        bool remove_from_linked_list(int block_ptr, int val)
        {
            int cur_ptr = block_store[block_ptr + BLOCK_LIST_OFFSET];
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



        public string MemoryUsage
        {
            get {
                return string.Format("ListSize {0}  Blocks Count {1} Free {2} Mem {3}kb  Linked Mem {4}kb",
                    list_heads.size, allocated_count, free_blocks.size * sizeof(int) / 1024, block_store.size, linked_store.size * sizeof(int) / 1024);
            }
        }


    }
}
