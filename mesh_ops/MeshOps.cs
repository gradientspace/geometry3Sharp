using System;

namespace g3
{

    public struct SetGroupBehavior
    {
        public enum Modes
        {
            Ignore = 0,
            AutoGenerate = 1,
            UseConstant = 2
        };
        Modes Mode;
        int SetGroupID;

        public SetGroupBehavior(Modes mode, int id = 0) {
            Mode = mode;
            SetGroupID = id;
        }

        public int GetGroupID(DMesh3 mesh)
        {
            if (Mode == Modes.Ignore)
                return -1;
            else if (Mode == Modes.AutoGenerate)
                return mesh.AllocateTriangleGroup();
            else
                return SetGroupID;
        }

        public static SetGroupBehavior Ignore { get { return new SetGroupBehavior(Modes.Ignore, 0); } }
        public static SetGroupBehavior AutoGenerate { get { return new SetGroupBehavior(Modes.AutoGenerate, 0); } }
        public static SetGroupBehavior SetTo(int groupID) { return new SetGroupBehavior(Modes.UseConstant, groupID); }
    }


    public static class MeshOps
    {

    }
}
