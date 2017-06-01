using System;
using System.Collections.Generic;

namespace g3
{
    public static class FaceGroupUtil
    {

        public static void SetGroupID(DMesh3 mesh, int to)
        {
            if (mesh.HasTriangleGroups == false)
                return;
            foreach (int tid in mesh.TriangleIndices())
                mesh.SetTriangleGroup(tid, to);
        }


        public static void SetGroupID(DMesh3 mesh, IEnumerable<int> triangles, int to)
        {
            if (mesh.HasTriangleGroups == false)
                return;
            foreach (int tid in triangles)
                mesh.SetTriangleGroup(tid, to);
        }


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


        // dictionary pairs are [group_id, tri_count] 
        public static Dictionary<int, int> CountAllGroups(DMesh3 mesh)
        {
            Dictionary<int, int> GroupCounts = new Dictionary<int, int>();

            if (mesh.HasTriangleGroups) {
                int NT = mesh.MaxTriangleID;
                for (int tid = 0; tid < NT; ++tid) {
                    if (mesh.IsTriangle(tid)) {
                        int gid = mesh.GetTriangleGroup(tid);
                        if (GroupCounts.ContainsKey(gid) == false)
                            GroupCounts[gid] = 1;
                        else
                            GroupCounts[gid]++;
                    }
                }
            }
            return GroupCounts;
        }


        // Returns array of triangle lists (stored as arrays)
        // This requires 2 passes over mesh, but each pass is linear
        public static int[][] FindTriangleSetsByGroup(DMesh3 mesh)
        {
            if (!mesh.HasTriangleGroups)
                return new int[0][];

            // find # of groups and triangle count for each
            Dictionary<int, int> counts = CountAllGroups(mesh);
            List<int> GroupIDs = new List<int>(counts.Keys);
            GroupIDs.Sort();        // might as well sort ascending...
            Dictionary<int, int> groupMap = new Dictionary<int, int>();

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
                    int k = counters[i]++;
                    sets[i][k] = tid;
                }
            }

            return sets;
        }




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


    }
}
