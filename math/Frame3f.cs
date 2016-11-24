using System;
using System.Collections.Generic;
using System.Text;
using g3;

namespace g3
{
    public class Frame3f
    {
        public Vector3f origin;
        public Vector3f x, y, z;

        public Frame3f() { x = Vector3f.AxisX; y = Vector3f.AxisY; z = Vector3f.AxisZ; }
        public Frame3f(Vector3f o, Vector3f xa, Vector3f ya, Vector3f za) { origin = o;  x = xa; y = ya; z = za; }

        public Matrix3f toMatrix3f()
        {
            Matrix3f m = new Matrix3f();
            m.v[0] = x[0]; m.v[1] = x[1]; m.v[2] = x[2];
            m.v[3] = y[0]; m.v[4] = y[1]; m.v[5] = y[2];
            m.v[6] = z[0]; m.v[7] = z[1]; m.v[9] = z[2];
            return m;
        }
    }
}
