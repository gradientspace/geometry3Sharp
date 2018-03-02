using System;
using System.Collections.Generic;



namespace g3
{
    public struct AxisAlignedBox2i : IComparable<AxisAlignedBox2i>, IEquatable<AxisAlignedBox2i>
    {
        public Vector2i Min;
        public Vector2i Max;

        public static readonly AxisAlignedBox2i Empty = new AxisAlignedBox2i(false);
        public static readonly AxisAlignedBox2i Zero = new AxisAlignedBox2i(0);
        public static readonly AxisAlignedBox2i UnitPositive = new AxisAlignedBox2i(1);
        public static readonly AxisAlignedBox2i Infinite =
            new AxisAlignedBox2i(int.MinValue, int.MinValue, int.MaxValue, int.MaxValue);


        public AxisAlignedBox2i(bool bIgnore)
        {
            Min = new Vector2i(int.MaxValue, int.MaxValue);
            Max = new Vector2i(int.MinValue, int.MinValue);
        }

        public AxisAlignedBox2i(int xmin, int ymin, int xmax, int ymax)
        {
            Min = new Vector2i(xmin, ymin);
            Max = new Vector2i(xmax, ymax);
        }

        public AxisAlignedBox2i(int fCubeSize)
        {
            Min = new Vector2i(0, 0);
            Max = new Vector2i(fCubeSize, fCubeSize);
        }

        public AxisAlignedBox2i(int fWidth, int fHeight)
        {
            Min = new Vector2i(0, 0);
            Max = new Vector2i(fWidth, fHeight);
        }

        public AxisAlignedBox2i(Vector2i vMin, Vector2i vMax)
        {
            Min = new Vector2i(Math.Min(vMin.x, vMax.x), Math.Min(vMin.y, vMax.y));
            Max = new Vector2i(Math.Max(vMin.x, vMax.x), Math.Max(vMin.y, vMax.y));
        }

        public AxisAlignedBox2i(Vector2i vCenter, int fHalfWidth, int fHalfHeight, int fHalfDepth)
        {
            Min = new Vector2i(vCenter.x - fHalfWidth, vCenter.y - fHalfHeight);
            Max = new Vector2i(vCenter.x + fHalfWidth, vCenter.y + fHalfHeight);
        }
        public AxisAlignedBox2i(Vector2i vCenter, int fHalfSize)
        {
            Min = new Vector2i(vCenter.x - fHalfSize, vCenter.y - fHalfSize);
            Max = new Vector2i(vCenter.x + fHalfSize, vCenter.y + fHalfSize);
        }

        public AxisAlignedBox2i(Vector2i vCenter) {
            Min = Max = vCenter;
        }

        public int Width {
            get { return Math.Max(Max.x - Min.x, 0); }
        }
        public int Height {
            get { return Math.Max(Max.y - Min.y, 0); }
        }


        public int Area
        {
            get { return Width * Height; }
        }
        public int DiagonalLength
        {
            get {
                return (int)Math.Sqrt((Max.x-Min.x)*(Max.x-Min.x) + (Max.y-Min.y)*(Max.y-Min.y));
            }
        }
        public int MaxDim
        {
            get { return Math.Max(Width, Height); }
        }

        public Vector2i Diagonal
        {
            get { return new Vector2i(Max.x - Min.x, Max.y - Min.y); }
        }
        public Vector2i Extents
        {
            get { return new Vector2i((Max.x - Min.x)/2, (Max.y - Min.y)/2); }
        }
        public Vector2i Center
        {
            get { return new Vector2i((Min.x + Max.x)/2, (Min.y + Max.y)/2); }
        }


        public static bool operator ==(AxisAlignedBox2i a, AxisAlignedBox2i b) {
            return a.Min == b.Min && a.Max == b.Max;
        }
        public static bool operator !=(AxisAlignedBox2i a, AxisAlignedBox2i b) {
            return a.Min != b.Min || a.Max != b.Max;
        }
        public override bool Equals(object obj) {
            return this == (AxisAlignedBox2i)obj;
        }
        public bool Equals(AxisAlignedBox2i other) {
            return this == other;
        }
        public int CompareTo(AxisAlignedBox2i other) {
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


        //! 0 == bottom-left, 1 = bottom-right, 2 == top-right, 3 == top-left
        public Vector2i GetCorner(int i)
        {
            return new Vector2i((i % 3 == 0) ? Min.x : Max.x, (i < 2) ? Min.y : Max.y);
        }

        //! value is subtracted from min and added to max
        public void Expand(int nRadius)
        {
            Min.x -= nRadius; Min.y -= nRadius;
            Max.x += nRadius; Max.y += nRadius;
        }
        //! value is added to min and subtracted from max
        public void Contract(int nRadius)
        {
            Min.x += nRadius; Min.y += nRadius;
            Max.x -= nRadius; Max.y -= nRadius;
        }

        public void Scale(int sx, int sy, int sz)
        {
            Vector2i c = Center;
            Vector2i e = Extents; e.x *= sx; e.y *= sy; 
            Min = new Vector2i(c.x - e.x, c.y - e.y);
            Max = new Vector2i(c.x + e.x, c.y + e.y);
        }

        public void Contain(Vector2i v)
        {
            Min.x = Math.Min(Min.x, v.x);
            Min.y = Math.Min(Min.y, v.y);
            Max.x = Math.Max(Max.x, v.x);
            Max.y = Math.Max(Max.y, v.y);
        }

        public void Contain(AxisAlignedBox2i box)
        {
            Min.x = Math.Min(Min.x, box.Min.x);
            Min.y = Math.Min(Min.y, box.Min.y);
            Max.x = Math.Max(Max.x, box.Max.x);
            Max.y = Math.Max(Max.y, box.Max.y);
        }


        public void Contain(Vector3d v)
        {
            Min.x = Math.Min(Min.x, (int)v.x);
            Min.y = Math.Min(Min.y, (int)v.y);
            Max.x = Math.Max(Max.x, (int)v.x);
            Max.y = Math.Max(Max.y, (int)v.y);
        }

        public void Contain(AxisAlignedBox3d box)
        {
            Min.x = Math.Min(Min.x, (int)box.Min.x);
            Min.y = Math.Min(Min.y, (int)box.Min.y);
            Max.x = Math.Max(Max.x, (int)box.Max.x);
            Max.y = Math.Max(Max.y, (int)box.Max.y);
        }


        public AxisAlignedBox2i Intersect(AxisAlignedBox2i box)
        {
            AxisAlignedBox2i intersect = new AxisAlignedBox2i(
                Math.Max(Min.x, box.Min.x), Math.Max(Min.y, box.Min.y), 
                Math.Min(Max.x, box.Max.x), Math.Min(Max.y, box.Max.y) );
            if (intersect.Height <= 0 || intersect.Width <= 0)
                return AxisAlignedBox2i.Empty;
            else
                return intersect;
        }



        public bool Contains(Vector2i v) {
            return (Min.x <= v.x) && (Min.y <= v.y)
                && (Max.x >= v.x) && (Max.y >= v.y);
        }
        public bool Contains(ref Vector2i v) {
            return (Min.x <= v.x) && (Min.y <= v.y)
                && (Max.x >= v.x) && (Max.y >= v.y);
        }


        public bool Contains(AxisAlignedBox2i box) {
            return Contains(ref box.Min) && Contains(ref box.Max);
        }
        public bool Contains(ref AxisAlignedBox2i box) {
            return Contains(ref box.Min) && Contains(ref box.Max);
        }



        public bool Intersects(AxisAlignedBox2i box)
        {
            return !((box.Max.x <= Min.x) || (box.Min.x >= Max.x)
                || (box.Max.y <= Min.y) || (box.Min.y >= Max.y));
        }


        public double DistanceSquared(Vector2i v)
        {
            int dx = (v.x < Min.x) ? Min.x - v.x : (v.x > Max.x ? v.x - Max.x : 0);
            int dy = (v.y < Min.y) ? Min.y - v.y : (v.y > Max.y ? v.y - Max.y : 0);
            return dx * dx + dy * dy;
        }
        public int Distance(Vector2i v)
        {
            return (int)Math.Sqrt(DistanceSquared(v));
        }


        public Vector2i NearestPoint(Vector2i v)
        {
            int x = (v.x < Min.x) ? Min.x : (v.x > Max.x ? Max.x : v.x);
            int y = (v.y < Min.y) ? Min.y : (v.y > Max.y ? Max.y : v.y);
            return new Vector2i(x, y);
        }



        //! relative translation
        public void Translate(Vector2i vTranslate)
        {
            Min += vTranslate;
            Max += vTranslate;
        }

        public void MoveMin(Vector2i vNewMin)
        {
            Max.x = vNewMin.x + (Max.x - Min.x);
            Max.y = vNewMin.y + (Max.y - Min.y);
            Min = vNewMin;
        }
        public void MoveMin(int fNewX, int fNewY)
        {
            Max.x = fNewX + (Max.x - Min.x);
            Max.y = fNewY + (Max.y - Min.y);
            Min= new Vector2i(fNewX, fNewY);
        }




        public IEnumerable<Vector2i> IndicesInclusive() {
            for (int yi = Min.y; yi <= Max.y; ++yi) {
                for (int xi = Min.x; xi <= Max.x; ++xi)
                    yield return new Vector2i(xi, yi);
            }
        }
        public IEnumerable<Vector2i> IndicesExclusive() {
            for (int yi = Min.y; yi < Max.y; ++yi) {
                for (int xi = Min.x; xi < Max.x; ++xi)
                    yield return new Vector2i(xi, yi);
            }
        }


        public override string ToString()
        {
            return string.Format("x[{0},{1}] y[{2},{3}]", Min.x, Max.x, Min.y, Max.y);
        }




    }
}
