using System;

namespace g3
{
    /// <summary>
    /// Quaternionf is a float-storage variant of Quaterniond, intended to be used for serialization and buffer storage (to reduce memory usage)
    /// Quaterniond should be used for any geometric calculations.
    /// </summary>
    public struct Quaternionf
    {
        // note: in Wm5 version, this is a 4-element array stored in order (w,x,y,z).
        public float x, y, z, w;

        public Quaternionf(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public Quaternionf(float[] v2) { x = v2[0]; y = v2[1]; z = v2[2]; w = v2[3]; }
        public Quaternionf(Quaternionf q2) { x = q2.x; y = q2.y; z = q2.z; w = q2.w; }

        static public readonly Quaternionf Zero = new Quaternionf(0.0f, 0.0f, 0.0f, 0.0f);
        static public readonly Quaternionf Identity = new Quaternionf(0.0f, 0.0f, 0.0f, 1.0f);

        public float this[int key] {
            get { if (key == 0) return x; else if (key == 1) return y; else if (key == 2) return z; else return w; }
            set { if (key == 0) x = value; else if (key == 1) y = value; else if (key == 2) z = value; else w = value; }
        }


        // these multiply quaternion by (1,0,0), (0,1,0), (0,0,1), respectively.
        // faster than full multiply, because of all the zeros
        public Vector3f AxisX {
            get {
                float twoY = 2 * y; float twoZ = 2 * z;
                float twoWY = twoY * w; float twoWZ = twoZ * w;
                float twoXY = twoY * x; float twoXZ = twoZ * x;
                float twoYY = twoY * y; float twoZZ = twoZ * z;
                return new Vector3f(1 - (twoYY + twoZZ), twoXY + twoWZ, twoXZ - twoWY);
            }
        }
        public Vector3f AxisY {
            get {
                float twoX = 2 * x; float twoY = 2 * y; float twoZ = 2 * z;
                float twoWX = twoX * w; float twoWZ = twoZ * w; float twoXX = twoX * x;
                float twoXY = twoY * x; float twoYZ = twoZ * y; float twoZZ = twoZ * z;
                return new Vector3f(twoXY - twoWZ, 1 - (twoXX + twoZZ), twoYZ + twoWX);
            }
        }
        public Vector3f AxisZ {
            get {
                float twoX = 2 * x; float twoY = 2 * y; float twoZ = 2 * z;
                float twoWX = twoX * w; float twoWY = twoY * w; float twoXX = twoX * x;
                float twoXZ = twoZ * x; float twoYY = twoY * y; float twoYZ = twoZ * y;
                return new Vector3f(twoXZ + twoWY, twoYZ - twoWX, 1 - (twoXX + twoYY));
            }
        }


        public bool EpsilonEqual(Quaternionf q2, float epsilon) {
            return (float)Math.Abs(x - q2.x) <= epsilon && 
                   (float)Math.Abs(y - q2.y) <= epsilon &&
                   (float)Math.Abs(z - q2.z) <= epsilon &&
                   (float)Math.Abs(w - q2.w) <= epsilon;
        }


        public override string ToString() {
            return string.Format("{0:F8} {1:F8} {2:F8} {3:F8}", x, y, z, w);
        }
        public string ToString(string fmt) {
            return string.Format("{0} {1} {2} {3}", x.ToString(fmt), y.ToString(fmt), z.ToString(fmt), w.ToString(fmt));
        }

    }
}
