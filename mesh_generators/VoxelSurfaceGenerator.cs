using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class VoxelSurfaceGenerator
    {
        public IBinaryVoxelGrid Voxels;

        // if true, we do not add triangles that are "inside" outer surface of voxel faces
        public bool SkipInteriorFaces = true;

        // if false, we skip faces along outer boundary
        public bool CapAtBoundary = true;

        // "normal" meshes are counter-clockwise. Unity is CW though...
        public bool Clockwise = false;

        // result
        public DMesh3 Mesh;



        public void Generate()
        {
            Mesh = new DMesh3(MeshComponents.VertexNormals);

            AxisAlignedBox3i bounds = Voxels.GridBounds;
            bounds.Max -= Vector3i.One;

            int[] vertices = new int[4];

            foreach ( Vector3i nz in Voxels.NonZeros() ) {

                Box3d cube = Box3d.UnitZeroCentered;
                cube.Center = (Vector3d)nz;

                for ( int fi = 0; fi < 6; ++fi ) {

                    // checks dependent on neighbours
                    Index3i nbr = nz + gIndices.GridOffsets6[fi];
                    if (bounds.Contains(nbr)) {
                        if (SkipInteriorFaces && Voxels.Get(nbr))
                            continue;
                    } else if ( CapAtBoundary == false ) {
                        continue;
                    }


                    int ni = gIndices.BoxFaceNormals[fi];
                    Vector3f n = (Vector3f)(Math.Sign(ni) * cube.Axis(Math.Abs(ni) - 1));
                    NewVertexInfo vi = new NewVertexInfo(Vector3d.Zero, n);
                    for ( int j = 0; j < 4; ++j ) {
                        vi.v = cube.Corner(gIndices.BoxFaces[fi, j]);
                        vertices[j] = Mesh.AppendVertex(vi);
                    }

                    Index3i t0 = new Index3i(vertices[0], vertices[1], vertices[2], Clockwise);
                    Index3i t1 = new Index3i(vertices[0], vertices[2], vertices[3], Clockwise);
                    Mesh.AppendTriangle(t0);
                    Mesh.AppendTriangle(t1);
                }

            }

        }
    }
}
