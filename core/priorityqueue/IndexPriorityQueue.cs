using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{

    /// <summary>
    /// This is a priority queue class that does not use an object for each queue node.
    /// Integer IDs must be provided by the user to identify unique nodes.
    /// The max expected ID must also be provided (although this could be relaxed by resizing in Enqueue...)
    /// 
    /// conceptually based on https://github.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp
    /// </summary>
    public class IndexPriorityQueue
    {
        // set this to true during development to catch issues
        public bool EnableDebugChecks = false;

        struct QueueNode
        {
            public int id;

            public float priority;      // queue internals
            public int index;           // queue internals
        }

        DVector<QueueNode> nodes;       // list of allocated nodes, active up to num_nodes
        int num_nodes;                  // count of active nodes
        int[] id_to_index;              // mapping from external ids to internal node indices
                                        // [TODO] could make this sparse somehow? use SparseList?


        public IndexPriorityQueue(int maxIndex)
        {
            nodes = new DVector<QueueNode>();
            id_to_index = new int[maxIndex];
            for (int i = 0; i < maxIndex; ++i)
                id_to_index[i] = -1;
            num_nodes = 0;
        }


        public int Count {
            get { return num_nodes; }
        }


        public void Clear()
        {
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
        public void Enqueue(int id, float priority)
        {
            if (EnableDebugChecks && Contains(id) == true)
                throw new Exception("IndexPriorityQueue.Enqueue: tried to add node that is already in queue!");

            QueueNode node = new QueueNode();
            node.id = id;
            node.priority = priority;
            num_nodes++;
            node.index = num_nodes;
            id_to_index[id] = node.index;
            nodes.insert(node, num_nodes);
            move_up(nodes[num_nodes].index);
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

            //Swap the node with the last node
            swap_nodes_at_indices(iNode, num_nodes);
            // after swap, inode is the one we want to keep, and numNodes is the one we discard
            nodes[num_nodes] = new QueueNode();
            num_nodes--;

            //Now bubble formerLastNode (which is no longer the last node) up or down as appropriate
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

        /// <summary>
        /// move node at iFrom to iTo
        /// </summary>
        private void move(int iFrom, int iTo)
        {
            QueueNode n = nodes[iFrom];
            n.index = iTo;
            nodes[iTo] = n;
            id_to_index[n.id] = iTo;
        }



        private void move_up(int iNode)
        {
            // while our priority is lower than parent, we swap upwards
            int iParent = iNode / 2;
            while ( iParent >= 1 ) {
                if ( compare_priority(iParent, iNode) )
                    break;
                swap_nodes_at_indices(iNode, iParent);
                iNode = iParent;
                iParent = nodes[iNode].index / 2;
            }
        }



        private void move_down(int iNode)
        {
            while (true) {
                int iParent = iNode;
                int iLeftChild = 2 * iNode;

                // past end of tree, must be in the right spot
                if(iLeftChild > num_nodes) {
                    break;
                }

                // check if priority is higher than either child
                if( compare_priority(iLeftChild, iParent) ) 
                    iParent = iLeftChild;

                int iRightChild = iLeftChild + 1;
                if ( iRightChild <= num_nodes ) {
                    if ( compare_priority(iRightChild, iParent) )
                        iParent = iRightChild;
                }

                // if we found node with higher priority, swap downwards with that child and continue
                if (iParent != iNode) {
                    swap_nodes_at_indices(iParent, iNode);
                    iNode = iParent;
                } else {
                    break;
                }
            }
        }





        /// <summary>
        /// call after node is modified, to move it to correct position in queue
        /// </summary>
        private void on_node_updated(int iNode) {
            int iParent = iNode / 2;
            if ( iParent > 0 && compare_priority(iNode, iParent) )
                move_up(iNode);
            else
                move_down(iNode);
        }






        /// <summary>
        /// returns true if priority at iHigher is less than at iLower
        /// </summary>
        private bool compare_priority(int iHigher, int iLower)
        {
            return (nodes[iHigher].priority < nodes[iLower].priority);
        }



        public void DebugPrint() {
            for (int i = 1; i <= num_nodes; ++i)
                System.Console.WriteLine("{0} : p {1}  index {2}  id {3}", i, nodes[i].priority, nodes[i].index, nodes[i].id);
        }


    }
}
