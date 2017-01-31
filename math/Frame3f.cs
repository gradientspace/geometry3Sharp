using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using g3;

namespace g3
{
    public struct Frame3f
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
            get { return rotation.AxisX; }
        }
        public Vector3f Y
        {
            get { return rotation.AxisY; }
        }
        public Vector3f Z
        {
            get { return rotation.AxisZ; }
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
            Debug.Assert(rotation.w != 0);      // catch un-initialized quaternions
            rotation = q * rotation;
        }
        public Frame3f Rotated(Quaternionf q)
        {
            Debug.Assert(rotation.w != 0);
            return new Frame3f(this.origin, q * this.rotation);
        }
        public Frame3f Rotated(float fAngle, int nAxis)
        {
            Debug.Assert(rotation.w != 0);
            return this.Rotated(new Quaternionf(GetAxis(nAxis), fAngle));
        }

        public void RotateAround(Vector3f point, Quaternionf q)
        {
            Debug.Assert(rotation.w != 0);
            Vector3f dv = q * (origin - point);
            rotation = q * rotation;
            origin = point + dv;
        }
        public Frame3f RotatedAround(Vector3f point, Quaternionf q)
        {
            Debug.Assert(rotation.w != 0);
            Vector3f dv = q * (this.origin - point);
            return new Frame3f(point + dv, q * this.rotation);
        }

        public void AlignAxis(int nAxis, Vector3f vTo)
        {
            Debug.Assert(rotation.w != 0);
            Quaternionf rot = Quaternionf.FromTo(GetAxis(nAxis), vTo);
            Rotate(rot);
        }
        public void ConstrainedAlignAxis(int nAxis, Vector3f vTo, Vector3f vAround)
        {
            Debug.Assert(rotation.w != 0);
            Vector3f axis = GetAxis(nAxis);
            float fAngle = MathUtil.PlaneAngleSignedD(axis, vTo, vAround);
            Quaternionf rot = Quaternionf.AxisAngleD(vAround, fAngle);
            Rotate(rot);
        }


        public Vector3f FromFrameP(Vector2f v, int nPlaneNormalAxis)
        {
            Vector3f dv = new Vector3f(v[0], v[1], 0);
            if (nPlaneNormalAxis == 0) {
                dv[0] = 0; dv[2] = v[0];
            } else if ( nPlaneNormalAxis == 1 ) {
                dv[1] = 0; dv[2] = v[1];
            }
            return this.rotation * dv + this.origin;
        }

        public Vector3f ProjectToPlane(Vector3f p, int nNormal)
        {
            Vector3f d = p - origin;
            Vector3f n = GetAxis(nNormal);
            return origin + (d - d.Dot(n) * n);
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


        public Ray3f ToFrame(Ray3f r)
        {
            return new Ray3f(ToFrameP(r.Origin), ToFrameV(r.Direction));
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



        public static Frame3f Interpolate(Frame3f f1, Frame3f f2, float alpha)
        {
            return new Frame3f(
                Vector3f.Lerp(f1.origin, f2.origin, alpha),
                Quaternionf.Slerp(f1.rotation, f2.rotation, alpha) );
        }



        public bool EpsilonEqual(Frame3f f2, float epsilon) {
            return origin.EpsilonEqual(f2.origin, epsilon) &&
                rotation.EpsilonEqual(f2.rotation, epsilon);
        }
        public bool PrecisionEqual(Frame3f f2, int nDigits) {
            return origin.PrecisionEqual(f2.origin, nDigits) && 
                rotation.PrecisionEqual(f2.rotation, nDigits);
        }


        public override string ToString() {
            return ToString("F4");
        }
        public string ToString(string fmt) {
            return string.Format("[Frame3f: Origin={0}, X={1}, Y={2}, Z={3}]", Origin.ToString(fmt), X.ToString(fmt), Y.ToString(fmt), Z.ToString(fmt));
        }



        // finds minimal rotation that aligns source frame with axes of target frame.
        // considers all signs
        //   1) find smallest angle(axis_source, axis_target), considering all sign permutations
        //   2) rotate source to align axis_source with sign*axis_target
        //   3) now rotate around alined_axis_source to align second-best pair of axes
        public static Frame3f SolveMinRotation(Frame3f source, Frame3f target)
        {
            int best_i = -1, best_j = -1;
            double fMaxAbsDot = 0, fMaxSign = 0;
            for (int i = 0; i < 3; ++i) {
                for (int j = 0; j < 3; ++j) {
                    double d = source.GetAxis(i).Dot(target.GetAxis(j));
                    double a = Math.Abs(d);
                    if (a > fMaxAbsDot) {
                        fMaxAbsDot = a;
                        fMaxSign = Math.Sign(d);
                        best_i = i;
                        best_j = j;
                    }
                }
            }

            Frame3f R1 = source.Rotated(
                Quaternionf.FromTo(source.GetAxis(best_i), (float)fMaxSign * target.GetAxis(best_j)));
            Vector3f vAround = R1.GetAxis(best_i);

            int second_i = -1, second_j = -1;
            double fSecondDot = 0, fSecondSign = 0;
            for (int i = 0; i < 3; ++i) {
                if (i == best_i)
                    continue;
                for (int j = 0; j < 3; ++j) {
                    if (j == best_j)
                        continue;
                    double d = R1.GetAxis(i).Dot(target.GetAxis(j));
                    double a = Math.Abs(d);
                    if (a > fSecondDot) {
                        fSecondDot = a;
                        fSecondSign = Math.Sign(d);
                        second_i = i;
                        second_j = j;
                    }
                }
            }

            R1.ConstrainedAlignAxis(second_i, (float)fSecondSign * target.GetAxis(second_j), vAround);

            return R1;
        }


    }
}
