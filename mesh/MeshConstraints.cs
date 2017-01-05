using System;
using System.Collections.Generic;
using System.Linq;

namespace g3
{

    [Flags]
    public enum EdgeRefineFlags
    {
        NoConstraint = 0,
        NoFlip = 1,
        NoSplit = 2,
        NoCollapse = 4,
        FullyConstrained = 7
    }


    public struct EdgeConstraint
    {
        EdgeRefineFlags refineFlags;


        public EdgeConstraint(EdgeRefineFlags rflags)
        {
            refineFlags = rflags;
        }

        public bool CanFlip {
            get { return (refineFlags & EdgeRefineFlags.NoFlip) == 0; }
        }
        public bool CanSplit {
            get { return (refineFlags & EdgeRefineFlags.NoSplit) == 0; }
        }
        public bool CanCollapse {
            get { return (refineFlags & EdgeRefineFlags.NoCollapse) == 0; }
        }
        public bool NoModifications {
            get { return (refineFlags & EdgeRefineFlags.FullyConstrained) == EdgeRefineFlags.FullyConstrained; }
        }

        static public readonly EdgeConstraint Unconstrained = new EdgeConstraint() { refineFlags = 0 };
    }



    public struct VertexConstraint
    {
        public bool Fixed;
        public int FixedSetID;      // in Remesher, we can allow two Fixed vertices with 
                                    // same FixedSetID to be collapsed together
        
        public VertexConstraint(bool isFixed, int setID = InvalidSetID)
        {
            Fixed = isFixed;
            FixedSetID = setID;
        }

        public const int InvalidSetID = -1;
        static public readonly VertexConstraint Unconstrained = new VertexConstraint() { Fixed = false };
     }




    public class MeshConstraints
    {

        Dictionary<int, EdgeConstraint> Edges = new Dictionary<int, EdgeConstraint>();

        public bool HasEdgeConstraint(int eid)
        {
            return Edges.ContainsKey(eid);
        }

        public EdgeConstraint GetEdgeConstraint(int eid)
        {
            EdgeConstraint ec;
            if (Edges.TryGetValue(eid, out ec))
                return ec;
            return EdgeConstraint.Unconstrained;
        }

        public void SetOrUpdateEdgeConstraint(int eid, EdgeConstraint ec)
        {
            Edges[eid] = ec;
        }

        public void ClearEdgeConstraint(int eid)
        {
            Edges.Remove(eid);
        }



        Dictionary<int, VertexConstraint> Vertices = new Dictionary<int, VertexConstraint>();

        public bool HasVertexConstraint(int vid)
        {
            return Vertices.ContainsKey(vid);
        }

        public VertexConstraint GetVertexConstraint(int vid)
        {
            VertexConstraint vc;
            if (Vertices.TryGetValue(vid, out vc))
                return vc;
            return VertexConstraint.Unconstrained;
        }

        public void SetOrUpdateVertexConstraint(int vid, VertexConstraint vc)
        {
            Vertices[vid] = vc;
        }

        public void ClearVertexConstraint(int vid)
        {
            Vertices.Remove(vid);
        }


    }
}
