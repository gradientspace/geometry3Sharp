using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace g3
{
    public class MeshLocalParam
    {
        public static readonly Vector2f InvalidUV = new Vector2f(float.MaxValue, float.MaxValue);


        public enum UVModes
        {
            ExponentialMap,
            ExponentialMap_UpwindAvg,
            PlanarProjection
        }
        public UVModes UVMode = UVModes.ExponentialMap_UpwindAvg;


        class GraphNode : DynamicPriorityQueueNode, IEquatable<GraphNode>
        {
            public int id;
            public GraphNode parent;
            public float graph_distance;
            public Vector2f uv;
            public bool frozen;

            public bool Equals(GraphNode other)
            {
                return id == other.id;
            }
        }

        DynamicPriorityQueue<GraphNode> SparseQueue;
        SparseObjectList<GraphNode> SparseNodes;
        MemoryPool<GraphNode> SparseNodePool;

        Func<int, Vector3f> PositionF;
        Func<int, Vector3f> NormalF;
        Func<int, IEnumerable<int>> NeighboursF;

        // maybe should be sparse array?
        Frame3f SeedFrame;
        float max_graph_distance;
        float max_uv_distance;


        public MeshLocalParam(int nMaxID,
            Func<int, Vector3f> nodePositionF,
            Func<int, Vector3f> nodeNormalF,
            Func<int, IEnumerable<int>> neighboursF)
        {
            PositionF = nodePositionF;
            NormalF = nodeNormalF;
            NeighboursF = neighboursF;

            SparseQueue = new DynamicPriorityQueue<GraphNode>();
            SparseNodes = new SparseObjectList<GraphNode>(nMaxID, 0);
            SparseNodePool = new MemoryPool<GraphNode>();
            max_graph_distance = float.MinValue;
            max_uv_distance = float.MinValue;
        }


        public void Reset()
        {
            SparseQueue.Clear(false);
            SparseNodes.Clear();
            SparseNodePool.ReturnAll();
            max_graph_distance = float.MinValue;
        }



        /// <summary>
        /// Compute distances that are less/equal to fMaxDistance from the seeds
        /// Terminates early, so Queue may not be empty
        /// </summary>
        public void ComputeToMaxDistance(Frame3f seedFrame, Index3i seedNbrs, float fMaxGraphDistance)
        {
            SeedFrame = seedFrame;

            for ( int j = 0; j < 3; ++j ) {
                int vid = seedNbrs[j];
                GraphNode g = get_node(vid);
                g.uv = compute_local_uv(ref SeedFrame, PositionF(vid));
                g.graph_distance = g.uv.Length;
                g.frozen = true;
                Debug.Assert(SparseQueue.Contains(g) == false);
                SparseQueue.Enqueue(g, g.graph_distance);
            }

            while (SparseQueue.Count > 0) {
                GraphNode g = SparseQueue.Dequeue();
                max_graph_distance = Math.Max(g.graph_distance, max_graph_distance);
                if ( max_graph_distance > fMaxGraphDistance )
                    return;

                if (g.parent != null) {
                    switch (UVMode) {
                        case UVModes.ExponentialMap:
                            update_uv_expmap(g);
                            break;
                        case UVModes.ExponentialMap_UpwindAvg:
                            update_uv_upwind_expmap(g);
                            break;
                        case UVModes.PlanarProjection:
                            update_uv_planar(g);
                            break;
                    }
                }

                float uv_dist_sqr = g.uv.LengthSquared;
                if (uv_dist_sqr > max_uv_distance)
                    max_uv_distance = uv_dist_sqr;

                g.frozen = true;
                update_neighbours_sparse(g);
            }

            max_uv_distance = (float)Math.Sqrt(max_uv_distance);
        }



        public void TransformUV(float fScale, Vector2f vTranslate)
        {
            foreach ( var pair in SparseNodes.NonZeroValues() ) {
                GraphNode g = pair.Value;
                if (g.frozen)
                    g.uv = (g.uv * fScale) + vTranslate;
            }
        }



        /// <summary>
        /// Get the maximum distance encountered during the Compute()
        /// </summary>
        public float MaxGraphDistance {
            get { return max_graph_distance; }
        }

        public float MaxUVDistance {
            get { return max_uv_distance; }
        }

        /// <summary>
        /// Get the computed uv at node id
        /// </summary>
        public Vector2f GetUV(int id)
        {
            GraphNode g = SparseNodes[id];
            if (g == null)
                return InvalidUV;
            return g.uv;
        }



        public void ApplyUVs(Action<int,Vector2f> applyF)
        {
            foreach (var pair in SparseNodes.NonZeroValues()) {
                GraphNode g = pair.Value;
                if (g.frozen)
                    applyF(g.id, g.uv);
            }
        }




        Vector2f compute_local_uv(ref Frame3f f, Vector3f pos)
        {
            pos -= f.Origin;
            Vector2f uv = new Vector2f(pos.Dot(f.X), pos.Dot(f.Y));
            return uv;
        }



        Vector2f propagate_uv(Vector3f pos, Vector2f nbrUV, ref Frame3f fNbr, ref Frame3f fSeed)
        {
            Vector2f local_uv = compute_local_uv(ref fNbr, pos);

            Frame3f fSeedToLocal = fSeed;
            fSeedToLocal.AlignAxis(2, fNbr.Z);

            Vector3f vAlignedSeedX = fSeedToLocal.X;
            Vector3f vLocalX = fNbr.X;

            float fCosTheta = vLocalX.Dot(vAlignedSeedX);

            // compute rotated min-dist vector for this particle
            float fTmp = 1 - fCosTheta * fCosTheta;
            if (fTmp < 0)
                fTmp = 0;     // need to clamp so that sqrt works...
            float fSinTheta = (float)Math.Sqrt(fTmp);
            Vector3f vCross = vLocalX.Cross(vAlignedSeedX);
            if (vCross.Dot(fNbr.Z) < 0)    // get the right sign...
                fSinTheta = -fSinTheta;


            Matrix2f mFrameRotate = new Matrix2f(fCosTheta, fSinTheta, -fSinTheta, fCosTheta);

            return nbrUV + mFrameRotate * local_uv;
        }


        void update_uv_expmap(GraphNode node)
        {
            int vid = node.id;
            Util.gDevAssert(node.parent != null && node.parent.frozen == true);
            int parent_id = node.parent.id;

            Vector3f parentPos = PositionF(parent_id);
            Frame3f parentFrame = new Frame3f(parentPos, NormalF(parent_id));
            node.uv = propagate_uv(PositionF(vid), node.parent.uv, ref parentFrame, ref SeedFrame);
        }



        void update_uv_upwind_expmap(GraphNode node)
        {
            int vid = node.id;
            Vector3f pos = PositionF(vid);

            Vector2f avg_uv = Vector2f.Zero;
            float fWeightSum = 0;
            int nbr_count = 0;
            foreach ( var nbr_id in NeighboursF(node.id) ) {
                GraphNode nbr_node = get_node(nbr_id, false);
                if ( nbr_node.frozen ) {
                    Vector3f nbr_pos = PositionF(nbr_id);
                    Frame3f nbr_frame = new Frame3f(nbr_pos, NormalF(nbr_id));
                    Vector2f nbr_uv = propagate_uv(pos, nbr_node.uv, ref nbr_frame, ref SeedFrame);
                    float fWeight = 1.0f / (pos.DistanceSquared(nbr_pos) + MathUtil.ZeroTolerancef);
                    avg_uv += fWeight * nbr_uv;
                    fWeightSum += fWeight;
                    nbr_count++;
                }
            }
            Util.gDevAssert(nbr_count > 0);

            //avg_uv /= (float)nbr_count;
            avg_uv /= fWeightSum;
            node.uv = avg_uv;
        }



        void update_uv_planar(GraphNode g)
        {
            g.uv = compute_local_uv(ref SeedFrame, PositionF(g.id));
        }



        GraphNode get_node(int id, bool bCreateIfMissing = true)
        {
            GraphNode g = SparseNodes[id];
            if (g == null) {
                g = SparseNodePool.Allocate();
                g.id = id; g.parent = null; g.frozen = false;
                g.uv = Vector2f.Zero;
                g.graph_distance = float.MaxValue;
                SparseNodes[id] = g;
            }
            return g;
        }



        void update_neighbours_sparse(GraphNode parent)
        {
            Vector3f parentPos = PositionF(parent.id);
            float parentDist = parent.graph_distance;

            foreach (int nbr_id in NeighboursF(parent.id)) {
                GraphNode nbr = get_node(nbr_id);
                if (nbr.frozen)
                    continue;

                float nbr_dist = parentDist + parentPos.Distance(PositionF(nbr_id));
                if (SparseQueue.Contains(nbr)) {
                    if (nbr_dist < nbr.priority) {
                        nbr.parent = parent;
                        nbr.graph_distance = nbr_dist;
                        SparseQueue.Update(nbr, nbr.graph_distance);
                    }
                } else {
                    nbr.parent = parent;
                    nbr.graph_distance = nbr_dist;
                    SparseQueue.Enqueue(nbr, nbr.graph_distance);
                }
            }
        }







    }
}
