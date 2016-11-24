using System;
using System.Collections.Generic;
using System.Text;
using g3;



namespace g3
{
    public class Frame3f
    {
        Quaternionf rotation;
        Vector3f origin;

        static readonly public Frame3f Identity = new Frame3f(Vector3f.Zero, Quaternionf.Identity);

        public Frame3f(Frame3f copy)
        {
            this.rotation = copy.rotation;
            this.origin = copy.origin;
        }

        public Frame3f(Vector3f origin)
        {
            rotation = Quaternionf.Identity;
            this.origin = origin;
        }

        public Frame3f(Vector3f origin, Vector3f setZ)
        {
            rotation = Quaternionf.FromTo(Vector3f.AxisZ, setZ);
            this.origin = origin;
        }

        public Frame3f(Vector3f origin, Vector3f setAxis, int nAxis)
        {
            if (nAxis == 0)
                rotation = Quaternionf.FromTo(Vector3f.AxisX, setAxis);
            else if (nAxis == 1)
                rotation = Quaternionf.FromTo(Vector3f.AxisY, setAxis);
            else
                rotation = Quaternionf.FromTo(Vector3f.AxisZ, setAxis);
            this.origin = origin;
        }

        public Frame3f(Vector3f origin, Quaternionf orientation)
        {
            rotation = orientation;
            this.origin = origin;
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

        public Vector3f X
        {
            get { return rotation * Vector3f.AxisX; }
        }
        public Vector3f Y
        {
            get { return rotation * Vector3f.AxisY; }
        }
        public Vector3f Z
        {
            get { return rotation * Vector3f.AxisZ; }
        }

        public Vector3f GetAxis(int nAxis)
        {
            if (nAxis == 0)
                return rotation * Vector3f.AxisX;
            else if (nAxis == 1)
                return rotation * Vector3f.AxisY;
            else if (nAxis == 2)
                return rotation * Vector3f.AxisZ;
            else
                throw new ArgumentOutOfRangeException("nAxis");
        }


        public void Translate(Vector3f v)
        {
            origin += v;
        }
        public Frame3f Translated(Vector3f v)
        {
            return new Frame3f(this.origin + v, this.rotation);
        }
        public Frame3f Translated(float fDistance, int nAxis)
        {
            return new Frame3f(this.origin + fDistance * this.GetAxis(nAxis), this.rotation);
        }
        public Frame3f Scaled(float f)
        {
            return new Frame3f(f * this.origin, this.rotation);
        }
        public void Rotate(Quaternionf q)
        {
            rotation = q * rotation;
        }
        public Frame3f Rotated(Quaternionf q)
        {
            return new Frame3f(this.origin, q * this.rotation);
        }
        public Frame3f Rotated(float fAngle, int nAxis)
        {
            return this.Rotated(new Quaternionf(GetAxis(nAxis), fAngle));
        }


        public void AlignAxis(int nAxis, Vector3f vTo)
        {
            Quaternionf rot = Quaternionf.FromTo(GetAxis(nAxis), vTo);
            Rotate(rot);
        }
        public void ConstrainedAlignAxis(int nAxis, Vector3f vTo, Vector3f vAround)
        {
            Vector3f axis = GetAxis(nAxis);
            float fAngle = MathUtil.PlaneAngleSignedD(axis, vTo, vAround);
            Quaternionf rot = Quaternionf.AxisAngleD(vAround, fAngle);
            Rotate(rot);
        }




        public Vector3f ToFrameP(Vector3f v)
        {
            v = v - this.origin;
            v = Quaternionf.Inverse(this.rotation) * v;
            return v;
        }
        public Vector3f FromFrameP(Vector3f v)
        {
            return this.rotation * v + this.origin;
        }


        public Vector3f ToFrameV(Vector3f v)
        {
            return Quaternionf.Inverse(this.rotation) * v;
        }
        public Vector3f FromFrameV(Vector3f v)
        {
            return this.rotation * v;
        }

        public Quaternionf ToFrame(Quaternionf q)
        {
            return Quaternionf.Inverse(this.rotation) * q;
        }
        public Quaternionf FromFrame(Quaternionf q)
        {
            return this.rotation * q;
        }


        public Frame3f ToFrame(Frame3f f)
        {
            return new Frame3f(ToFrameP(f.origin), ToFrame(f.rotation));
        }
        public Frame3f FromFrame(Frame3f f)
        {
            return new Frame3f(FromFrameP(f.origin), FromFrame(f.rotation));
        }



        public Vector3f RayPlaneIntersection(Vector3f ray_origin, Vector3f ray_direction, int nAxisAsNormal)
        {
            Vector3f N = GetAxis(nAxisAsNormal);
            float d = -Vector3f.Dot(Origin, N);
            float t = -(Vector3f.Dot(ray_origin, N) + d) / (Vector3f.Dot(ray_direction, N));
            return ray_origin + t * ray_direction;
        }


        public override string ToString() {
            return ToString("F4");
        }
        public virtual string ToString(string fmt) {
            return string.Format("[Frame3f: Origin={0}, X={1}, Y={2}, Z={3}]", Origin.ToString(fmt), X.ToString(fmt), Y.ToString(fmt), Z.ToString(fmt));
        }


    }
}
