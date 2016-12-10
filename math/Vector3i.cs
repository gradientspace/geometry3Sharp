using System;
using System.Collections.Generic;
using System.Text;

namespace g3
{
    public struct Vector3i
    {
        public int x;
        public int y;
        public int z;

        public Vector3i(int f) { x = y = z = f; }
        public Vector3i(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public Vector3i(int[] v2) { x = v2[0]; y = v2[1]; z = v2[2]; }

        static public readonly Vector3i Zero = new Vector3i(0, 0, 0);
        static public readonly Vector3i AxisX = new Vector3i(1, 0, 0);
        static public readonly Vector3i AxisY = new Vector3i(0, 1, 0);
        static public readonly Vector3i AxisZ = new Vector3i(0, 0, 1);

        public int this[int key]
        {
            get { return (key == 0) ? x : (key == 1) ? y : z; }
            set { if (key == 0) x = value; else if (key == 1) y = value; else z = value; }
        }

        public int[] array {
            get { return new int[] { x, y, z }; }
        }

        public void Add(int s) { x += s;  y += s;  z += s; }


        public override string ToString() {
            return string.Format("{0} {1} {2}", x, y, z);
        }

    }
}
