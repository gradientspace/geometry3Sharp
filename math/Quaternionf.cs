using System;

#if G3_USING_UNITY
using UnityEngine;
#endif


namespace g3
{
    public class Quaternionf
    {
        public float[] v = { 0, 0, 0, 1 };

        public Quaternionf() { }
        public Quaternionf(float x, float y, float z, float w) { v[0] = x; v[1] = y; v[2] = z; v[3] = w; }
        public Quaternionf(float[] v2) { v[0] = v2[0]; v[1] = v2[1]; v[2] = v2[2]; v[3] = v2[3]; }
        public Quaternionf(Vector3f copy) { v[0] = copy.v[0]; v[1] = copy.v[1]; v[2] = copy.v[2]; v[3] = copy.v[3]; }

        public float this[int key]
        {
            get { return v[key]; }
            set { v[key] = value; }
        }


        public override string ToString() {
            return string.Format("{0:F8} {1:F8} {2:F8} {3:F8}", v[0], v[1], v[2], v[3]);
        }



#if G3_USING_UNITY
        public static implicit operator Quaternionf(Quaternion q)
        {
            return new Quaternionf(q.x, q.y, q.z, q.w);
        }
        public static implicit operator Quaternion(Quaternionf q)
        {
            return new Quaternion(q[0], q[1], q[2], q[3]);
        }
#endif

    }
}
