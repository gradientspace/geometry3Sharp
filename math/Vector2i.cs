using System;

namespace g3
{
    public struct Vector2i
    {
        private int[] v;

        public Vector2i(int f) { v = new int[2];  v[0] = v[1] = f; }
        public Vector2i(int x, int y) { v = new int[2]; v[0] = x; v[1] = y;}
        public Vector2i(int[] v2) { v = new int[2]; v[0] = v2[0]; v[1] = v2[1];}

        static public readonly Vector2i Zero = new Vector2i(0, 0);

        public int this[int key]
        {
            get { return v[key]; }
            set { v[key] = value; }
        }

        public void Add(int s) { v[0] += s; v[1] += s; }



        public override string ToString() {
            return string.Format("{0} {1}", v[0], v[1]);
        }

    }
}
