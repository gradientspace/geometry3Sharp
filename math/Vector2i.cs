using System;

namespace g3
{
    public struct Vector2i
    {
        public int x;
        public int y;

        public Vector2i(int f) { x = y = f; }
        public Vector2i(int x, int y) { this.x = x; this.y = y; }
        public Vector2i(int[] v2) { x = v2[0]; y = v2[1]; }

        static public readonly Vector3i Zero = new Vector3i(0, 0, 0);
        static public readonly Vector3i AxisX = new Vector3i(1, 0, 0);
        static public readonly Vector3i AxisY = new Vector3i(0, 1, 0);
        static public readonly Vector3i AxisZ = new Vector3i(0, 0, 1);

        public int this[int key]
        {
            get { return (key == 0) ? x : y; }
            set { if (key == 0) x = value; else y = value; }
        }

        public int[] array
        {
            get { return new int[] { x, y }; }
        }

        public void Add(int s) { x += s; y += s; }


        public override string ToString()
        {
            return string.Format("{0} {1}", x, y);
        }

    }
}
