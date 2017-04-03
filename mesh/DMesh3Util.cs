using System;
using System.Collections.Generic;


namespace g3
{
    public class DMesh3Builder : IMeshBuilder
    {
        public List<DMesh3> Meshes;
        public List<GenericMaterial> Materials;

		// this is a map from index into Meshes to index into Materials (-1 if no material)
		//  (so, currently we can only have 1 material per mesh!)
        public List<int> MaterialAssignment;

        public List<Dictionary<string, object> > Metadata;

        int nActiveMesh;

        public DMesh3Builder()
        {
            Meshes = new List<DMesh3>();
            Materials = new List<GenericMaterial>();
            MaterialAssignment = new List<int>();
            Metadata = new List<Dictionary<string, object>>();
            nActiveMesh = -1;
        }

        public int AppendNewMesh(bool bHaveVtxNormals, bool bHaveVtxColors, bool bHaveVtxUVs, bool bHaveFaceGroups)
        {
            int index = Meshes.Count;
			DMesh3 m = new DMesh3(bHaveVtxNormals, bHaveVtxColors, bHaveVtxUVs, bHaveFaceGroups);
            Meshes.Add(m);
            MaterialAssignment.Add(-1);     // no material is known
            Metadata.Add(new Dictionary<string, object>());
            nActiveMesh = index;
            return index;
        }

        public void SetActiveMesh(int id)
        {
            if (id >= 0 && id < Meshes.Count)
                nActiveMesh = id;
            else
                throw new ArgumentOutOfRangeException("active mesh id is out of range");
        }

        public int AppendTriangle(int i, int j, int k)
        {
            return Meshes[nActiveMesh].AppendTriangle(i, j, k);
        }

        public int AppendTriangle(int i, int j, int k, int g)
        {
            return Meshes[nActiveMesh].AppendTriangle(i, j, k, g);
        }

        public int AppendVertex(double x, double y, double z)
        {
            return Meshes[nActiveMesh].AppendVertex(new Vector3d(x, y, z));
        }
        public int AppendVertex(NewVertexInfo info)
        {
            return Meshes[nActiveMesh].AppendVertex(info);
        }

        public bool SupportsMetaData { get { return true; } }
        public void AppendMetaData(string identifier, object data)
        {
            Metadata[nActiveMesh].Add(identifier, data);
        }


        // just store GenericMaterial object, we can't use it here
        public int BuildMaterial(GenericMaterial m)
        {
            int id = Materials.Count;
            Materials.Add(m);
            return id;
        }

        // do material assignment to mesh
        public void AssignMaterial(int materialID, int meshID)
        {
            if (meshID >= MaterialAssignment.Count || materialID >= Materials.Count)
                throw new ArgumentOutOfRangeException("[SimpleMeshBuilder::AssignMaterial] meshID or materialID are out-of-range");
            MaterialAssignment[meshID] = materialID;
        }
    }





}
