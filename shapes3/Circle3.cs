using System;

namespace g3
{
    // somewhat ported from WildMagic5
    public class Circle3d
    {
        // The plane containing the circle is Dot(N,X-C) = 0, where X is any point
        // in the plane.  Vectors U, V, and N form an orthonormal right-handed set
        // (matrix [U V N] is orthonormal and has determinant 1).  The circle
        // within the plane is parameterized by X = C + R*(cos(t)*U + sin(t)*V),
        // where t is an angle in [-pi,pi).

		public Vector3d Center;
        public Vector3d Normal;
        public Vector3d PlaneX, PlaneY;
		public double Radius;
		public bool IsReversed;		// use ccw orientation instead of cw

		public Circle3d(Vector3d center, double radius, Vector3d axis0, Vector3d axis1, Vector3d normal)
		{
			IsReversed = false;
			Center = center;
            Normal = normal;
            PlaneX = axis0;
            PlaneY = axis1;
			Radius = radius;
		}
		public Circle3d(Frame3f frame, double radius, int nNormalAxis = 1)
		{
			IsReversed = false;
			Center = frame.Origin;
            Normal = frame.GetAxis(nNormalAxis);
            PlaneX = frame.GetAxis((nNormalAxis + 1) % 3);
            PlaneY = frame.GetAxis((nNormalAxis + 2) % 3);
			Radius = radius;
		}
		public Circle3d(Vector3d center, double radius)
		{
			IsReversed = false;
			Center = center;
            Normal = Vector3d.AxisY;
            PlaneX = Vector3d.AxisX;
            PlaneY = Vector3d.AxisZ;
			Radius = radius;
		}

		public bool IsClosed {
			get { return true; }
		}

		public void Reverse() {
			IsReversed = ! IsReversed;
		}


		// angle in range [0,360] (but works for any value, obviously)
        public Vector3d SampleDeg(double degrees)
        {
            double theta = degrees * MathUtil.Deg2Rad;
            double c = Math.Cos(theta), s = Math.Sin(theta);
            return Center + c * Radius * PlaneX + s * Radius * PlaneY;
        }

		// angle in range [0,2pi] (but works for any value, obviously)
        public Vector3d SampleRad(double radians)
        {
            double c = Math.Cos(radians), s = Math.Sin(radians);
            return Center + c * Radius * PlaneX + s * Radius * PlaneY;
        }



		public double ParamLength {
			get { return 1.0f; }
		}

		// t in range[0,1] spans circle [0,2pi]
		public Vector3d SampleT(double t) {
			double theta = (IsReversed) ? -t*MathUtil.TwoPI : t*MathUtil.TwoPI;
			double c = Math.Cos(theta), s = Math.Sin(theta);
            return Center + c * Radius * PlaneX + s * Radius * PlaneY;
		}

		public bool HasArcLength { get {return true;} }

		public double ArcLength {
			get {
				return MathUtil.TwoPI * Radius;
			}
		}

		public Vector3d SampleArcLength(double a) {
			double t = a / ArcLength;
			double theta = (IsReversed) ? -t*MathUtil.TwoPI : t*MathUtil.TwoPI;
			double c = Math.Cos(theta), s = Math.Sin(theta);
            return Center + c * Radius * PlaneX + s * Radius * PlaneY;
		}


        public double Circumference {
			get { return MathUtil.TwoPI * Radius; }
		}
        public double Diameter {
			get { return 2 * Radius; }
		}
        public double Area {
            get { return Math.PI * Radius * Radius; }
        }

    }
}
