using System;
using System.Collections.Generic;

namespace g3
{
    public class IndexUtil
    {
        // test if [a0,a1] and [b0,b1] are the same pair, ignoring order
        public static bool same_pair_unordered(int a0, int a1, int b0, int b1)
        {
            return (a0 == b0) ?
                (a1 == b1) :
                (a0 == b1 && a1 == b0);
        }

        // find the vtx that is the same in both ev0 and ev1
        public static int find_shared_edge_v(ref Index2i ev0, ref Index2i ev1)
        {
            if (ev0.a == ev1.a)             return ev0.a;
            else if (ev0.a == ev1.b)        return ev0.a;
            else if (ev0.b == ev1.a)        return ev0.b;
            else if (ev0.b == ev1.b)        return ev0.b;
            else return DMesh3.InvalidID;
        }


        // find the vtx that is the same in both ev0 and ev1
        public static int find_edge_other_v(ref Index2i ev, int v)
        {
            if (ev.a == v)          return ev.b;
            else if (ev.b == v)     return ev.a;
            else                    return DMesh3.InvalidID;
        }
        public static int find_edge_other_v(Index2i ev, int v)
        {
            if (ev.a == v)          return ev.b;
            else if (ev.b == v)     return ev.a;
            else                    return DMesh3.InvalidID;
        }


        // return index of a in tri_verts, or InvalidID if not found
        public static int find_tri_index(int a, int[] tri_verts)
        {
            if (tri_verts[0] == a) return 0;
            if (tri_verts[1] == a) return 1;
            if (tri_verts[2] == a) return 2;
            return DMesh3.InvalidID;
        }
		public static int find_tri_index(int a, Index3i tri_verts)
		{
			if (tri_verts.a == a) return 0;
			if (tri_verts.b == a) return 1;
			if (tri_verts.c == a) return 2;
			return DMesh3.InvalidID;
		}
        public static int find_tri_index(int a, ref Index3i tri_verts)
        {
            if (tri_verts.a == a) return 0;
            if (tri_verts.b == a) return 1;
            if (tri_verts.c == a) return 2;
            return DMesh3.InvalidID;
        }

        // return index of a in tri_verts, or InvalidID if not found
        public static int find_edge_index_in_tri(int a, int b, int[] tri_verts )
        {
            if (same_pair_unordered(a, b, tri_verts[0], tri_verts[1])) return 0;
            if (same_pair_unordered(a, b, tri_verts[1], tri_verts[2])) return 1;
            if (same_pair_unordered(a, b, tri_verts[2], tri_verts[0])) return 2;
            return DMesh3.InvalidID;
        }
        public static int find_edge_index_in_tri(int a, int b, ref Index3i tri_verts )
        {
            if (same_pair_unordered(a, b, tri_verts.a, tri_verts.b)) return 0;
            if (same_pair_unordered(a, b, tri_verts.b, tri_verts.c)) return 1;
            if (same_pair_unordered(a, b, tri_verts.c, tri_verts.a)) return 2;
            return DMesh3.InvalidID;
        }

        // find sequence [a,b] in tri_verts (mod3) and return index of a, or InvalidID if not found
        public static int find_tri_ordered_edge(int a, int b, int[] tri_verts)
        {
            if (tri_verts[0] == a && tri_verts[1] == b) return 0;
            if (tri_verts[1] == a && tri_verts[2] == b) return 1;
            if (tri_verts[2] == a && tri_verts[0] == b) return 2;
            return DMesh3.InvalidID;
        }

        /// <summary>
        ///  find sequence [a,b] in tri_verts (mod3) and return index of a, or InvalidID if not found
        /// </summary>
        public static int find_tri_ordered_edge(int a, int b, ref Index3i tri_verts)
        {
            if (tri_verts.a == a && tri_verts.b == b) return 0;
            if (tri_verts.b == a && tri_verts.c == b) return 1;
            if (tri_verts.c == a && tri_verts.a == b) return 2;
            return DMesh3.InvalidID;
        }
        public static int find_tri_ordered_edge(int a, int b, Index3i tri_verts)
        {
            return find_tri_ordered_edge(a, b, ref tri_verts);
        }

        // find sequence [a,b] in tri_verts (mod3) then return the third **value**, or InvalidID if not found
        public static int find_tri_other_vtx(int a, int b, int[] tri_verts)
        {
            for (int j = 0; j < 3; ++j) {
                if (same_pair_unordered(a, b, tri_verts[j], tri_verts[(j + 1) % 3]))
                    return tri_verts[(j + 2) % 3];
            }
            return DMesh3.InvalidID;
        }
		public static int find_tri_other_vtx(int a, int b, Index3i tri_verts)
		{
			for (int j = 0; j < 3; ++j) {
				if (same_pair_unordered(a, b, tri_verts[j], tri_verts[(j + 1) % 3]))
					return tri_verts[(j + 2) % 3];
			}
			return DMesh3.InvalidID;
		}
		public static int find_tri_other_vtx(int a, int b, DVector<int> tri_array, int ti)
		{
			int i = 3*ti;
			for (int j = 0; j < 3; ++j) {
				if (same_pair_unordered(a, b, tri_array[i+j], tri_array[i + ((j + 1) % 3)]))
					return tri_array[i + ((j + 2) % 3)];
			}
			return DMesh3.InvalidID;
		}


        /// <summary>
        /// assuming a is in tri-verts, returns other two vertices, in correct order (or Index2i.Max if not found)
        /// </summary>
        public static Index2i find_tri_other_verts(int a, ref Index3i tri_verts)
        {
            if (tri_verts.a == a)
                return new Index2i(tri_verts.b, tri_verts.c);
            else if (tri_verts.b == a)
                return new Index2i(tri_verts.c, tri_verts.a);
            else if (tri_verts.c == a)
                return new Index2i(tri_verts.a, tri_verts.b);
            return Index2i.Max;
        }

        // find sequence [a,b] in tri_verts (mod3) then return the third **index**, or InvalidID if not found
        public static int find_tri_other_index(int a, int b, int[] tri_verts)
        {
            for (int j = 0; j < 3; ++j) {
                if (same_pair_unordered(a, b, tri_verts[j], tri_verts[(j + 1) % 3]))
                    return (j + 2) % 3;
            }
            return DMesh3.InvalidID;
        }


        // Set [a,b] to order found in tri_verts (mod3). return true if we swapped.
        // Assumes that a and b are in tri_verts, if not the result is garbage!
        public static bool orient_tri_edge(ref int a, ref int b, ref Index3i tri_verts)
		{
			if (a == tri_verts.a) {
				if (tri_verts.c == b) {
					int x = a; a = b; b = x;
					return true;
				}
			} else if (a == tri_verts.b) {
				if (tri_verts.a == b) {
					int x = a; a = b; b = x;
					return true;
				}
			} else if (a == tri_verts.c) {
				if (tri_verts.b == b) {
					int x = a; a = b; b = x;
					return true;
				}
			}
			return false;
		}
        public static bool orient_tri_edge(ref int a, ref int b, Index3i tri_verts) {
            return orient_tri_edge(ref a, ref b, ref tri_verts);
        }

        // set [a,b] to order found in tri_verts (mod3), and return third **value**, or InvalidID if not found
        public static int orient_tri_edge_and_find_other_vtx(ref int a, ref int b, int[] tri_verts)
        {
            for (int j = 0; j < 3; ++j) {
                if (same_pair_unordered(a, b, tri_verts[j], tri_verts[(j + 1) % 3])) {
                    a = tri_verts[j];
                    b = tri_verts[(j + 1) % 3];
                    return tri_verts[(j + 2) % 3];
                }
            }
            return DMesh3.InvalidID;
        }


		// set [a,b] to order found in tri_verts (mod3), and return third **value**, or InvalidID if not found
		public static int orient_tri_edge_and_find_other_vtx(ref int a, ref int b, Index3i tri_verts)
		{
			for (int j = 0; j < 3; ++j) {
				if (same_pair_unordered(a, b, tri_verts[j], tri_verts[(j + 1) % 3])) {
					a = tri_verts[j];
					b = tri_verts[(j + 1) % 3];
					return tri_verts[(j + 2) % 3];
				}
			}
			return DMesh3.InvalidID;
		}


        public static bool is_ordered(int a, int b, ref Index3i tri_verts)
        {
            return (tri_verts.a == a && tri_verts.b == b) ||
                   (tri_verts.b == a && tri_verts.c == b) ||
                   (tri_verts.c == a && tri_verts.a == b);
        }


        public static bool is_same_triangle(int a, int b, int c, ref Index3i tri)
        {
            if (tri.a == a)         return same_pair_unordered(tri.b, tri.c, b, c);
            else if ( tri.b == a )  return same_pair_unordered(tri.a, tri.c, b, c);
            else if ( tri.c == a )  return same_pair_unordered(tri.a, tri.b, b, c);
            return false;
        }


        public static void cycle_indices_minfirst(ref Index3i tri)
        {
            if (tri.b < tri.a && tri.b < tri.c) {
                int a = tri.a, b = tri.b, c = tri.c;
                tri.a = b;
                tri.b = c;
                tri.c = a;
            } else if (tri.c < tri.a && tri.c < tri.b) {
                int a = tri.a, b = tri.b, c = tri.c;
                tri.a = c;
                tri.b = a;
                tri.c = b;
            }
        }


        public static void sort_indices(ref Index3i tri)
        {
            // possibly this can be re-ordered to have fewer tests? ...
            if ( tri.a < tri.b && tri.a < tri.c ) {
                if (tri.b > tri.c) {
                    int b = tri.b; tri.b = tri.c; tri.c = b;
                }
            } else if ( tri.b < tri.a && tri.b < tri.c ) {
                if ( tri.a < tri.c ) {
                    int b = tri.b; tri.b = tri.a; tri.a = b;
                } else {
                    int a = tri.a, b = tri.b, c = tri.c;
                    tri.a = b; tri.b = c; tri.c = a;
                }
            } else if ( tri.c < tri.a && tri.c < tri.b ) {
                if ( tri.b < tri.a ) {
                    int c = tri.c; tri.c = tri.a; tri.a = c;
                } else {
                    int a = tri.a, b = tri.b, c = tri.c;
                    tri.a = c; tri.b = a; tri.c = b;
                }
            }
        }



        public static Vector3i ToGrid3Index(int idx, int nx, int ny)
        {
            int x = idx % nx;
            int y = (idx / nx) % ny;
            int z = idx / (nx * ny);
            return new Vector3i(x, y, z);
        }

        public static int ToGrid3Linear(int i, int j, int k, int nx, int ny) {
            return i + nx * (j + ny * k);
        }
        public static int ToGrid3Linear(Vector3i ijk, int nx, int ny) {
            return ijk.x + nx * (ijk.y + ny * ijk.z);
        }
        public static int ToGrid3Linear(ref Vector3i ijk, int nx, int ny) {
            return ijk.x + nx * (ijk.y + ny * ijk.z);
        }



        /// <summary>
        /// Filter out invalid entries in indices[] list. Will return indices itself if 
        /// none invalid, and bForceCopy == false
        /// </summary>
        public static int[] FilterValid(int[] indices, Func<int, bool> FilterF, bool bForceCopy = false )
        {
            int nValid = 0;
            for ( int i = 0; i < indices.Length; ++i ) {
                if (FilterF(indices[i]))
                    ++nValid;
            }
            if (nValid == indices.Length && bForceCopy == false)
                return indices;
            int[] valid = new int[nValid];
            int vi = 0;
            for ( int i = 0; i < indices.Length; ++i ) {
                if (FilterF(indices[i]))
                    valid[vi++] = indices[i];
            }
            return valid;
        }



        /// <summary>
        /// return trune if CheckF returns true for all members of indices list
        /// </summary>
        public static bool IndicesCheck(int[] indices, Func<int, bool> CheckF)
        {
            for ( int i = 0; i < indices.Length; ++i ) {
                if (CheckF(indices[i]) == false)
                    return false;
            }
            return true;
        }



        /// <summary>
        /// Apply map to indices
        /// </summary>
        public static void Apply(List<int> indices, IIndexMap map)
        {
            int N = indices.Count;
            for (int i = 0; i < N; ++i)
                indices[i] = map[indices[i]];
        }

        public static void Apply(int[] indices, IIndexMap map)
        {
            int N = indices.Length;
            for (int i = 0; i < N; ++i)
                indices[i] = map[indices[i]];
        }

        public static void Apply(int[] indices, IList<int> map)
        {
            int N = indices.Length;
            for (int i = 0; i < N; ++i)
                indices[i] = map[indices[i]];
        }



        public static void TrianglesToVertices(DMesh3 mesh, IEnumerable<int> triangles, HashSet<int> vertices) {
            foreach ( int tid in triangles ) {
                Index3i tv = mesh.GetTriangle(tid);
                vertices.Add(tv.a); vertices.Add(tv.b); vertices.Add(tv.c);
            }
        }        
        public static void TrianglesToVertices(DMesh3 mesh, HashSet<int> triangles, HashSet<int> vertices) {
            foreach ( int tid in triangles ) {
                Index3i tv = mesh.GetTriangle(tid);
                vertices.Add(tv.a); vertices.Add(tv.b); vertices.Add(tv.c);
            }
        }


        public static void TrianglesToEdges(DMesh3 mesh, IEnumerable<int> triangles, HashSet<int> edges) {
            foreach ( int tid in triangles ) {
                Index3i te = mesh.GetTriEdges(tid);
                edges.Add(te.a); edges.Add(te.b); edges.Add(te.c);
            }
        }
        public static void TrianglesToEdges(DMesh3 mesh, HashSet<int> triangles, HashSet<int> edges) {
            foreach ( int tid in triangles ) {
                Index3i te = mesh.GetTriEdges(tid);
                edges.Add(te.a); edges.Add(te.b); edges.Add(te.c);
            }
        }



        public static void EdgesToVertices(DMesh3 mesh, IEnumerable<int> edges, HashSet<int> vertices) {
            foreach (int eid in edges) { 
                Index2i ev = mesh.GetEdgeV(eid);
                vertices.Add(ev.a); vertices.Add(ev.b);
            }
        }
        public static void EdgesToVertices(DMesh3 mesh, HashSet<int> edges, HashSet<int> vertices) {
            foreach (int eid in edges) { 
                Index2i ev = mesh.GetEdgeV(eid);
                vertices.Add(ev.a); vertices.Add(ev.b);
            }
        }

    }





    public static class gIndices
    {
        // integer indices offsets in x/y directions
        public static readonly Vector2i[] GridOffsets4 = new Vector2i[] {
            new Vector2i( -1, 0), new Vector2i( 1, 0),
            new Vector2i( 0, -1), new Vector2i( 0, 1)
        };

        // integer indices offsets in x/y directions and diagonals
        public static readonly Vector2i[] GridOffsets8 = new Vector2i[] {
            new Vector2i( -1, 0), new Vector2i( 1, 0),
            new Vector2i( 0, -1), new Vector2i( 0, 1),
            new Vector2i( -1, 1), new Vector2i( 1, 1),
            new Vector2i( -1, -1), new Vector2i( 1, -1)
        };



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
        public static readonly int[] BoxFaceNormals = new int[6] { -3, 3, -1, 1, -2, 2 }; 


        // integer indices offsets in x/y/z directions, corresponds w/ BoxFaces directions
        public static readonly Vector3i[] GridOffsets6 = new Vector3i[] {
            new Vector3i( 0, 0,-1), new Vector3i( 0, 0, 1),
            new Vector3i(-1, 0, 0), new Vector3i( 1, 0, 0),
            new Vector3i( 0,-1, 0), new Vector3i( 0, 1, 0)
        };

		// integer indices offsets in x/y/z directions and diagonals
		public static readonly Vector3i[] GridOffsets26 = new Vector3i[] {
			// face-nbrs
			new Vector3i( 0, 0,-1), new Vector3i( 0, 0, 1),
			new Vector3i(-1, 0, 0), new Vector3i( 1, 0, 0),
			new Vector3i( 0,-1, 0), new Vector3i( 0, 1, 0),
			// edge-nbrs (+y, 0, -y)
			new Vector3i(1, 1, 0), new Vector3i(-1, 1, 0),
			new Vector3i(0, 1, 1), new Vector3i( 0, 1,-1),
			new Vector3i(1, 0, 1), new Vector3i(-1, 0, 1),
			new Vector3i(1, 0,-1), new Vector3i(-1, 0,-1),
			new Vector3i(1, -1, 0), new Vector3i(-1,-1, 0),
			new Vector3i(0, -1, 1), new Vector3i( 0,-1,-1),
			// corner-nbrs (+y,-y)
			new Vector3i(1, 1, 1), new Vector3i(-1, 1, 1),
			new Vector3i(1, 1,-1), new Vector3i(-1, 1,-1),
			new Vector3i(1,-1, 1), new Vector3i(-1,-1, 1),
			new Vector3i(1,-1,-1), new Vector3i(-1,-1,-1)
		};



        public static IEnumerable<Vector3i> Grid3Indices(int nx, int ny, int nz)
        {
            for (int z = 0; z < nz; ++z)
                for (int y = 0; y < ny; ++y)
                    for (int x = 0; x < nx; ++x)
                        yield return new Vector3i(x, y, z);
        }


        public static IEnumerable<Vector3i> Grid3IndicesYZ(int ny, int nz)
        {
            for (int z = 0; z < nz; ++z)
                for (int y = 0; y < ny; ++y)
                    yield return new Vector3i(0, y, z);
        }

        public static IEnumerable<Vector3i> Grid3IndicesXZ(int nx, int nz)
        {
            for (int z = 0; z < nz; ++z)
                for (int x = 0; x < nx; ++x)
                    yield return new Vector3i(x, 0, z);
        }

        public static IEnumerable<Vector3i> Grid3IndicesXY(int nx, int ny)
        {
            for (int y = 0; y < ny; ++y)
                for (int x = 0; x < nx; ++x)
                    yield return new Vector3i(x, y, 0);
        }


    }



}
