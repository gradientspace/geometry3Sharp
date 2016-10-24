using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public interface IMesh
    {
        int VertexCount { get; }
        int TriangleCount { get; }

        bool HasVertexColors { get; }
        bool HasVertexNormals { get; }

        Vector3d GetVertex(int i);
        Vector3d GetVertexNormal(int i);
        Vector3d GetVertexColor(int i);


        bool HasTriangleGroups { get; }

        Vector3i GetTriangle(int i);
        int GetTriangleGroup(int i);

    }


    /*
     * Abstracts construction of meshes, so that we can construct different types, etc
     */
    public interface IMeshBuilder
    {
        // return ID of new mesh
        int AppendNewMesh();
        void SetActiveMesh(int id);

        int AppendVertex(double x, double y, double z);
        int AppendVertexN(double x, double y, double z, float nx, float ny, float nz);
        int AppendVertexC(double x, double y, double z, float r, float g, float b);
        int AppendVertexNC(double x, double y, double z, float nx, float ny, float nz, float r, float g, float b);

        int AppendTriangle(int i, int j, int k);
        int AppendTriangle(int i, int j, int k, int g);


        // material handling

        // return client-side unique ID of material
        int BuildMaterial(GenericMaterial m);

        // do material assignment to mesh, where meshID comes from IMeshBuilder
        void AssignMaterial(int materialID, int meshID);
    }




}
