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
        Frame3d SeedFrame;
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
        public void ComputeToMaxDistance(Frame3d seedFrame, Index3i seedNbrs, float fMaxGraphDistance)
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




        Vector2f compute_local_uv(ref Frame3d f, Vector3f pos)
        {
            pos -= (Vector3f)f.Origin;
            Vector2f uv = new Vector2f(pos.Dot((Vector3f)f.X), pos.Dot((Vector3f)f.Y));
            return uv;
        }



        Vector2f propagate_uv(Vector3f pos, Vector2f nbrUV, ref Frame3d fNbr, ref Frame3d fSeed)
        {
            Vector2f local_uv = compute_local_uv(ref fNbr, pos);

            Frame3d fSeedToLocal = fSeed;
            fSeedToLocal.AlignAxis(2, fNbr.Z);

            Vector3d vAlignedSeedX = fSeedToLocal.X;
            Vector3d vLocalX = fNbr.X;

            double fCosTheta = vLocalX.Dot(vAlignedSeedX);

            // compute rotated min-dist vector for this particle
            double fTmp = 1 - fCosTheta * fCosTheta;
            if (fTmp < 0)
                fTmp = 0;     // need to clamp so that sqrt works...
            double fSinTheta = (float)Math.Sqrt(fTmp);
            Vector3d vCross = vLocalX.Cross(vAlignedSeedX);
            if (vCross.Dot(fNbr.Z) < 0)    // get the right sign...
                fSinTheta = -fSinTheta;


            Matrix2d mFrameRotate = new Matrix2d(fCosTheta, fSinTheta, -fSinTheta, fCosTheta);

            return (Vector2f)(nbrUV + mFrameRotate * local_uv);
        }


        void update_uv_expmap(GraphNode node)
        {
            int vid = node.id;
            Util.gDevAssert(node.parent != null && node.parent.frozen == true);
            int parent_id = node.parent.id;

            Vector3f parentPos = PositionF(parent_id);
            Frame3d parentFrame = new Frame3d(parentPos, NormalF(parent_id));
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
                    Frame3d nbr_frame = new Frame3d(nbr_pos, NormalF(nbr_id));
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
