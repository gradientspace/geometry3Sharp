using System;

#if G3_USING_UNITY
using UnityEngine;
#endif

namespace g3
{
    public struct AxisAlignedBox3f : IComparable<AxisAlignedBox3f>, IEquatable<AxisAlignedBox3f>
    {
        public Vector3f Min;
        public Vector3f Max;

        public static readonly AxisAlignedBox3f Empty = new AxisAlignedBox3f(false);
        public static readonly AxisAlignedBox3f Zero = new AxisAlignedBox3f(0);
        public static readonly AxisAlignedBox3f UnitPositive = new AxisAlignedBox3f(1);
        public static readonly AxisAlignedBox3f Infinite =
            new AxisAlignedBox3f(float.MinValue, float.MinValue, float.MinValue, float.MaxValue, float.MaxValue, float.MaxValue);


        public AxisAlignedBox3f(bool bIgnore)
        {
            Min = new Vector3f(float.MaxValue, float.MaxValue, float.MaxValue);
            Max = new Vector3f(float.MinValue, float.MinValue, float.MinValue);
        }

        public AxisAlignedBox3f(float xmin, float ymin, float zmin, float xmax, float ymax, float zmax)
        {
            Min = new Vector3f(xmin, ymin, zmin);
            Max = new Vector3f(xmax, ymax, zmax);
        }

        public AxisAlignedBox3f(float fCubeSize)
        {
            Min = new Vector3f(0, 0, 0);
            Max = new Vector3f(fCubeSize, fCubeSize, fCubeSize);
        }

        public AxisAlignedBox3f(float fWidth, float fHeight, float fDepth)
        {
            Min = new Vector3f(0, 0, 0);
            Max = new Vector3f(fWidth, fHeight, fDepth);
        }

        public AxisAlignedBox3f(Vector3f vMin, Vector3f vMax)
        {
            Min = new Vector3f(Math.Min(vMin.x, vMax.x), Math.Min(vMin.y, vMax.y), Math.Min(vMin.z, vMax.z));
            Max = new Vector3f(Math.Max(vMin.x, vMax.x), Math.Max(vMin.y, vMax.y), Math.Max(vMin.z, vMax.z));
        }

        public AxisAlignedBox3f(Vector3f vCenter, float fHalfWidth, float fHalfHeight, float fHalfDepth)
        {
            Min = new Vector3f(vCenter.x - fHalfWidth, vCenter.y - fHalfHeight, vCenter.z - fHalfDepth);
            Max = new Vector3f(vCenter.x + fHalfWidth, vCenter.y + fHalfHeight, vCenter.z + fHalfDepth);
        }
        public AxisAlignedBox3f(Vector3f vCenter, float fHalfSize)
        {
            Min = new Vector3f(vCenter.x - fHalfSize, vCenter.y - fHalfSize, vCenter.z - fHalfSize);
            Max = new Vector3f(vCenter.x + fHalfSize, vCenter.y + fHalfSize, vCenter.z + fHalfSize);
        }

        public AxisAlignedBox3f(Vector3f vCenter) {
            Min = Max = vCenter;
        }

        public float Width
        {
            get { return Max.x - Min.x; }
        }
        public float Height
        {
            get { return Max.y - Min.y; }
        }
        public float Depth
        {
            get { return Max.z - Min.z; }
        }

        public float Volume
        {
            get { return Width * Height * Depth; }
        }
        public float DiagonalLength
        {
            get {
                return (float)Math.Sqrt((Max.x - Min.x) * (Max.x - Min.x)
              + (Max.y - Min.y) * (Max.y - Min.y) + (Max.z - Min.z) * (Max.z - Min.z));
            }
        }
        public float MaxDim
        {
            get { return Math.Max(Width, Math.Max(Height, Depth)); }
        }

        public Vector3f Diagonal
        {
            get { return new Vector3f(Max.x - Min.x, Max.y - Min.y, Max.z - Min.z); }
        }
        public Vector3f Extents
        {
            get { return new Vector3f((Max.x - Min.x) * 0.5, (Max.y - Min.y) * 0.5, (Max.z - Min.z) * 0.5); }
        }
        public Vector3f Center
        {
            get { return new Vector3f(0.5 * (Min.x + Max.x), 0.5 * (Min.y + Max.y), 0.5 * (Min.z + Max.z)); }
        }


        public static bool operator ==(AxisAlignedBox3f a, AxisAlignedBox3f b) {
            return a.Min == b.Min && a.Max == b.Max;
        }
        public static bool operator !=(AxisAlignedBox3f a, AxisAlignedBox3f b) {
            return a.Min != b.Min || a.Max != b.Max;
        }
        public override bool Equals(object obj) {
            return this == (AxisAlignedBox3f)obj;
        }
        public bool Equals(AxisAlignedBox3f other) {
            return this == other;
        }
        public int CompareTo(AxisAlignedBox3f other) {
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


        // TODO
        ////! 0 == bottom-left, 1 = bottom-right, 2 == top-right, 3 == top-left
        //public Vector3f GetCorner(int i) {
        //    return new Vector3f((i % 3 == 0) ? Min.x : Max.x, (i < 2) ? Min.y : Max.y);
        //}

        //! value is subtracted from min and added to max
        public void Expand(float fRadius)
        {
            Min.x -= fRadius; Min.y -= fRadius; Min.z -= fRadius;
            Max.x += fRadius; Max.y += fRadius; Max.z += fRadius;
        }
        //! value is added to min and subtracted from max
        public void Contract(float fRadius)
        {
            Min.x += fRadius; Min.y += fRadius; Min.z += fRadius;
            Max.x -= fRadius; Max.y -= fRadius; Max.z -= fRadius;
        }

        public void Scale(float sx, float sy, float sz)
        {
            Vector3f c = Center;
            Vector3f e = Extents; e.x *= sx; e.y *= sy; e.z *= sz;
            Min = new Vector3f(c.x - e.x, c.y - e.y, c.z - e.z);
            Max = new Vector3f(c.x + e.x, c.y + e.y, c.z + e.z);
        }

        public void Contain(Vector3f v)
        {
            Min.x = Math.Min(Min.x, v.x);
            Min.y = Math.Min(Min.y, v.y);
            Min.z = Math.Min(Min.z, v.z);
            Max.x = Math.Max(Max.x, v.x);
            Max.y = Math.Max(Max.y, v.y);
            Max.z = Math.Max(Max.z, v.z);
        }

        public void Contain(AxisAlignedBox3f box)
        {
            Contain(box.Min);
            Contain(box.Max);
        }


        public void Contain(Vector3d v)
        {
            Min.x = Math.Min(Min.x, (float)v.x);
            Min.y = Math.Min(Min.y, (float)v.y);
            Min.z = Math.Min(Min.z, (float)v.z);
            Max.x = Math.Max(Max.x, (float)v.x);
            Max.y = Math.Max(Max.y, (float)v.y);
            Max.z = Math.Max(Max.z, (float)v.z);
        }

        public void Contain(AxisAlignedBox3d box)
        {
            Contain(box.Min);
            Contain(box.Max);
        }


        public AxisAlignedBox3f Intersect(AxisAlignedBox3f box)
        {
            AxisAlignedBox3f intersect = new AxisAlignedBox3f(
                Math.Max(Min.x, box.Min.x), Math.Max(Min.y, box.Min.y), Math.Max(Min.z, box.Min.z),
                Math.Min(Max.x, box.Max.x), Math.Min(Max.y, box.Max.y), Math.Min(Max.z, box.Max.z));
            if (intersect.Height <= 0 || intersect.Width <= 0 || intersect.Depth <= 0)
                return AxisAlignedBox3f.Empty;
            else
                return intersect;
        }



        public bool Contains(Vector3f v)
        {
            return (Min.x <= v.x) && (Min.y <= v.y) && (Min.z <= v.z)
                && (Max.x >= v.x) && (Max.y >= v.y) && (Max.z >= v.z);
        }
        public bool Intersects(AxisAlignedBox3f box)
        {
            return !((box.Max.x <= Min.x) || (box.Min.x >= Max.x)
                || (box.Max.y <= Min.y) || (box.Min.y >= Max.y)
                || (box.Max.z <= Min.z) || (box.Min.z >= Max.z));
        }


        public double DistanceSquared(Vector3f v)
        {
            float dx = (v.x < Min.x) ? Min.x - v.x : (v.x > Max.x ? v.x - Max.x : 0);
            float dy = (v.y < Min.y) ? Min.y - v.y : (v.y > Max.y ? v.y - Max.y : 0);
            float dz = (v.z < Min.z) ? Min.z - v.z : (v.z > Max.z ? v.z - Max.z : 0);
            return dx * dx + dy * dy + dz * dz;
        }
        public float Distance(Vector3f v)
        {
            return (float)Math.Sqrt(DistanceSquared(v));
        }



        //! relative translation
        public void Translate(Vector3f vTranslate)
        {
            Min.Add(vTranslate);
            Max.Add(vTranslate);
        }

        public void MoveMin(Vector3f vNewMin)
        {
            Max.x = vNewMin.x + (Max.x - Min.x);
            Max.y = vNewMin.y + (Max.y - Min.y);
            Max.z = vNewMin.z + (Max.z - Min.z);
            Min.Set(vNewMin);
        }
        public void MoveMin(float fNewX, float fNewY, float fNewZ)
        {
            Max.x = fNewX + (Max.x - Min.x);
            Max.y = fNewY + (Max.y - Min.y);
            Max.z = fNewZ + (Max.z - Min.z);
            Min.Set(fNewX, fNewY, fNewZ);
        }



        public override string ToString()
        {
            return string.Format("x[{0:F8},{1:F8}] y[{2:F8},{3:F8}] z[{4:F8},{5:F8}]", Min.x, Max.x, Min.y, Max.y, Min.z, Max.z);
        }



#if G3_USING_UNITY
        public static implicit operator AxisAlignedBox3f(UnityEngine.Bounds b)
        {
            return new AxisAlignedBox3f(b.min, b.max);
        }
        public static implicit operator UnityEngine.Bounds(AxisAlignedBox3f b)
        {
            UnityEngine.Bounds ub = new Bounds();
            ub.SetMinMax(b.Min, b.Max);
            return ub;
        }
#endif


    }
}
