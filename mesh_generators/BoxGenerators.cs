using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{
    /// <summary>
    /// Generate a minimal box
    /// </summary>
    public class TrivialBox3Generator : MeshGenerator
    {
        public Box3d Box = Box3d.UnitZeroCentered;
        public bool NoSharedVertices = false;

        public override MeshGenerator Generate()
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

            return this;
        }
    }









    /// <summary>
    /// Generate a mesh of a box that has "gridded" faces, ie grid of triangulated quads, 
    /// with EdgeVertices verts along each edge.
    /// [TODO] allow varying EdgeVertices in each dimension (tricky...)
    /// </summary>
    public class GridBox3Generator : MeshGenerator
    {
        public Box3d Box = Box3d.UnitZeroCentered;
        public int EdgeVertices = 8;
        public bool NoSharedVertices = false;

        public override MeshGenerator Generate()
        {
            int N = (EdgeVertices > 1) ? EdgeVertices : 2;
            int Nm2 = N - 2;
            int NT = N - 1;
            int N2 = N * N;
            vertices = new VectorArray3d((NoSharedVertices) ? (N2 * 6) : (8 + Nm2*12 + Nm2*Nm2*6));
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);
            triangles = new IndexArray3i(2 * NT * NT * 6 );
            groups = new int[triangles.Count];

            Vector3d[] boxvertices = Box.ComputeVertices();

            int vi = 0;
            int ti = 0;
            if (NoSharedVertices) {
                for (int fi = 0; fi < 6; ++fi) {
                    // get corner vertices
                    Vector3d v00 = boxvertices[gIndices.BoxFaces[fi, 0]];
                    Vector3d v01 = boxvertices[gIndices.BoxFaces[fi, 1]];
                    Vector3d v11 = boxvertices[gIndices.BoxFaces[fi, 2]];
                    Vector3d v10 = boxvertices[gIndices.BoxFaces[fi, 3]];
                    Vector3f faceN = Math.Sign(gIndices.BoxFaceNormals[fi]) * (Vector3f)Box.Axis(Math.Abs(gIndices.BoxFaceNormals[fi]) - 1);

                    // add vertex rows
                    int start_vi = vi;
                    for (int yi = 0; yi < N; ++yi) {
                        double ty = (double)yi / (double)(N - 1);
                        for (int xi = 0; xi < N; ++xi) {
                            double tx = (double)xi / (double)(N - 1);
                            normals[vi] = faceN;
                            uv[vi] = new Vector2f(tx, ty);
                            vertices[vi++] = bilerp(ref v00, ref v01, ref v11, ref v10, tx, ty);
                        }
                    }

                    // add faces
                    for (int y0 = 0; y0 < NT; ++y0) {
                        for (int x0 = 0; x0 < NT; ++x0) {
                            int i00 = start_vi + y0 * N + x0;
                            int i10 = start_vi + (y0+1) * N + x0;
                            int i01 = i00 + 1, i11 = i10 + 1;

                            groups[ti] = fi;
                            triangles.Set(ti++, i00, i01, i11, Clockwise);
                            groups[ti] = fi;
                            triangles.Set(ti++, i00, i11, i10, Clockwise);
                        }
                    }
                }

            } else {
                // construct integer coordinates
                Vector3i[] intvertices = new Vector3i[boxvertices.Length];
                for ( int k = 0; k < boxvertices.Length; ++k ) {
                    Vector3d v = boxvertices[k] - Box.Center;
                    intvertices[k] = new Vector3i(
                        v.x < 0 ? 0 : N - 1,
                        v.y < 0 ? 0 : N - 1,
                        v.z < 0 ? 0 : N - 1);
                }
                int[] faceIndicesV = new int[N2];

                // add edge vertices and store in this map
                // todo: don't use a map (?)  how do we do that, though...
                //   - each index is in range [0,N). If we have (i,j,k), then for a given
                //     i, we have a finite number of j and k (< 2N?).
                //     make N array of 2N length, key on i, linear search for matching j/k?
                Dictionary<Vector3i, int> edgeVerts = new Dictionary<Vector3i, int>();
                for (int fi = 0; fi < 6; ++fi) {
                    // get corner vertices
                    int c00 = gIndices.BoxFaces[fi, 0], c01 = gIndices.BoxFaces[fi, 1],
                        c11 = gIndices.BoxFaces[fi, 2], c10 = gIndices.BoxFaces[fi, 3];
                    Vector3d v00 = boxvertices[c00];  Vector3i vi00 = intvertices[c00];
                    Vector3d v01 = boxvertices[c01];  Vector3i vi01 = intvertices[c01];
                    Vector3d v11 = boxvertices[c11];  Vector3i vi11 = intvertices[c11];
                    Vector3d v10 = boxvertices[c10];  Vector3i vi10 = intvertices[c10];

                    Action<Vector3d, Vector3d, Vector3i, Vector3i> do_edge = (a, b, ai, bi) => {
                        for (int i = 0; i < N; ++i) {
                            double t = (double)i / (double)(N - 1);
                            Vector3i vidx = lerp(ref ai, ref bi, t);
                            if (edgeVerts.ContainsKey(vidx) == false) {
                                Vector3d v = Vector3d.Lerp(ref a, ref b, t);
                                normals[vi] = (Vector3f)v.Normalized;
                                uv[vi] = Vector2f.Zero;
                                edgeVerts[vidx] = vi;
                                vertices[vi++] = v;
                            }
                        }
                    };
                    do_edge(v00, v01, vi00, vi01);
                    do_edge(v01, v11, vi01, vi11);
                    do_edge(v11, v10, vi11, vi10);
                    do_edge(v10, v00, vi10, vi00);
                }


                // now generate faces
                for (int fi = 0; fi < 6; ++fi) {
                    // get corner vertices
                    int c00 = gIndices.BoxFaces[fi, 0], c01 = gIndices.BoxFaces[fi, 1],
                        c11 = gIndices.BoxFaces[fi, 2], c10 = gIndices.BoxFaces[fi, 3];
                    Vector3d v00 = boxvertices[c00]; Vector3i vi00 = intvertices[c00];
                    Vector3d v01 = boxvertices[c01]; Vector3i vi01 = intvertices[c01];
                    Vector3d v11 = boxvertices[c11]; Vector3i vi11 = intvertices[c11];
                    Vector3d v10 = boxvertices[c10]; Vector3i vi10 = intvertices[c10];
                    Vector3f faceN = Math.Sign(gIndices.BoxFaceNormals[fi]) * (Vector3f)Box.Axis(Math.Abs(gIndices.BoxFaceNormals[fi]) - 1);

                    // add vertex rows, using existing vertices if we have them in map
                    for (int yi = 0; yi < N; ++yi) {
                        double ty = (double)yi / (double)(N - 1);
                        for (int xi = 0; xi < N; ++xi) {
                            double tx = (double)xi / (double)(N - 1);
                            Vector3i vidx = bilerp(ref vi00, ref vi01, ref vi11, ref vi10, tx, ty);
                            int use_vi;
                            if ( edgeVerts.TryGetValue(vidx, out use_vi) == false ) { 
                                Vector3d v = bilerp(ref v00, ref v01, ref v11, ref v10, tx, ty);
                                use_vi = vi++;
                                normals[use_vi] = faceN;
                                uv[use_vi] = new Vector2f(tx, ty);
                                vertices[use_vi] = v;
                            } 
                            faceIndicesV[yi * N + xi] = use_vi;
                        }
                    }

                    // add faces
                    for (int y0 = 0; y0 < NT; ++y0) {
                        int y1 = y0 + 1;
                        for (int x0 = 0; x0 < NT; ++x0) {
                            int x1 = x0 + 1;
                            int i00 = faceIndicesV[y0 * N + x0];
                            int i01 = faceIndicesV[y0 * N + x1];
                            int i11 = faceIndicesV[y1 * N + x1];
                            int i10 = faceIndicesV[y1 * N + x0];

                            groups[ti] = fi;
                            triangles.Set(ti++, i00, i01, i11, Clockwise);
                            groups[ti] = fi;
                            triangles.Set(ti++, i00, i11, i10, Clockwise);
                        }
                    }
                }


            }

            return this;
        }



    }


}
