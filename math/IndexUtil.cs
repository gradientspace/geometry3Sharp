using System;

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

        // return index of a in tri_verts, or InvalidID if not found
        public static int find_tri_index(int a, int[] tri_verts)
        {
            if (tri_verts[0] == a) return 0;
            if (tri_verts[1] == a) return 1;
            if (tri_verts[2] == a) return 2;
            return DMesh3.InvalidID;
        }
		public static int find_tri_index(int a, Vector3i tri_verts)
		{
			if (tri_verts[0] == a) return 0;
			if (tri_verts[1] == a) return 1;
			if (tri_verts[2] == a) return 2;
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

        // find sequence [a,b] in tri_verts (mod3) and return index of a, or InvalidID if not found
        public static int find_tri_ordered_edge(int a, int b, int[] tri_verts)
        {
            if (tri_verts[0] == a && tri_verts[1] == b) return 0;
            if (tri_verts[1] == a && tri_verts[2] == b) return 1;
            if (tri_verts[2] == a && tri_verts[0] == b) return 2;
            return DMesh3.InvalidID;
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
		public static int find_tri_other_vtx(int a, int b, Vector3i tri_verts)
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

        // find sequence [a,b] in tri_verts (mod3) then return the third **index**, or InvalidID if not found
        public static int find_tri_other_index(int a, int b, int[] tri_verts)
        {
            for (int j = 0; j < 3; ++j) {
                if (same_pair_unordered(a, b, tri_verts[j], tri_verts[(j + 1) % 3]))
                    return (j + 2) % 3;
            }
            return DMesh3.InvalidID;
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

    }
}
