using System;
using System.Collections.Generic;

namespace g3
{
    public static class FaceGroupUtil
    {

        /// <summary>
        /// Set group ID of all triangles in mesh
        /// </summary>
        public static void SetGroupID(DMesh3 mesh, int to)
        {
            if (mesh.HasTriangleGroups == false)
                return;
            foreach (int tid in mesh.TriangleIndices())
                mesh.SetTriangleGroup(tid, to);
        }


        /// <summary>
        /// Set group id of subset of triangles in mesh
        /// </summary>
        public static void SetGroupID(DMesh3 mesh, IEnumerable<int> triangles, int to)
        {
            if (mesh.HasTriangleGroups == false)
                return;
            foreach (int tid in triangles)
                mesh.SetTriangleGroup(tid, to);
        }


        /// <summary>
        /// replace group id in mesh
        /// </summary>
        public static void SetGroupToGroup(DMesh3 mesh, int from, int to)
        {
            if (mesh.HasTriangleGroups == false)
                return;

            int NT = mesh.MaxTriangleID;
            for ( int tid = 0; tid < NT; ++tid) {
                if (mesh.IsTriangle(tid)) {
                    int gid = mesh.GetTriangleGroup(tid);
                    if (gid == from)
                        mesh.SetTriangleGroup(tid, to);
                }
            }
        }


        /// <summary>
        /// find the set of group ids used in mesh
        /// </summary>
        public static HashSet<int> FindAllGroups(DMesh3 mesh)
        {
            HashSet<int> Groups = new HashSet<int>();

            if (mesh.HasTriangleGroups) {
                int NT = mesh.MaxTriangleID;
                for (int tid = 0; tid < NT; ++tid) {
                    if (mesh.IsTriangle(tid)) {
                        int gid = mesh.GetTriangleGroup(tid);
                        Groups.Add(gid);
                    }
                }
            }
            return Groups;
        }


        /// <summary>
        /// count number of tris in each group in mesh
        /// returned pairs are [group_id, tri_count] 
        /// </summary>
        public static SparseList<int> CountAllGroups(DMesh3 mesh)
        {
            SparseList<int> GroupCounts = new SparseList<int>(mesh.MaxGroupID, 0, 0);

            if (mesh.HasTriangleGroups) {
                int NT = mesh.MaxTriangleID;
                for (int tid = 0; tid < NT; ++tid) {
                    if (mesh.IsTriangle(tid)) {
                        int gid = mesh.GetTriangleGroup(tid);
                        GroupCounts[gid] = GroupCounts[gid] + 1;
                    }
                }
            }
            return GroupCounts;
        }


        /// <summary>
        /// collect triangles by group id. Returns array of triangle lists (stored as arrays).
        /// This requires 2 passes over mesh, but each pass is linear
        /// </summary>
        public static int[][] FindTriangleSetsByGroup(DMesh3 mesh, int ignoreGID = int.MinValue)
        {
            if (!mesh.HasTriangleGroups)
                return new int[0][];

            // find # of groups and triangle count for each
            SparseList<int> counts = CountAllGroups(mesh);
            List<int> GroupIDs = new List<int>();
            foreach ( var idxval in counts.Values() ) {
                if (idxval.Key != ignoreGID && idxval.Value > 0)
                    GroupIDs.Add(idxval.Key);
            }
            GroupIDs.Sort();        // might as well sort ascending...
            SparseList<int> groupMap = new SparseList<int>(mesh.MaxGroupID, GroupIDs.Count, -1);

            // allocate sets
            int[][] sets = new int[GroupIDs.Count][];
            int[] counters = new int[GroupIDs.Count];
            for (int i = 0; i < GroupIDs.Count; ++i) {
                int gid = GroupIDs[i];
                sets[i] = new int[ counts[gid] ];
                counters[i] = 0;
                groupMap[gid] = i;
            }

            // accumulate triangles
            int NT = mesh.MaxTriangleID;
            for (int tid = 0; tid < NT; ++tid) {
                if (mesh.IsTriangle(tid)) {
                    int gid = mesh.GetTriangleGroup(tid);
                    int i = groupMap[gid];
                    if (i >= 0) {
                        int k = counters[i]++;
                        sets[i][k] = tid;
                    }
                }
            }

            return sets;
        }



        /// <summary>
        /// find list of triangles in mesh with specific group id
        /// </summary>
        public static List<int> FindTrianglesByGroup(IMesh mesh, int findGroupID)
        {
            List<int> tris = new List<int>();
            if (mesh.HasTriangleGroups == false)
                return tris;
            foreach ( int tid in mesh.TriangleIndices() ) {
                if ( mesh.GetTriangleGroup(tid) == findGroupID)
                    tris.Add(tid);
            }
            return tris;
        }


        /// <summary>
        /// split input mesh into submeshes based on group ID
        /// **does not** separate disconnected components w/ same group ID
        /// </summary>
        public static DMesh3[] SeparateMeshByGroups(DMesh3 mesh, out int[] groupIDs)
        {
            Dictionary<int, List<int>> meshes = new Dictionary<int, List<int>>();
            foreach ( int tid in mesh.TriangleIndices() ) {
                int gid = mesh.GetTriangleGroup(tid);
                List<int> tris;
                if ( meshes.TryGetValue(gid, out tris) == false ) {
                    tris = new List<int>();
                    meshes[gid] = tris;
                }
                tris.Add(tid);
            }

            DMesh3[] result = new DMesh3[meshes.Count];
            groupIDs = new int[meshes.Count];
            int k = 0;
            foreach ( var pair in meshes ) {
                groupIDs[k] = pair.Key;
                List<int> tri_list = pair.Value;
                result[k++] = DSubmesh3.QuickSubmesh(mesh, tri_list);
            }

            return result;
        }
        public static DMesh3[] SeparateMeshByGroups(DMesh3 mesh) {
            int[] ids; return SeparateMeshByGroups(mesh, out ids);
        }


    }
}
