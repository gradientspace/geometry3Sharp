using System;
using System.Collections.Generic;
using System.Text;

namespace g3
{
    public class Vector3f
    {
        public float[] v = { 0, 0, 0 };

        public Vector3f() {}
        public Vector3f(float f) { v[0] = v[1] = v[2] = f; }
        public Vector3f(float x, float y, float z) { v[0] = x; v[1] = y; v[2] = z; }
        public Vector3f(float[] v2) { v[0] = v2[0]; v[1] = v2[1]; v[2] = v2[2]; }

        static public Vector3f Zero() { return new Vector3f(0.0f, 0.0f, 0.0f); }

        public float this[int key]
        {
            get { return v[key]; }
            set { v[key] = value; }
        }

    }
}
