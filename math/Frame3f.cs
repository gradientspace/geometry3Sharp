using System;

namespace g3
{
    /// <summary>
    /// Frame3f is a float-storage variant of Frame3d, intended to be used for serialization and buffer storage (to reduce memory usage)
    /// Frame3d should be used for any geometric calculations.
    /// </summary>
    public struct Frame3f
    {
        Quaternionf rotation;
        Vector3f origin;

        static readonly public Frame3f Identity = new Frame3f(Vector3f.Zero, Quaternionf.Identity);

        public Frame3f(Frame3d copy)
        {
            this.rotation = (Quaternionf)copy.Rotation;
            this.origin = (Vector3f)copy.Origin;
        }
        public Frame3f(Vector3f origin, Quaternionf orientation)
        {
            rotation = orientation;
            this.origin = origin;
        }
        public Frame3f(Vector3d origin, Quaterniond orientation)
        {
            rotation = (Quaternionf)orientation;
            this.origin = (Vector3f)origin;
        }


        public Quaternionf Rotation
        {
            get { return rotation; }
            set { rotation = value; }
        }
        public Vector3f Origin
        {
            get { return origin; }
            set { origin = value; }
        }

        public readonly Vector3f X {
            get { return rotation.AxisX; }
        }
        public readonly Vector3f Y {
            get { return rotation.AxisY; }
        }
        public readonly Vector3f Z {
            get { return rotation.AxisZ; }
        }

        public readonly bool EpsilonEqual(Frame3f f2, float epsilon) {
            return origin.EpsilonEqual(f2.origin, epsilon) &&
                rotation.EpsilonEqual(f2.rotation, epsilon);
        }

        public override string ToString() {
            return ToString("F4");
        }
        public string ToString(string fmt) {
            return string.Format("[Frame3f: Origin={0}, X={1}, Y={2}, Z={3}]", Origin.ToString(fmt), X.ToString(fmt), Y.ToString(fmt), Z.ToString(fmt));
        }

    }


}
