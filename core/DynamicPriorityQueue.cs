using System;
using System.Collections;
using System.Collections.Generic;

namespace g3
{


    /// <summary>
    /// To use DynamicPriorityQueue, your queue node type needs to subclass this one.
    /// However the priority and index members are for internal queue use, not yours!
    /// </summary>
    public abstract class DynamicPriorityQueueNode
    {
        // queue priority value for this node. never modify this value!
        public float priority { get; protected internal set; }

        // current position in the queue's tree/array. not meaningful externally, do not use this value for anything!!
        internal int index { get; set; }
    }




    /// <summary>
    /// This is a min-heap priority queue class that does not use any fixed-size internal data structures.
    /// It is maent mainly for use on subsets of larger graphs.
    /// If you need a PQ for a larger portion of a graph, consider IndexPriorityQueue instead.
    /// 
    /// You need to subclass DynamicPriorityQueueNode, and *you* allocate the nodes, not the queue.
    /// If there is a chance you will re-use nodes, consider using a MemoryPool<T>.
    /// See DijkstraGraphDistance for example usage.
    /// 
    /// conceptually based on https://github.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp
    /// </summary>
    public class DynamicPriorityQueue<T> : IEnumerable<T>
        where T : DynamicPriorityQueueNode
    {
        // set this to true during development to catch issues
        public bool EnableDebugChecks = false;

        DVector<T> nodes;       // tree of allocated nodes, stored linearly. active up to num_nodes (allocated may be larger)
        int num_nodes;          // count of active nodes



        public DynamicPriorityQueue()
        {
            num_nodes = 0;
            nodes = new DVector<T>();
        }

        /// <summary>
        /// number of nodes currently in queue
        /// </summary>
        public int Count {
            get { return num_nodes; }
        }


        /// <summary>
        /// reset the queue to empty state. 
        /// if bFreeMemory is false, we don't discard internal data structures, so there will be less allocation next time
        /// (this does not make a huge difference...)
        /// </summary>
        public void Clear(bool bFreeMemory = true) {
            if ( bFreeMemory )
                nodes = new DVector<T>();
            num_nodes = 0;
        }

        /// <summary>
        /// node at head of queue
        /// </summary>
        public T First {
            get { return nodes[1]; }
        }

        /// <summary>
        /// Priority of node at head of queue
        /// </summary>
        public float FirstPriority {
            get { return nodes[1].priority; }
        }


        /// <summary>
        /// constant-time check to see if node is already in queue
        /// </summary>
        public bool Contains(T node) {
            return (nodes[node.index] == node);
        }


        /// <summary>
        /// Add node to list w/ given priority
        /// Behavior is undefined if you call w/ same node twice
        /// </summary>
        public void Enqueue(T node, float priority)
        {
            if (EnableDebugChecks && Contains(node) == true)
                throw new Exception("DynamicPriorityQueue.Enqueue: tried to add node that is already in queue!");

            node.priority = priority;
            num_nodes++;
            nodes.insert(node, num_nodes);
            node.index = num_nodes;
            move_up(nodes[num_nodes]);
        }

        /// <summary>
        /// remove node at head of queue, update queue, and return that node
        /// </summary>
        public T Dequeue()
        {
            if (EnableDebugChecks && Count == 0)
                throw new Exception("DynamicPriorityQueue.Dequeue: queue is empty!");

            T returnMe = nodes[1];
            Remove(returnMe);
            return returnMe;
        }



        /// <summary>
        /// remove this node from queue. Undefined behavior if called w/ same node twice!
        /// Behavior is undefined if you call w/ node that is not in queue
        /// </summary>
        public void Remove(T node)
        {
            if (EnableDebugChecks && Contains(node) == false)
                throw new Exception("DynamicPriorityQueue.Remove: tried to remove node that does not exist in queue!");

            //If the node is already the last node, we can remove it immediately
            if (node.index == num_nodes) {
                nodes[num_nodes] = null;
                num_nodes--;
                return;
            }

            //Swap the node with the last node
            T formerLastNode = nodes[num_nodes];
            swap_nodes(node, formerLastNode);
            nodes[num_nodes] = null;
            num_nodes--;

            //Now bubble formerLastNode (which is no longer the last node) up or down as appropriate
            on_node_updated(formerLastNode);
        }




        /// <summary>
        /// update priority at node, and then move it to correct position in queue
        /// Behavior is undefined if you call w/ node that is not in queue
        /// </summary>
        public void Update(T node, float priority)
        {
            if (EnableDebugChecks && Contains(node) == false)
                throw new Exception("DynamicPriorityQueue.Update: tried to update node that does not exist in queue!");

            node.priority = priority;
            on_node_updated(node);
        }



        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 1; i <= num_nodes; i++)
                yield return nodes[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }



        /*
         * Internals
         */


        // swap node locations and indices
        private void swap_nodes(T node1, T node2)
        {
            nodes[node1.index] = node2;
            nodes[node2.index] = node1;
            int temp = node1.index;
            node1.index = node2.index;
            node2.index = temp;
        }


        // move node up tree to correct position by iteratively swapping w/ parent
        private void move_up(T node)
        {
            int parent = node.index / 2;
            while (parent >= 1) {
                T parentNode = nodes[parent];
                if (has_higher_priority(parentNode, node))
                    break;
                swap_nodes(node, parentNode);
                parent = node.index / 2;
            }
        }


        // move node down tree branches to correct position, by iteratively swapping w/ children
        private void move_down(T node)
        {
            // we will put input node at this position after we are done swaps (ie actually moves, not swaps)
            int cur_node_index = node.index;

            while (true) {
                T min_node = node;
                int iLeftChild = 2 * cur_node_index;

                // past end of tree, must be in the right spot
                if (iLeftChild > num_nodes) {
                    // store input node in final position
                    node.index = cur_node_index;
                    nodes[cur_node_index] = node;
                    break;
                }

                // check if priority is larger than either child - if so we want to swap
                T left_child_node = nodes[iLeftChild];
                if (has_higher_priority(left_child_node, min_node)) {
                    min_node = left_child_node;
                }
                int iRightChild = iLeftChild + 1;
                if (iRightChild <= num_nodes) {
                    T right_child_node = nodes[iRightChild];
                    if (has_higher_priority(right_child_node, min_node)) {
                        min_node = right_child_node;
                    }
                }

                // if we found node with higher priority, swap with it (ie move it up) and take its place
                // (but we only write start node to final position, not intermediary slots)
                if (min_node != node) {
                    nodes[cur_node_index] = min_node;

                    int temp = min_node.index;
                    min_node.index = cur_node_index;
                    cur_node_index = temp;
                } else {
                    // store input node in final position
                    node.index = cur_node_index;
                    nodes[cur_node_index] = node;
                    break;
                }
            }
        }





        /// call after node is modified, to move it to correct position in queue
        private void on_node_updated(T node)
        {
            int parentIndex = node.index / 2;
            T parentNode = nodes[parentIndex];
            if (parentIndex > 0 && has_higher_priority(node, parentNode)) 
                move_up(node);
            else
                move_down(node);
        }


        /// returns true if priority at higher is less than at lower
        private bool has_higher_priority(T higher, T lower)
        {
            return (higher.priority < lower.priority);
        }



        /// <summary>
        /// Check if queue has been corrupted
        /// </summary>
        public bool IsValidQueue()
        {
            for (int i = 1; i < num_nodes; i++) {
                if (nodes[i] != null) {
                    int childLeftIndex = 2 * i;
                    if (childLeftIndex < num_nodes && nodes[childLeftIndex] != null && has_higher_priority(nodes[childLeftIndex], nodes[i]))
                        return false;

                    int childRightIndex = childLeftIndex + 1;
                    if (childRightIndex < num_nodes && nodes[childRightIndex] != null && has_higher_priority(nodes[childRightIndex], nodes[i]))
                        return false;
                }
            }
            return true;
        }




        public void DebugPrint()
        {
            for (int i = 1; i <= num_nodes; ++i)
                System.Console.WriteLine("{0} : p {1}  idx {2}", i, nodes[i].priority, nodes[i].index);
        }

    }
}
