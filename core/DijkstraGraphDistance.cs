using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using g3ext;

namespace g3
{
    public class DijkstraGraphDistance
    {
        class GraphNode : FastPriorityQueueNode, IEquatable<GraphNode>
        {
            public int id;
            public GraphNode parent;
            public bool frozen;

            public bool Equals(GraphNode other) {
                return id == other.id;
            }
        }

        FastPriorityQueue<GraphNode> Queue;

        Func<int, int, float> NodeDistanceF;
        Func<int, IEnumerable<int>> NeighboursF;

        // maybe should be sparse array?
        List<int> Seeds;
        SparseObjectList<GraphNode> Nodes;
        float max_value;

        public DijkstraGraphDistance(int nMaxNodes, int nMaxID,
            IEnumerable<int> nodeIDs, 
            Func<int, int, float> nodeDistanceF,
            Func<int,IEnumerable<int>> neighboursF,
            IEnumerable<Vector2d> seeds = null                // these are pairs (index, seedval)
            )
        {
            int initial_max = (nMaxNodes < 1024) ? nMaxNodes : nMaxNodes / 4;
            Queue = new FastPriorityQueue<GraphNode>(initial_max);
            Queue.ENABLE_DEBUG_SAFETY_CHECKS = false;   // otherwise debug is super-slow

            NodeDistanceF = nodeDistanceF;
            NeighboursF = neighboursF;

            Nodes = new SparseObjectList<GraphNode>(nMaxID, nMaxNodes);
            Seeds = new List<int>();

            max_value = float.MinValue;

            if (seeds != null) {
                foreach (var v in seeds)
                    AddSeed((int)v.x, (float)v.y);
            }
        }
      

        public void AddSeed(int id, float seed_dist)
        {
            GraphNode g = get_node(id);
            Debug.Assert(Queue.Contains(g) == false);
            enqueue_node(g, seed_dist);
            Seeds.Add(id);
        }
        public bool IsSeed(int id)
        {
            return Seeds.Contains(id);
        }


        public void Compute()
        {
            // queue all the seeds nbrs
            //foreach ( int sid in Seeds ) {
            //    update_neighbours(sid);
            //}

            while ( Queue.Count > 0) {
                GraphNode g = Queue.Dequeue();
                max_value = Math.Max(g.Priority, max_value);
                g.frozen = true;
                update_neighbours(g);
            }
        }


        public float MaxDistance {
            get { return max_value; }
        }


        public float GetDistance(int id)
        {
            GraphNode g = Nodes[id];
            if (g == null)
                return float.MaxValue;
            return g.Priority;
        }




        GraphNode get_node(int id) {
            GraphNode g = Nodes[id];
            if (g == null) {
                g = new GraphNode() { id = id, parent = null, frozen = false };
                Nodes[id] = g;
            }
            return g;
        }

        void enqueue_node(GraphNode g, float dist)
        {
            if (Queue.Count == Queue.MaxSize - 1)
                Queue.Resize(Queue.MaxSize + Queue.MaxSize / 2);
            Queue.Enqueue(g, dist);
        }

        void update_neighbours(int id)
        {
            GraphNode parent = get_node(id);
            update_neighbours(parent);
        }
        void update_neighbours(GraphNode parent)
        {
            //Debug.Assert(Queue.Contains(parent));

            float cur_dist = parent.Priority;
            foreach (int nbr_id in NeighboursF(parent.id)) {
                float nbr_dist = NodeDistanceF(parent.id, nbr_id) + cur_dist;
                GraphNode nbr = get_node(nbr_id);
                if (nbr.frozen)
                    continue;

                if (Queue.Contains(nbr)) {
                    if (nbr_dist < nbr.Priority) {
                        nbr.parent = parent;
                        Queue.UpdatePriority(nbr, nbr_dist);
                    }
                } else {
                    enqueue_node(nbr, nbr_dist);
                }
            }
        }







    }
}
