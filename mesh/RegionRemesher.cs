using System;
using System.Collections.Generic;


namespace g3
{
    public class RegionRemesher : Remesher
    {
        public DMesh3 BaseMesh;
        public DSubmesh3 Region;

        // By default is initialized w/ all boundary constraints
        // You can add more, but don't screw up!
        MeshConstraints bdry_constraints;

        int[] cur_base_tris;


        public RegionRemesher(DMesh3 mesh, int[] regionTris)
        {
            BaseMesh = mesh;
            Region = new DSubmesh3(mesh, regionTris);
            Region.ComputeBoundaryInfo(regionTris);
            base.mesh = Region.SubMesh;

            cur_base_tris = (int[])regionTris.Clone();

            // add boundary-edge constraints
            // [TODO] actually only want to constrain submesh border edges...
            bdry_constraints = new MeshConstraints();
            MeshConstraintUtil.FixAllBoundaryEdges(bdry_constraints, Region.SubMesh);
            SetExternalConstraints(bdry_constraints);
        }



        public void BackPropropagate()
        {
            // remove existing submesh triangles
            MeshEditor editor = new MeshEditor(BaseMesh);
            editor.RemoveTriangles(cur_base_tris, true);

            // insert new submesh
            int[] new_tris = new int[Region.SubMesh.TriangleCount];
            IndexMap mapV;
            editor.ReinsertSubmesh(Region, ref new_tris, out mapV);
            cur_base_tris = new_tris;
        }



    }
}
