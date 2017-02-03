using System;
using System.Collections.Generic;


namespace g3
{
    public class RegionRemesher : Remesher
    {
        public DMesh3 BaseMesh;
        public DSubmesh3 Region;

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
            MeshConstraints cons = new MeshConstraints();
            MeshConstraintUtil.FixAllBoundaryEdges(cons, Region.SubMesh);
            SetExternalConstraints(cons);
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
