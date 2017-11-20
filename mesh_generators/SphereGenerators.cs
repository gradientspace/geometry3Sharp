using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{


    /// <summary>
    /// Generate a mesh of a sphere by first generating a mesh of a cube, 
    /// and then normalizing the vertices and moving them to sphere of desired radius.
    /// </summary>
    public class Sphere3Generator_NormalizedCube : GridBox3Generator
    {
        public double Radius = 1.0;

        public override MeshGenerator Generate()
        {
            base.Generate();
            for ( int i = 0; i < vertices.Count; ++i ) {
                Vector3d v = vertices[i] - Box.Center;
                v.Normalize();
                vertices[i] = Box.Center + Radius*v;
                normals[i] = (Vector3f)v;
            }

            return this;
        }

    }






}
