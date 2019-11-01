using System.Collections.Generic;

namespace g3
{
    public class GraphTubeMesher
    {
        public DGraph3 Graph;
        public HashSet<int> TipVertices;
        public HashSet<int> GroundVertices;

        public double PostRadius = 1.25;
        public double TipRadius = 0.5;
        public double GroundRadius = 3.25;

        public double SamplerCellSizeHint = 0.0;
        public double ActualCellSize = 0;


        /// <summary>
        /// Set this to be able to cancel running remesher
        /// </summary>
        public ProgressCancel Progress = null;

        /// <summary>
        /// if this returns true, abort computation. 
        /// </summary>
        protected virtual bool Cancelled()
        {
            return (Progress == null) ? false : Progress.Cancelled();
        }


        /*
         *  Outputs
         */
        public DMesh3 ResultMesh;


        public GraphTubeMesher(DGraph3 graph)
        {
            Graph = graph;
        }


        public GraphTubeMesher(GraphSupportGenerator support_gen)
        {
            Graph = support_gen.Graph;
            TipVertices = support_gen.TipVertices;
            GroundVertices = support_gen.GroundVertices;
            SamplerCellSizeHint = support_gen.CellSize;
        }


        public virtual void Generate()
        {
            AxisAlignedBox3d graphBox = Graph.CachedBounds;
            graphBox.Expand(2 * PostRadius);

            double cellSize = (SamplerCellSizeHint == 0) ? (PostRadius / 5) : SamplerCellSizeHint;
            ImplicitFieldSampler3d sampler = new ImplicitFieldSampler3d(graphBox, cellSize);
            ActualCellSize = cellSize;

            // sample segments into graph
            ImplicitLine3d line = new ImplicitLine3d() { Radius = PostRadius };
            foreach (int eid in Graph.EdgeIndices()) {
                Index2i ev = Graph.GetEdgeV(eid);
                Vector3d v0 = Graph.GetVertex(ev.a);
                Vector3d v1 = Graph.GetVertex(ev.b);
                double r = PostRadius;

                int upper_vid = (v0.y > v1.y) ? ev.a : ev.b;
                if (TipVertices.Contains(upper_vid))
                    r = TipRadius;

                line.Segment = new Segment3d(v0, v1);
                line.Radius = r;
                sampler.Sample(line, line.Radius / 2);
            }

            foreach (int vid in GroundVertices) {
                Vector3d v = Graph.GetVertex(vid);
                sampler.Sample(new ImplicitSphere3d() { Origin = v - (PostRadius / 2) * Vector3d.AxisY, Radius = GroundRadius });
            }


            ImplicitHalfSpace3d cutPlane = new ImplicitHalfSpace3d() {
                Origin = Vector3d.Zero, Normal = Vector3d.AxisY
            };
            ImplicitDifference3d cut = new ImplicitDifference3d() { A = sampler.ToImplicit(), B = cutPlane };

            MarchingCubes mc = new MarchingCubes() { Implicit = cut, Bounds = graphBox, CubeSize = PostRadius / 3 };
            mc.Bounds.Min.y = -2 * mc.CubeSize;
            mc.Bounds.Min.x -= 2 * mc.CubeSize; mc.Bounds.Min.z -= 2 * mc.CubeSize;
            mc.Bounds.Max.x += 2 * mc.CubeSize; mc.Bounds.Max.z += 2 * mc.CubeSize;
            mc.CancelF = this.Cancelled;
            mc.Generate();

            ResultMesh = mc.Mesh;
        }



    }
}
