using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{

    /// <summary>
    /// This is a min-heap priority queue class that does not use an object for each queue node.
    /// Integer IDs must be provided by the user to identify unique nodes.
    /// Internally an array is used to keep track of the mapping from ids to internal indices,
    /// so the max ID must also be provided.
    /// 
    /// See DijkstraGraphDistance for example usage.
    /// 
    /// conceptually based on https://github.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp
    /// </summary>
    public class IndexPriorityQueue : IEnumerable<int>
    {
        // set this to true during development to catch issues
        public bool EnableDebugChecks = false;

        struct QueueNode
        {
            public int id;              // external id

            public float priority;      // the priority of this id
            public int index;           // index in tree data structure (tree is stored as flat array)
        }

        DVector<QueueNode> nodes;       // tree of allocated nodes, stored linearly. active up to num_nodes (allocated may be larger)
        int num_nodes;                  // count of active nodes
        int[] id_to_index;              // mapping from external ids to internal node indices
                                        // [TODO] could make this sparse using SparseList...


        /// <summary>
        /// maxIndex parameter is required because internally a fixed-size array is used to track mapping
        /// from IDs to internal node indices. If this seems problematic because you won't be inserting the
        /// full index space, consider a DynamicPriorityQueue instead.
        /// </summary>
        public IndexPriorityQueue(int maxID)
        {
            nodes = new DVector<QueueNode>();
            id_to_index = new int[maxID];
            for (int i = 0; i < maxID; ++i)
                id_to_index[i] = -1;
            num_nodes = 0;
        }


        public int Count {
            get { return num_nodes; }
        }


        /// <summary>
        /// reset the queue to empty state. 
        /// if bFreeMemory is false, we don't discard internal data structures, so there will be less allocation next time
        /// (this does not make a huge difference...)
        /// </summary>
        public void Clear(bool bFreeMemory = true)
        {
            if ( bFreeMemory )
                nodes = new DVector<QueueNode>();
            Array.Clear(id_to_index, 0, id_to_index.Length);
            num_nodes = 0;
        }


        /// <summary>
        /// id of node at head of queue
        /// </summary>
        public int First {
            get { return nodes[1].id; }
        }

        /// <summary>
        /// Priority of node at head of queue
        /// </summary>
        public float FirstPriority {
            get { return nodes[1].priority; }
        }

        /// <summary>
        /// constant-time check to see if id is already in queue
        /// </summary>
        public bool Contains(int id) {
            int iNode = id_to_index[id];
            if (iNode <= 0 || iNode > num_nodes)
                return false;
            return nodes[iNode].index > 0;
        }


        /// <summary>
        /// Add id to list w/ given priority
        /// Behavior is undefined if you call w/ same id twice
        /// </summary>
        public void Insert(int id, float priority)
        {
            if (EnableDebugChecks && Contains(id) == true)
                throw new Exception("IndexPriorityQueue.Insert: tried to add node that is already in queue!");

            QueueNode node = new QueueNode();
            node.id = id;
            node.priority = priority;
            num_nodes++;
            node.index = num_nodes;
            id_to_index[id] = node.index;
            nodes.insert(node, num_nodes);
            move_up(nodes[num_nodes].index);
        }
        public void Enqueue(int id, float priority) {
            Insert(id, priority);
        }

        /// <summary>
        /// remove node at head of queue, update queue, and return id for that node
        /// </summary>
        public int Dequeue()
        {
            if (EnableDebugChecks && Count == 0)
                throw new Exception("IndexPriorityQueue.Dequeue: queue is empty!");

            int id = nodes[1].id;
            remove_at_index(1);
            return id;
        }

        /// <summary>
        /// remove this node from queue. Undefined behavior if called w/ same id twice!
        /// Behavior is undefined if you call w/ id that is not in queue
        /// </summary>
        public void Remove(int id)
        {
            if (EnableDebugChecks && Contains(id) == false)
                throw new Exception("IndexPriorityQueue.Remove: tried to remove node that does not exist in queue!");

            int iNode = id_to_index[id];
            remove_at_index(iNode);
        }


        /// <summary>
        /// update priority at node id, and then move it to correct position in queue
        /// Behavior is undefined if you call w/ id that is not in queue
        /// </summary>
        public void Update(int id, float priority)
        {
            if (EnableDebugChecks && Contains(id) == false)
                throw new Exception("IndexPriorityQueue.Update: tried to update node that does not exist in queue!");

            int iNode = id_to_index[id];

            QueueNode n = nodes[iNode];
            n.priority = priority;
            nodes[iNode] = n;

            on_node_updated(iNode);
        }


        /// <summary>
        /// Query the priority at node id, assuming it exists in queue
        /// </summary>
        public float GetPriority(int id)
        {
            if (EnableDebugChecks && Contains(id) == false)
                throw new Exception("IndexPriorityQueue.Update: tried to get priorty of node that does not exist in queue!");
            int iNode = id_to_index[id];
            return nodes[iNode].priority;
        }



        public IEnumerator<int> GetEnumerator()
        {
            for (int i = 1; i <= num_nodes; i++)
                yield return nodes[i].id;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }



        /*
         * Internals
         */


        private void remove_at_index(int iNode)
        {
            // node-is-last-node case
            if(iNode == num_nodes) {
                nodes[iNode] = new QueueNode();
                num_nodes--;
                return;
            }

            // [RMS] is there a better way to do this? seems random to move the last node to
            // top of tree? But I guess otherwise we might have to shift entire branches??

            //Swap the node with the last node
            swap_nodes_at_indices(iNode, num_nodes);
            // after swap, inode is the one we want to keep, and numNodes is the one we discard
            nodes[num_nodes] = new QueueNode();
            num_nodes--;

            //Now shift iNode (ie the former last node) up or down as appropriate
            on_node_updated(iNode);
        }



        private void swap_nodes_at_indices(int i1, int i2)
        {
            QueueNode n1 = nodes[i1];
            n1.index = i2;
            QueueNode n2 = nodes[i2];
            n2.index = i1;
            nodes[i1] = n2;
            nodes[i2] = n1;

            id_to_index[n2.id] = i1;
            id_to_index[n1.id] = i2;
        }

        /// move node at iFrom to iTo
        private void move(int iFrom, int iTo)
        {
            QueueNode n = nodes[iFrom];
            n.index = iTo;
            nodes[iTo] = n;
            id_to_index[n.id] = iTo;
        }

        /// set node at iTo
        private void set(int iTo, ref QueueNode n)
        {
            n.index = iTo;
            nodes[iTo] = n;
            id_to_index[n.id] = iTo;
        }


        // move iNode up tree to correct position by iteratively swapping w/ parent
        private void move_up(int iNode)
        {
            // save start node, we will move this one to correct position in tree
            int iStart = iNode;
            QueueNode iStartNode = nodes[iStart];

            // while our priority is lower than parent, we swap upwards, ie move parent down
            int iParent = iNode / 2;
            while ( iParent >= 1 ) {
                if (nodes[iParent].priority < iStartNode.priority)
                    break;
                move(iParent, iNode);
                iNode = iParent;
                iParent = nodes[iNode].index / 2;
            }

            // write input node into final position, if we moved it
            if (iNode != iStart) {
                set(iNode, ref iStartNode);
            }
        }


        // move iNode down tree branches to correct position, by iteratively swapping w/ children
        private void move_down(int iNode)
        {
            // save start node, we will move this one to correct position in tree
            int iStart = iNode;
            QueueNode iStartNode = nodes[iStart];

            // keep moving down until lower nodes have higher priority
            while (true) {
                int iMoveTo = iNode;
                int iLeftChild = 2 * iNode;

                // past end of tree, must be in the right spot
                if (iLeftChild > num_nodes) {
                    break;
                }

                // check if priority is larger than either child - if so we want to swap
                float min_priority = iStartNode.priority;
                float left_child_priority = nodes[iLeftChild].priority;
                if (left_child_priority < min_priority) {
                    iMoveTo = iLeftChild;
                    min_priority = left_child_priority;
                }
                int iRightChild = iLeftChild + 1;
                if (iRightChild <= num_nodes) {
                    if (nodes[iRightChild].priority < min_priority) {
                        iMoveTo = iRightChild;
                    }
                }

                // if we found node with higher priority, swap with it (ie move it up) and take its place
                // (but we only write start node to final position, not intermediary slots)
                if (iMoveTo != iNode) {
                    move(iMoveTo, iNode);
                    iNode = iMoveTo;
                } else {
                    break;
                }
            }

            // if we moved node, write it to its new position
            if ( iNode != iStart ) {
                set(iNode, ref iStartNode);
            }
        }




        /// call after node is modified, to move it to correct position in queue
        private void on_node_updated(int iNode) {
            int iParent = iNode / 2;
            if ( iParent > 0 && has_higher_priority(iNode, iParent) )
                move_up(iNode);
            else
                move_down(iNode);
        }






        /// returns true if priority at iHigher is less than at iLower
        private bool has_higher_priority(int iHigher, int iLower)
        {
            return (nodes[iHigher].priority < nodes[iLower].priority);
        }



        /// <summary>
        /// Check if queue has been corrupted
        /// </summary>
        public bool IsValidQueue()
        {
            for (int i = 1; i < num_nodes; i++) {
                int childLeftIndex = 2 * i;
                if (childLeftIndex < num_nodes && has_higher_priority(childLeftIndex, i))
                    return false;

                int childRightIndex = childLeftIndex + 1;
                if (childRightIndex < num_nodes && has_higher_priority(childRightIndex, i))
                    return false;
            }
            return true;
        }


        public void DebugPrint() {
            for (int i = 1; i <= num_nodes; ++i)
                System.Console.WriteLine("{0} : p {1}  index {2}  id {3}", i, nodes[i].priority, nodes[i].index, nodes[i].id);
        }


    }
}
