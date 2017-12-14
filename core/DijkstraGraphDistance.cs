using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace g3
{
    /// <summary>
    /// Compute Dijkstra shortest-path algorithm on a graph. 
    /// Computation is index-based, but can use sparse data
    /// structures if the index space will be sparse.
    /// 
    /// Construction is somewhat complicated, but see shortcut static
    /// methods at end of file for common construction cases:
    ///   - MeshVertices(mesh) - compute on vertices of mesh
    /// 
    /// </summary>
    public class DijkstraGraphDistance
    {

        /// <summary>
        /// if you enable this, then you can call GetOrder()
        /// </summary>
        public bool TrackOrder = false;


        class GraphNode : DynamicPriorityQueueNode, IEquatable<GraphNode>
        {
            public int id;
            public GraphNode parent;
            public bool frozen;

            public bool Equals(GraphNode other)
            {
                return id == other.id;
            }
        }

        DynamicPriorityQueue<GraphNode> SparseQueue;
        SparseObjectList<GraphNode> SparseNodes;
        MemoryPool<GraphNode> SparseNodePool;


        struct GraphNodeStruct : IEquatable<GraphNodeStruct>
        {
            public int id;
            public int parent;
            public bool frozen;
            public float distance;

            public GraphNodeStruct(int id, int parent, float distance)
            {
                this.id = id; this.parent = parent; this.distance = distance; frozen = false;
            }

            public bool Equals(GraphNodeStruct other)
            {
                return id == other.id;
            }
            public static readonly GraphNodeStruct Zero = new GraphNodeStruct() { id = -1, parent = -1, distance = float.MaxValue, frozen = false };
        }


        IndexPriorityQueue DenseQueue;
        GraphNodeStruct[] DenseNodes;

        Func<int,bool> NodeFilterF;
        Func<int, int, float> NodeDistanceF;
        Func<int, IEnumerable<int>> NeighboursF;

        // maybe should be sparse array?
        List<int> Seeds;
        float max_value;

        List<int> order;

        /// <summary>
        /// Constructor configures the graph as well. Graph is not specified
        /// explicitly, is provided via functions, for maximum flexibility.
        /// 
        /// nMaxID: maximum ID that will be added. 
        /// bSparse: is ID space large but sparse? this will save memory
        /// nodeFilterF: restrict to a subset of nodes (eg if you want to filter neighbours but not change neighboursF
        /// nodeDistanceF: must return (symmetric) distance between two nodes a and b
        /// neighboursF: return enumeration of neighbours of a
        /// seeds: although Vector2d, are actually pairs (id, seedvalue)   (or use AddSeed later)
        /// </summary>
        public DijkstraGraphDistance(int nMaxID, bool bSparse,
            Func<int,bool> nodeFilterF,
            Func<int, int, float> nodeDistanceF,
            Func<int, IEnumerable<int>> neighboursF,
            IEnumerable<Vector2d> seeds = null                // these are pairs (index, seedval)
            )
        {
            NodeFilterF = nodeFilterF;
            NodeDistanceF = nodeDistanceF;
            NeighboursF = neighboursF;


            if (bSparse) {
                SparseQueue = new DynamicPriorityQueue<GraphNode>();
                SparseNodes = new SparseObjectList<GraphNode>(nMaxID, 0);
                SparseNodePool = new MemoryPool<GraphNode>();
            } else {
                DenseQueue = new IndexPriorityQueue(nMaxID);
                DenseNodes = new GraphNodeStruct[nMaxID];
            }

            Seeds = new List<int>();
            max_value = float.MinValue;
            if (seeds != null) {
                foreach (var v in seeds)
                    AddSeed((int)v.x, (float)v.y);
            }

        }


        public void Reset()
        {
            if ( SparseNodes != null ) {
                SparseQueue.Clear(false);
                SparseNodes.Clear();
                SparseNodePool.ReturnAll();

            } else {
                DenseQueue.Clear(false);
                Array.Clear(DenseNodes, 0, DenseNodes.Length);
            }

            Seeds = new List<int>();
            max_value = float.MinValue;
        }


        /// <summary>
        /// Add seed point as id/distance pair
        /// </summary>
        public void AddSeed(int id, float seed_dist)
        {
            if (SparseNodes != null) {
                GraphNode g = get_node(id);
                Debug.Assert(SparseQueue.Contains(g) == false);
                SparseQueue.Enqueue(g, seed_dist);
            } else {
                Debug.Assert(DenseQueue.Contains(id) == false);
                enqueue_node_dense(id, seed_dist);
            }
            Seeds.Add(id);
        }
        public bool IsSeed(int id)
        {
            return Seeds.Contains(id);
        }


        /// <summary>
        /// Compute distances from seeds to all other ids.
        /// </summary>
        public void Compute()
        {
            if (TrackOrder == true)
                order = new List<int>();

            if (SparseNodes != null)
                Compute_Sparse();
            else
                Compute_Dense();
        }
        protected void Compute_Sparse()
        {
            while (SparseQueue.Count > 0) {
                GraphNode g = SparseQueue.Dequeue();
                max_value = Math.Max(g.priority, max_value);
                g.frozen = true;
                if (TrackOrder)
                    order.Add(g.id);
                update_neighbours_sparse(g);
            }
        }
        protected void Compute_Dense()
        {
            while (DenseQueue.Count > 0) {
                float idx_priority = DenseQueue.FirstPriority;
                int idx = DenseQueue.Dequeue();
                GraphNodeStruct g = DenseNodes[idx];
                g.frozen = true;
                if (TrackOrder)
                    order.Add(g.id);
                g.distance = max_value;
                DenseNodes[idx] = g;
                max_value = Math.Max(idx_priority, max_value);
                update_neighbours_dense(g.id);
            }
        }


        /// <summary>
        /// Compute distances that are less/equal to fMaxDistance from the seeds
        /// Terminates early, so Queue may not be empty
        /// </summary>
        public void ComputeToMaxDistance(float fMaxDistance)
        {
            if (TrackOrder == true)
                order = new List<int>();

            if (SparseNodes != null)
                ComputeToMaxDistance_Sparse(fMaxDistance);
            else
                ComputeToMaxDistance_Dense(fMaxDistance);
        }
        protected void ComputeToMaxDistance_Sparse(float fMaxDistance)
        {
            while (SparseQueue.Count > 0) {
                GraphNode g = SparseQueue.Dequeue();
                max_value = Math.Max(g.priority, max_value);
                if (max_value > fMaxDistance)
                    return;
                g.frozen = true;
                if (TrackOrder)
                    order.Add(g.id);
                update_neighbours_sparse(g);
            }
        }
        protected void ComputeToMaxDistance_Dense(float fMaxDistance)
        {
            while (DenseQueue.Count > 0) {
                float idx_priority = DenseQueue.FirstPriority;
                max_value = Math.Max(idx_priority, max_value);
                if (max_value > fMaxDistance)
                    return;
                int idx = DenseQueue.Dequeue();
                GraphNodeStruct g = DenseNodes[idx];
                g.frozen = true;
                if (TrackOrder)
                    order.Add(g.id);
                g.distance = max_value;
                DenseNodes[idx] = g;
                update_neighbours_dense(g.id);
            }
        }


        /// <summary>
        /// Get the maximum distance encountered during the Compute()
        /// </summary>
        public float MaxDistance {
            get { return max_value; }
        }


        /// <summary>
        /// Get the computed distance at node id. returns float.MaxValue if node was not computed.
        /// </summary>
        public float GetDistance(int id)
        {
            if (SparseNodes != null) {
                GraphNode g = SparseNodes[id];
                if (g == null)
                    return float.MaxValue;
                return g.priority;
            } else {
                GraphNodeStruct g = DenseNodes[id];
                return (g.frozen) ? g.distance : float.MaxValue;
            }
        }



        public List<int> GetOrder()
        {
            if (TrackOrder == false)
                throw new InvalidOperationException("DijkstraGraphDistance.GetOrder: Must set TrackOrder = true");
            return order;
        }



        GraphNode get_node(int id)
        {
            GraphNode g = SparseNodes[id];
            if (g == null) {
                //g = new GraphNode() { id = id, parent = null, frozen = false };
                g = SparseNodePool.Allocate();
                g.id = id; g.parent = null; g.frozen = false;
                SparseNodes[id] = g;
            }
            return g;
        }



        void update_neighbours_sparse(GraphNode parent)
        {
            float cur_dist = parent.priority;
            foreach (int nbr_id in NeighboursF(parent.id)) {
                if (NodeFilterF(nbr_id) == false)
                    continue;

                GraphNode nbr = get_node(nbr_id);
                if (nbr.frozen)
                    continue;

                float nbr_dist = NodeDistanceF(parent.id, nbr_id) + cur_dist;
                if (SparseQueue.Contains(nbr)) {
                    if (nbr_dist < nbr.priority) {
                        nbr.parent = parent;
                        SparseQueue.Update(nbr, nbr_dist);
                    }
                } else {
                    SparseQueue.Enqueue(nbr, nbr_dist);
                }
            }
        }




        void enqueue_node_dense(int id, float dist)
        {
            GraphNodeStruct g = new GraphNodeStruct(id, -1, dist);
            DenseNodes[id] = g;
            DenseQueue.Insert(id, dist);
        }

        void update_neighbours_dense(int parent_id)
        {
            GraphNodeStruct g = DenseNodes[parent_id];
            float cur_dist = g.distance;
            foreach (int nbr_id in NeighboursF(parent_id)) {
                if (NodeFilterF(nbr_id) == false)
                    continue;

                GraphNodeStruct nbr = DenseNodes[nbr_id];
                if (nbr.frozen)
                    continue;

                float nbr_dist = NodeDistanceF(parent_id, nbr_id) + cur_dist;
                if (DenseQueue.Contains(nbr_id)) {
                    if (nbr_dist < nbr.distance) {
                        nbr.parent = parent_id;
                        DenseQueue.Update(nbr_id, nbr_dist);
                        DenseNodes[nbr_id] = nbr;
                    }
                } else {
                    enqueue_node_dense(nbr_id, nbr_dist);
                }
            }
        }



        /// <summary>
        /// shortcut to setup functions for mesh vertices
        /// </summary>
        public static DijkstraGraphDistance MeshVertices(DMesh3 mesh)
        {
            return new DijkstraGraphDistance(
                mesh.MaxVertexID, false,
                (id) => { return true; },
                (a, b) => { return (float)mesh.GetVertex(a).Distance(mesh.GetVertex(b)); },
                mesh.VtxVerticesItr);
        }

    }
}
