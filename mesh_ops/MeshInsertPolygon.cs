using System;
using System.Collections.Generic;

namespace g3
{
    /// <summary>
    /// Insert Polygon into Mesh. Assumption is that Mesh has 3D coordinates (u,v,0).
    /// This is basically a helper/wrapper around MeshInsertUVPolyCurve.
    /// Inserted edge set is avaliable as .InsertedPolygonEdges, and
    /// triangles inside polygon as .InteriorTriangles
    /// </summary>
    public class MeshInsertPolygon
    {
        public DMesh3 Mesh;
        public GeneralPolygon2d Polygon;


        public bool SimplifyInsertion = true;

        public MeshInsertUVPolyCurve OuterInsert;
        public List<MeshInsertUVPolyCurve> HoleInserts;
        public HashSet<int> InsertedPolygonEdges;
        public MeshFaceSelection InteriorTriangles;

        public bool Insert()
        {
            OuterInsert = new MeshInsertUVPolyCurve(Mesh, Polygon.Outer);
            Util.gDevAssert(OuterInsert.Validate() == ValidationStatus.Ok);
            bool outerApplyOK = OuterInsert.Apply();
            if (outerApplyOK == false || OuterInsert.Loops.Count == 0)
                return false;
            if (SimplifyInsertion)
                OuterInsert.Simplify();

            HoleInserts = new List<MeshInsertUVPolyCurve>(Polygon.Holes.Count);
            for (int hi = 0; hi < Polygon.Holes.Count; ++hi) {
                MeshInsertUVPolyCurve insert = new MeshInsertUVPolyCurve(Mesh, Polygon.Holes[hi]);
                Util.gDevAssert(insert.Validate() == ValidationStatus.Ok);
                insert.Apply();
                if (SimplifyInsertion)
                    insert.Simplify();
                HoleInserts.Add(insert);
            }


            // find a triangle connected to loop that is inside the polygon
            //   [TODO] maybe we could be a bit more robust about this? at least
            //   check if triangle is too degenerate...
            int seed_tri = -1;
            EdgeLoop outer_loop = OuterInsert.Loops[0];
            for (int i = 0; i < outer_loop.EdgeCount; ++i) {
                if ( ! Mesh.IsEdge(outer_loop.Edges[i]) )
                    continue;

                Index2i et = Mesh.GetEdgeT(outer_loop.Edges[i]);
                Vector3d ca = Mesh.GetTriCentroid(et.a);
                bool in_a = Polygon.Outer.Contains(ca.xy);
                Vector3d cb = Mesh.GetTriCentroid(et.b);
                bool in_b = Polygon.Outer.Contains(cb.xy);
                if (in_a && in_b == false) {
                    seed_tri = et.a;
                    break;
                } else if (in_b && in_a == false) {
                    seed_tri = et.b;
                    break;
                }
            }
            if (seed_tri == -1)
                throw new Exception("MeshPolygonsInserter: could not find seed triangle!");

            // make list of all outer & hole edges
            InsertedPolygonEdges = new HashSet<int>(outer_loop.Edges);
            foreach (var insertion in HoleInserts) {
                foreach (int eid in insertion.Loops[0].Edges)
                    InsertedPolygonEdges.Add(eid);
            }

            // flood-fill inside loop from seed triangle
            InteriorTriangles = new MeshFaceSelection(Mesh);
            InteriorTriangles.FloodFill(seed_tri, null, (eid) => { return InsertedPolygonEdges.Contains(eid) == false; });

            return true;
        }

    }
}
