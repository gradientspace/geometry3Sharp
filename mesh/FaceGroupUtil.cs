using System;
using System.Collections.Generic;

namespace g3
{
    public static class FaceGroupUtil
    {

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

    }
}
