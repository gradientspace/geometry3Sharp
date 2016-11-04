using System;
using System.Collections.Generic;
using System.Text;

namespace g3
{
    public class Vector3d
    {
        public double[] v = { 0, 0, 0 };

        public Vector3d() {}
        public Vector3d(double f) { v[0] = v[1] = v[2] = f; }
        public Vector3d(double x, double y, double z) { v[0] = x; v[1] = y; v[2] = z; }
        public Vector3d(double[] v2) { v[0] = v2[0]; v[1] = v2[1]; v[2] = v2[2]; }

        static public readonly Vector3d Zero = new Vector3d(0.0f, 0.0f, 0.0f);
        static public readonly Vector3d AxisX = new Vector3d(1.0f, 0.0f, 0.0f);
        static public readonly Vector3d AxisY = new Vector3d(0.0f, 1.0f, 0.0f);
        static public readonly Vector3d AxisZ = new Vector3d(0.0f, 0.0f, 1.0f);

        public double this[int key]
        {
            get { return v[key]; }
            set { v[key] = value; }
        }

    }
}
