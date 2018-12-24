using System;
using System.Collections.Generic;

namespace g3
{



    /// <summary>
    /// Present mesh edge midpoints as a point set
    /// </summary>
    public class MeshEdgeMidpoints : IPointSet
    {
        public MeshEdgeMidpoints(DMesh3 mesh)
        {
            Mesh = mesh;
        }

        public DMesh3 Mesh;

        public int VertexCount { get { return Mesh.EdgeCount; } }
        public int MaxVertexID { get { return Mesh.MaxEdgeID; } }

        public bool HasVertexNormals { get { return false; } }
        public bool HasVertexColors { get { return false; } }

        public Vector3d GetVertex(int i) { return Mesh.GetEdgePoint(i, 0.5); }
        public Vector3f GetVertexNormal(int i) { return Vector3f.AxisY; }
        public Vector3f GetVertexColor(int i) { return Vector3f.One; }

        public bool IsVertex(int vID) { return Mesh.IsEdge(vID); }

        // iterators allow us to work with gaps in index space
        public IEnumerable<int> VertexIndices()
        {
            return Mesh.EdgeIndices();
        }

        /// <summary>
        /// Timestamp is incremented any time any change is made to the mesh
        /// </summary>
        public int Timestamp {
            get { return Mesh.Timestamp; }
        }
    }




    /// <summary>
    /// Present mesh boundary-edge midpoints as a point set
    /// </summary>
    public class MeshBoundaryEdgeMidpoints : IPointSet
    {
        public MeshBoundaryEdgeMidpoints(DMesh3 mesh)
        {
            Mesh = mesh;
            // [RMS] we could count boundary edges really fast by iterating by +4's in
            //  the mesh edge buffer... (also have to check that edge is valid though!)
            num_boundary_edges = 0;
            foreach (int eid in mesh.BoundaryEdgeIndices())
                num_boundary_edges++;
        }

        int num_boundary_edges;
        public DMesh3 Mesh;

        public int VertexCount { get { return num_boundary_edges; } }
        public int MaxVertexID { get { return Mesh.MaxEdgeID; } }

        public bool HasVertexNormals { get { return false; } }
        public bool HasVertexColors { get { return false; } }

        public Vector3d GetVertex(int i) { return Mesh.GetEdgePoint(i, 0.5); }
        public Vector3f GetVertexNormal(int i) { return Vector3f.AxisY; }
        public Vector3f GetVertexColor(int i) { return Vector3f.One; }

        public bool IsVertex(int vID) { return Mesh.IsEdge(vID) && Mesh.IsBoundaryEdge(vID); }

        // iterators allow us to work with gaps in index space
        public IEnumerable<int> VertexIndices()
        {
            return Mesh.BoundaryEdgeIndices();
        }

        /// <summary>
        /// Timestamp is incremented any time any change is made to the mesh
        /// </summary>
        public int Timestamp {
            get { return Mesh.Timestamp; }
        }
    }


}
