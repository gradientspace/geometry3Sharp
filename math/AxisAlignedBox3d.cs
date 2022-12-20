using System;

namespace g3
{
    public struct AxisAlignedBox3d : IComparable<AxisAlignedBox3d>, IEquatable<AxisAlignedBox3d>
    {
        public Vector3d Min;
        public Vector3d Max;

        public static readonly AxisAlignedBox3d Empty = new AxisAlignedBox3d(false);
        public static readonly AxisAlignedBox3d Zero = new AxisAlignedBox3d(0);
        public static readonly AxisAlignedBox3d UnitPositive = new AxisAlignedBox3d(1);
        public static readonly AxisAlignedBox3d Infinite = 
            new AxisAlignedBox3d(Double.MinValue, Double.MinValue, Double.MinValue, Double.MaxValue, Double.MaxValue, Double.MaxValue);


        public AxisAlignedBox3d(bool bIgnore) {
            Min = new Vector3d(Double.MaxValue, Double.MaxValue, Double.MaxValue);
            Max = new Vector3d(Double.MinValue, Double.MinValue, Double.MinValue);
        }

        public AxisAlignedBox3d(double xmin, double ymin, double zmin, double xmax, double ymax, double zmax) {
            Min = new Vector3d(xmin, ymin, zmin);
            Max = new Vector3d(xmax, ymax, zmax);
        }

		/// <summary>
		/// init box [0,size] x [0,size] x [0,size]
		/// </summary>
        public AxisAlignedBox3d(double fCubeSize) {
            Min = new Vector3d(0, 0, 0);
            Max = new Vector3d(fCubeSize, fCubeSize, fCubeSize);
        }

		/// <summary>
		/// Init box [0,width] x [0,height] x [0,depth]
		/// </summary>
        public AxisAlignedBox3d(double fWidth, double fHeight, double fDepth) {
            Min = new Vector3d(0, 0, 0);
            Max = new Vector3d(fWidth, fHeight, fDepth);
        }

        public AxisAlignedBox3d(Vector3d vMin, Vector3d vMax) {
            Min = new Vector3d(Math.Min(vMin.x, vMax.x), Math.Min(vMin.y, vMax.y), Math.Min(vMin.z, vMax.z));
            Max = new Vector3d(Math.Max(vMin.x, vMax.x), Math.Max(vMin.y, vMax.y), Math.Max(vMin.z, vMax.z));
        }
        public AxisAlignedBox3d(ref Vector3d vMin, ref Vector3d vMax) {
            Min = new Vector3d(Math.Min(vMin.x, vMax.x), Math.Min(vMin.y, vMax.y), Math.Min(vMin.z, vMax.z));
            Max = new Vector3d(Math.Max(vMin.x, vMax.x), Math.Max(vMin.y, vMax.y), Math.Max(vMin.z, vMax.z));
        }

        public AxisAlignedBox3d(Vector3d vCenter, double fHalfWidth, double fHalfHeight, double fHalfDepth) {
            Min = new Vector3d(vCenter.x - fHalfWidth, vCenter.y - fHalfHeight, vCenter.z - fHalfDepth);
            Max = new Vector3d(vCenter.x + fHalfWidth, vCenter.y + fHalfHeight, vCenter.z + fHalfDepth);
        }
        public AxisAlignedBox3d(ref Vector3d vCenter, double fHalfWidth, double fHalfHeight, double fHalfDepth) {
            Min = new Vector3d(vCenter.x - fHalfWidth, vCenter.y - fHalfHeight, vCenter.z - fHalfDepth);
            Max = new Vector3d(vCenter.x + fHalfWidth, vCenter.y + fHalfHeight, vCenter.z + fHalfDepth);
        }

        public AxisAlignedBox3d(Vector3d vCenter, double fHalfSize) {
            Min = new Vector3d(vCenter.x - fHalfSize, vCenter.y - fHalfSize, vCenter.z - fHalfSize);
            Max = new Vector3d(vCenter.x + fHalfSize, vCenter.y + fHalfSize, vCenter.z + fHalfSize);
        }

        public AxisAlignedBox3d(Vector3d vCenter) {
            Min = Max = vCenter;
        }

        public double Width {
            get { return Math.Max(Max.x - Min.x, 0); }
        }
        public double Height {
            get { return Math.Max(Max.y - Min.y, 0); }
        }
        public double Depth {
            get { return Math.Max(Max.z - Min.z, 0); }
        }

        public double Volume {
            get { return Width * Height * Depth; }
        }
        public double DiagonalLength {
            get { return (double)Math.Sqrt((Max.x - Min.x) * (Max.x - Min.x) 
                + (Max.y - Min.y) * (Max.y - Min.y) + (Max.z - Min.z) * (Max.z - Min.z)); }
        }
        public double MaxDim {
            get { return Math.Max(Width, Math.Max(Height, Depth)); }
        }

        public Vector3d Diagonal
        {
            get { return new Vector3d(Max.x - Min.x, Max.y - Min.y, Max.z-Min.z); }
        }
        public Vector3d Extents
        {
            get { return new Vector3d((Max.x - Min.x)*0.5, (Max.y - Min.y)*0.5, (Max.z - Min.z)*0.5); }
        }
        public Vector3d Center {
            get { return new Vector3d(0.5 * (Min.x + Max.x), 0.5 * (Min.y + Max.y), 0.5 * (Min.z + Max.z)); }
        }

        public static bool operator ==(AxisAlignedBox3d a, AxisAlignedBox3d b) {
            return a.Min == b.Min && a.Max == b.Max;
        }
        public static bool operator !=(AxisAlignedBox3d a, AxisAlignedBox3d b) {
            return a.Min != b.Min || a.Max != b.Max;
        }
        public override bool Equals(object obj) {
            return this == (AxisAlignedBox3d)obj;
        }
        public bool Equals(AxisAlignedBox3d other) {
            return this == other;
        }
        public int CompareTo(AxisAlignedBox3d other) {
            int c = this.Min.CompareTo(other.Min);
            if (c == 0)
                return this.Max.CompareTo(other.Max);
            return c;
        }
        public override int GetHashCode() {
            unchecked { // Overflow is fine, just wrap
                int hash = (int) 2166136261;
                hash = (hash * 16777619) ^ Min.GetHashCode();
                hash = (hash * 16777619) ^ Max.GetHashCode();
                return hash;
            }
        }


        // See Box3.Corner for details on which corner is which
        public Vector3d Corner(int i)
        {
            double x = (  ((i&1) != 0) ^ ((i&2) != 0) ) ? (Max.x) : (Min.x);
            double y = ( (i / 2) % 2 == 0 ) ? (Min.y) : (Max.y);
            double z = (i < 4) ? (Min.z) : (Max.z);
            return new Vector3d(x, y, z);
        }

        /// <summary>
        /// Returns point on face/edge/corner. For each coord value neg==min, 0==center, pos==max
        /// </summary>
        public Vector3d Point(int xi, int yi, int zi)
        {
            double x = (xi < 0) ? Min.x : ((xi == 0) ? (0.5*(Min.x + Max.x)) : Max.x);
            double y = (yi < 0) ? Min.y : ((yi == 0) ? (0.5*(Min.y + Max.y)) : Max.y);
            double z = (zi < 0) ? Min.z : ((zi == 0) ? (0.5*(Min.z + Max.z)) : Max.z);
            return new Vector3d(x, y, z);
        }


        // TODO
        ////! 0 == bottom-left, 1 = bottom-right, 2 == top-right, 3 == top-left
        //public Vector3d GetCorner(int i) {
        //    return new Vector3d((i % 3 == 0) ? Min.x : Max.x, (i < 2) ? Min.y : Max.y);
        //}

        //! value is subtracted from min and added to max
        public void Expand(double fRadius) {
            Min.x -= fRadius; Min.y -= fRadius; Min.z -= fRadius;
            Max.x += fRadius; Max.y += fRadius; Max.z += fRadius;
        }

        //! return this box expanded by radius
        public AxisAlignedBox3d Expanded(double fRadius) {
            return new AxisAlignedBox3d(
                Min.x - fRadius, Min.y - fRadius, Min.z - fRadius,
                Max.x + fRadius, Max.y + fRadius, Max.z + fRadius);
        }

        //! value is added to min and subtracted from max
        public void Contract(double fRadius) {
            double w = 2 * fRadius;
            if ( w > Max.x-Min.x ) { Min.x = Max.x = 0.5 * (Min.x + Max.x); }
                else { Min.x += fRadius; Max.x -= fRadius; }
            if ( w > Max.y-Min.y ) { Min.y = Max.y = 0.5 * (Min.y + Max.y); }
                else { Min.y += fRadius; Max.y -= fRadius; }
            if ( w > Max.z-Min.z ) { Min.z = Max.z = 0.5 * (Min.z + Max.z); }
                else { Min.z += fRadius; Max.z -= fRadius; }
        }

        //! return this box expanded by radius
        public AxisAlignedBox3d Contracted(double fRadius) {
            AxisAlignedBox3d result = new AxisAlignedBox3d(
                Min.x + fRadius, Min.y + fRadius, Min.z + fRadius,
                Max.x - fRadius, Max.y - fRadius, Max.z - fRadius);
            if (result.Min.x > result.Max.x) { result.Min.x = result.Max.x = 0.5 * (Min.x + Max.x); }
            if (result.Min.y > result.Max.y) { result.Min.y = result.Max.y = 0.5 * (Min.y + Max.y); }
            if (result.Min.z > result.Max.z) { result.Min.z = result.Max.z = 0.5 * (Min.z + Max.z); }
            return result;
        }


        public void Scale(double sx, double sy, double sz)
        {
            Vector3d c = Center;
            Vector3d e = Extents; e.x *= sx; e.y *= sy; e.z *= sz;
            Min = new Vector3d(c.x - e.x, c.y - e.y, c.z - e.z);
            Max = new Vector3d(c.x + e.x, c.y + e.y, c.z + e.z);
        }

        public void Contain(Vector3d v) {
            Min.x = Math.Min(Min.x, v.x);
            Min.y = Math.Min(Min.y, v.y);
            Min.z = Math.Min(Min.z, v.z);
            Max.x = Math.Max(Max.x, v.x);
            Max.y = Math.Max(Max.y, v.y);
            Max.z = Math.Max(Max.z, v.z);
        }

        public void Contain(ref Vector3d v) {
            Min.x = Math.Min(Min.x, v.x);
            Min.y = Math.Min(Min.y, v.y);
            Min.z = Math.Min(Min.z, v.z);
            Max.x = Math.Max(Max.x, v.x);
            Max.y = Math.Max(Max.y, v.y);
            Max.z = Math.Max(Max.z, v.z);
        }

        public void Contain(AxisAlignedBox3d box) {
            Min.x = Math.Min(Min.x, box.Min.x);
            Min.y = Math.Min(Min.y, box.Min.y);
            Min.z = Math.Min(Min.z, box.Min.z);
            Max.x = Math.Max(Max.x, box.Max.x);
            Max.y = Math.Max(Max.y, box.Max.y);
            Max.z = Math.Max(Max.z, box.Max.z);
        }

        public void Contain(ref AxisAlignedBox3d box) {
            Min.x = Math.Min(Min.x, box.Min.x);
            Min.y = Math.Min(Min.y, box.Min.y);
            Min.z = Math.Min(Min.z, box.Min.z);
            Max.x = Math.Max(Max.x, box.Max.x);
            Max.y = Math.Max(Max.y, box.Max.y);
            Max.z = Math.Max(Max.z, box.Max.z);
        }

        public AxisAlignedBox3d Intersect(AxisAlignedBox3d box) {
            AxisAlignedBox3d intersect = new AxisAlignedBox3d(
                Math.Max(Min.x, box.Min.x), Math.Max(Min.y, box.Min.y), Math.Max(Min.z, box.Min.z),
                Math.Min(Max.x, box.Max.x), Math.Min(Max.y, box.Max.y), Math.Min(Max.z, box.Max.z));
            if (intersect.Height <= 0 || intersect.Width <= 0 || intersect.Depth <= 0)
                return AxisAlignedBox3d.Empty;
            else
                return intersect;
        }



        public bool Contains(Vector3d v) {
            return (Min.x <= v.x) && (Min.y <= v.y) && (Min.z <= v.z)
                && (Max.x >= v.x) && (Max.y >= v.y) && (Max.z >= v.z);
        }
        public bool Contains(ref Vector3d v) {
            return (Min.x <= v.x) && (Min.y <= v.y) && (Min.z <= v.z)
                && (Max.x >= v.x) && (Max.y >= v.y) && (Max.z >= v.z);
        }

        public bool Contains(AxisAlignedBox3d box2) {
            return Contains(ref box2.Min) && Contains(ref box2.Max);
        }
        public bool Contains(ref AxisAlignedBox3d box2) {
            return Contains(ref box2.Min) && Contains(ref box2.Max);
        }


        public bool Intersects(AxisAlignedBox3d box) {
            return !((box.Max.x <= Min.x) || (box.Min.x >= Max.x) 
                || (box.Max.y <= Min.y) || (box.Min.y >= Max.y)
                || (box.Max.z <= Min.z) || (box.Min.z >= Max.z) );
        }



        public double DistanceSquared(Vector3d v)
        {
            double dx = (v.x < Min.x) ? Min.x - v.x : (v.x > Max.x ? v.x - Max.x : 0);
            double dy = (v.y < Min.y) ? Min.y - v.y : (v.y > Max.y ? v.y - Max.y : 0);
            double dz = (v.z < Min.z) ? Min.z - v.z : (v.z > Max.z ? v.z - Max.z : 0);
            return dx * dx + dy * dy + dz * dz;
        }
        public double Distance(Vector3d v)
        {
            return Math.Sqrt(DistanceSquared(v));
        }

        public double SignedDistance(Vector3d v)
        {
            if ( Contains(v) == false ) {
                return Distance(v);
            } else {
                double dx = Math.Min(Math.Abs(v.x - Min.x), Math.Abs(v.x - Max.x));
                double dy = Math.Min(Math.Abs(v.y - Min.y), Math.Abs(v.y - Max.y));
                double dz = Math.Min(Math.Abs(v.z - Min.z), Math.Abs(v.z - Max.z));
                return -MathUtil.Min(dx, dy, dz);
            }
        }


        public double DistanceSquared(ref AxisAlignedBox3d box2)
        {
            // compute lensqr( max(0, abs(center1-center2) - (extent1+extent2)) )
            double delta_x = Math.Abs((box2.Min.x + box2.Max.x) - (Min.x + Max.x))
                    - ((Max.x - Min.x) + (box2.Max.x - box2.Min.x));
            if ( delta_x < 0 )
                delta_x = 0;
            double delta_y = Math.Abs((box2.Min.y + box2.Max.y) - (Min.y + Max.y))
                    - ((Max.y - Min.y) + (box2.Max.y - box2.Min.y));
            if (delta_y < 0)
                delta_y = 0;
            double delta_z = Math.Abs((box2.Min.z + box2.Max.z) - (Min.z + Max.z))
                    - ((Max.z - Min.z) + (box2.Max.z - box2.Min.z));
            if (delta_z < 0)
                delta_z = 0;
            return 0.25 * (delta_x * delta_x + delta_y * delta_y + delta_z * delta_z);
        }


        // [TODO] we have handled corner cases, but not edge cases!
        //   those are 2D, so it would be like (dx > width && dy > height)
        //public double Distance(Vector3d v)
        //{
        //    double dx = (double)Math.Abs(v.x - Center.x);
        //    double dy = (double)Math.Abs(v.y - Center.y);
        //    double dz = (double)Math.Abs(v.z - Center.z);
        //    double fWidth = Width * 0.5;
        //    double fHeight = Height * 0.5;
        //    double fDepth = Depth * 0.5;
        //    if (dx < fWidth && dy < fHeight && dz < Depth)
        //        return 0.0f;
        //    else if (dx > fWidth && dy > fHeight && dz > fDepth)
        //        return (double)Math.Sqrt((dx - fWidth) * (dx - fWidth) + (dy - fHeight) * (dy - fHeight) + (dz - fDepth) * (dz - fDepth));
        //    else if (dx > fWidth)
        //        return dx - fWidth;
        //    else if (dy > fHeight)
        //        return dy - fHeight;
        //    else if (dz > fDepth)
        //        return dz - fDepth;
        //    return 0.0f;
        //}


        //! relative translation
        public void Translate(Vector3d vTranslate) {
            Min.Add(vTranslate);
            Max.Add(vTranslate);
        }

        public void MoveMin(Vector3d vNewMin) {
            Max.x = vNewMin.x + (Max.x - Min.x);
            Max.y = vNewMin.y + (Max.y - Min.y);
            Max.z = vNewMin.z + (Max.z - Min.z);
            Min.Set(vNewMin);
        }
        public void MoveMin(double fNewX, double fNewY, double fNewZ) {
            Max.x = fNewX + (Max.x - Min.x);
            Max.y = fNewY + (Max.y - Min.y);
            Max.z = fNewZ + (Max.z - Min.z);
            Min.Set(fNewX, fNewY, fNewZ);
        }



        public override string ToString() {
            return string.Format("x[{0:F8},{1:F8}] y[{2:F8},{3:F8}] z[{4:F8},{5:F8}]", Min.x, Max.x, Min.y, Max.y, Min.z, Max.z);
        }


        public static implicit operator AxisAlignedBox3d(AxisAlignedBox3f v)
        {
            return new AxisAlignedBox3d(v.Min, v.Max);
        }
        public static explicit operator AxisAlignedBox3f(AxisAlignedBox3d v)
        {
            return new AxisAlignedBox3f((Vector3f)v.Min, (Vector3f)v.Max);
        }



    }
}
