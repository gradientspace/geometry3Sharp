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
        public Frame3f(Vector3d origin)
        {
            rotation = Quaternionf.Identity;
            this.origin = (Vector3f)origin;
        }

        public Frame3f(Vector3f origin, Vector3f setZ)
        {
            rotation = Quaternionf.FromTo(Vector3f.AxisZ, setZ);
            this.origin = origin;
        }

        public Frame3f(Vector3d origin, Vector3d setZ)
        {
            rotation = Quaternionf.FromTo(Vector3f.AxisZ, (Vector3f)setZ);
            this.origin = (Vector3f)origin;
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

        public Frame3f(Vector3f origin, Vector3f x, Vector3f y, Vector3f z)
        {
            this.origin = origin;
            Matrix3f m = new Matrix3f(x, y, z, false);
            this.rotation = m.ToQuaternion();
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

        public void Scale(float f)
        {
            origin *= f;
        }
        public void Scale(Vector3f scale)
        {
            origin *= scale;
        }
        public Frame3f Scaled(float f)
        {
            return new Frame3f(f * this.origin, this.rotation);
        }
        public Frame3f Scaled(Vector3f scale)
        {
            return new Frame3f(scale * this.origin, this.rotation);
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

        /// <summary>
        /// this rotates the frame around its own axes, rather than around the world axes,
        /// which is what Rotate() does. So, RotateAroundAxis(AxisAngleD(Z,180)) is equivalent
        /// to Rotate(AxisAngleD(My_AxisZ,180)). 
        /// </summary>
        public void RotateAroundAxes(Quaternionf q)
        {
            rotation = rotation * q;
        }

        public void RotateAround(Vector3f point, Quaternionf q)
        {
            Vector3f dv = q * (origin - point);
            rotation = q * rotation;
            origin = point + dv;
        }
        public Frame3f RotatedAround(Vector3f point, Quaternionf q)
        {
            Vector3f dv = q * (this.origin - point);
            return new Frame3f(point + dv, q * this.rotation);
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

        public Vector3f ProjectToPlane(Vector3f p, int nNormal)
        {
            Vector3f d = p - origin;
            Vector3f n = GetAxis(nNormal);
            return origin + (d - d.Dot(n) * n);
        }

        public Vector3f FromPlaneUV(Vector2f v, int nPlaneNormalAxis)
        {
            Vector3f dv = new Vector3f(v[0], v[1], 0);
            if (nPlaneNormalAxis == 0) {
                dv[0] = 0; dv[2] = v[0];
            } else if ( nPlaneNormalAxis == 1 ) {
                dv[1] = 0; dv[2] = v[1];
            }
            return this.rotation * dv + this.origin;
        }
        [System.Obsolete("replaced with FromPlaneUV")]
        public Vector3f FromFrameP(Vector2f v, int nPlaneNormalAxis) {
            return FromPlaneUV(v, nPlaneNormalAxis);
        }

        public Vector2f ToPlaneUV(Vector3f p, int nNormal = 2, int nAxis0 = 0, int nAxis1 = 1)
        {
            Vector3f d = p - origin;
            float fu = d.Dot(GetAxis(nAxis0));
            float fv = d.Dot(GetAxis(nAxis1));
            return new Vector2f(fu, fv);
        }


        public float DistanceToPlane(Vector3f p, int nNormal)
        {
            return Math.Abs((p - origin).Dot(GetAxis(nNormal)));
        }
		public float DistanceToPlaneSigned(Vector3f p, int nNormal)
		{
			return (p - origin).Dot(GetAxis(nNormal));
		}


        ///<summary> Map point *into* local coordinates of Frame </summary>
		public Vector3f ToFrameP(Vector3f v)
        {
            v = v - this.origin;
            v = Quaternionf.Inverse(this.rotation) * v;
            return v;
        }
        ///<summary> Map point *into* local coordinates of Frame </summary>
        public Vector3d ToFrameP(Vector3d v)
        {
            v = v - this.origin;
            v = Quaternionf.Inverse(this.rotation) * v;
            return v;
        }
        /// <summary> Map point *from* local frame coordinates into "world" coordinates </summary>
        public Vector3f FromFrameP(Vector3f v)
        {
            return this.rotation * v + this.origin;
        }
        /// <summary> Map point *from* local frame coordinates into "world" coordinates </summary>
        public Vector3d FromFrameP(Vector3d v)
        {
            return this.rotation * v + this.origin;
        }


        ///<summary> Map vector *into* local coordinates of Frame </summary>
        public Vector3f ToFrameV(Vector3f v)
        {
            return Quaternionf.Inverse(this.rotation) * v;
        }
        ///<summary> Map vector *into* local coordinates of Frame </summary>
        public Vector3d ToFrameV(Vector3d v)
        {
            return Quaternionf.Inverse(this.rotation) * v;
        }
        /// <summary> Map vector *from* local frame coordinates into "world" coordinates </summary>
        public Vector3f FromFrameV(Vector3f v)
        {
            return this.rotation * v;
        }
        /// <summary> Map vector *from* local frame coordinates into "world" coordinates </summary>
        public Vector3d FromFrameV(Vector3d v)
        {
            return this.rotation * v;
        }



        ///<summary> Map quaternion *into* local coordinates of Frame </summary>
        public Quaternionf ToFrame(Quaternionf q)
        {
            return Quaternionf.Inverse(this.rotation) * q;
        }
        /// <summary> Map quaternion *from* local frame coordinates into "world" coordinates </summary>
        public Quaternionf FromFrame(Quaternionf q)
        {
            return this.rotation * q;
        }


        ///<summary> Map ray *into* local coordinates of Frame </summary>
        public Ray3f ToFrame(Ray3f r)
        {
            return new Ray3f(ToFrameP(r.Origin), ToFrameV(r.Direction));
        }
        /// <summary> Map ray *from* local frame coordinates into "world" coordinates </summary>
        public Ray3f FromFrame(Ray3f r)
        {
            return new Ray3f(FromFrameP(r.Origin), FromFrameV(r.Direction));
        }

        ///<summary> Map frame *into* local coordinates of Frame </summary>
        public Frame3f ToFrame(Frame3f f)
        {
            return new Frame3f(ToFrameP(f.origin), ToFrame(f.rotation));
        }
        /// <summary> Map frame *from* local frame coordinates into "world" coordinates </summary>
        public Frame3f FromFrame(Frame3f f)
        {
            return new Frame3f(FromFrameP(f.origin), FromFrame(f.rotation));
        }



        public Box3f ToFrame(Box3f box) {
            box.Center = ToFrameP(box.Center);
            box.AxisX = ToFrameV(box.AxisX);
            box.AxisY = ToFrameV(box.AxisY);
            box.AxisZ = ToFrameV(box.AxisZ);
            return box;
        }
        public Box3f FromFrame(Box3f box) {
            box.Center = FromFrameP(box.Center);
            box.AxisX = FromFrameV(box.AxisX);
            box.AxisY = FromFrameV(box.AxisY);
            box.AxisZ = FromFrameV(box.AxisZ);
            return box;
        }
        public Box3d ToFrame(Box3d box) {
            box.Center = ToFrameP(box.Center);
            box.AxisX = ToFrameV(box.AxisX);
            box.AxisY = ToFrameV(box.AxisY);
            box.AxisZ = ToFrameV(box.AxisZ);
            return box;
        }
        public Box3d FromFrame(Box3d box) {
            box.Center = FromFrameP(box.Center);
            box.AxisX = FromFrameV(box.AxisX);
            box.AxisY = FromFrameV(box.AxisY);
            box.AxisZ = FromFrameV(box.AxisZ);
            return box;
        }


        /// <summary>
        /// Compute intersection of ray with plane passing through frame origin, normal
        /// to the specified axis. 
        /// If the ray is parallel to the plane, no intersection can be found, and
        /// we return Vector3f.Invalid
        /// </summary>
        public Vector3f RayPlaneIntersection(Vector3f ray_origin, Vector3f ray_direction, int nAxisAsNormal)
        {
            Vector3f N = GetAxis(nAxisAsNormal);
            float d = -Vector3f.Dot(Origin, N);
            float div = Vector3f.Dot(ray_direction, N);
            if (MathUtil.EpsilonEqual(div, 0, MathUtil.ZeroTolerancef))
                return Vector3f.Invalid;
            float t = -(Vector3f.Dot(ray_origin, N) + d) / div;
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
