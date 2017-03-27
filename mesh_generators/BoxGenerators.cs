using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{

    public static class gIndices
    {
        // Corner vertices of box faces  -  see Box.Corner for points associated w/ indexing
        // Note that 
        public static readonly int[,] BoxFaces = new int[6, 4] {
            { 1, 0, 3, 2 },     // back, -z
            { 4, 5, 6, 7 },     // front, +z
            { 0, 4, 7, 3 },     // left, -x
            { 5, 1, 2, 6 },     // right, +x,
            { 0, 1, 5, 4 },     // bottom, -y
            { 7, 6, 2, 3 }      // top, +y
        };

        // Box Face normal. Use Sign(BoxFaceNormals[i]) * Box.Axis( Abs(BoxFaceNormals[i])-1 )
        //  (+1 is so we can have a sign on X)
        public static int[] BoxFaceNormals = new int[6] { -3, 3, -1, 1, -2, 2 }; 
    }


    public class TrivialBox3Generator : MeshGenerator
    {
        public Box3d Box = Box3d.UnitZeroCentered;
        public bool NoSharedVertices = false;

        public override void Generate()
        {
            vertices = new VectorArray3d((NoSharedVertices) ? (4 * 6) : 8);
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);
            triangles = new IndexArray3i(2 * 6);

            if ( NoSharedVertices == false ) {
                for (int i = 0; i < 8; ++i) {
                    vertices[i] = Box.Corner(i);
                    normals[i] = (Vector3f) (vertices[i] - Box.Center[i]).Normalized;
                    uv[i] = Vector2f.Zero;      // what to do for UVs in this case ?!?
                }
                int ti = 0;
                for ( int fi = 0; fi < 6; ++fi ) {
                    triangles.Set(ti++,
                        gIndices.BoxFaces[fi, 0], gIndices.BoxFaces[fi, 1], gIndices.BoxFaces[fi, 2], Clockwise);
                    triangles.Set(ti++,
                        gIndices.BoxFaces[fi, 0], gIndices.BoxFaces[fi, 2], gIndices.BoxFaces[fi, 3], Clockwise);
                }
            } else {
                int ti = 0;
                int vi = 0;
                Vector2f[] square_uv = new Vector2f[4] { Vector2f.Zero, new Vector2f(1, 0), new Vector2f(1, 1), new Vector2f(0, 1) };
                for ( int fi = 0; fi < 6; ++fi ) {
                    int v0 = vi++; vi += 3;
                    int ni = gIndices.BoxFaceNormals[fi];
                    Vector3f n = (Vector3f)(Math.Sign(ni) * Box.Axis(Math.Abs(ni) - 1));
                    for ( int j = 0; j < 4; ++j ) {
                        vertices[v0 + j] = Box.Corner(gIndices.BoxFaces[fi, j]);
                        normals[v0 + j] = n;
                        uv[v0 + j] = square_uv[j];
                    }

                    triangles.Set(ti++, v0, v0 + 1, v0 + 2, Clockwise);
                    triangles.Set(ti++, v0, v0 + 2, v0 + 3, Clockwise);
                }
            }
        }
    }
}
