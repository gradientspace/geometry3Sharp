using System;
using System.Collections.Generic;
using System.Text;

namespace g3
{
    public class Vector3i
    {
        public int[] v = { 0, 0, 0 };

        public Vector3i() {}
        public Vector3i(int f) { v[0] = v[1] = v[2] = f; }
        public Vector3i(int x, int y, int z) { v[0] = x; v[1] = y; v[2] = z; }
        public Vector3i(int[] v2) { v[0] = v2[0]; v[1] = v2[1]; v[2] = v2[2]; }

        static public readonly Vector3i Zero = new Vector3i(0, 0, 0);
        static public readonly Vector3i AxisX = new Vector3i(1, 0, 0);
        static public readonly Vector3i AxisY = new Vector3i(0, 1, 0);
        static public readonly Vector3i AxisZ = new Vector3i(0, 0, 1);

        public int this[int key]
        {
            get { return v[key]; }
            set { v[key] = value; }
        }


        public void Add(int s) { v[0] += s;  v[1] += s;  v[2] += s; }

    }
}
