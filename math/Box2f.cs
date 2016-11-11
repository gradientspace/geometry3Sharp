using System;

namespace g3
{
    public struct Box2f
    {
        public Vector2f Min;
        public Vector2f Max;

        public static Box2f Empty = new Box2f(false);
        public static Box2f Infinite = new Box2f(Single.MinValue, Single.MinValue, Single.MaxValue, Single.MaxValue);


        public Box2f(bool bIgnore) {
            Min = new Vector2f(Single.MaxValue, Single.MaxValue);
            Max = new Vector2f(Single.MinValue, Single.MinValue);
        }

        public Box2f(float xmin, float ymin, float xmax, float ymax) {
            Min = new Vector2f(xmin, ymin);
            Max = new Vector2f(xmax, ymax);
        }

        public Box2f(float fSquareSize) {
            Min = new Vector2f(0, 0);
            Max = new Vector2f(fSquareSize, fSquareSize);
        }

        public Box2f(float fWidth, float fHeight) {
            Min = new Vector2f(0, 0);
            Max = new Vector2f(fWidth, fHeight);
        }

        public Box2f(Vector2f vMin, Vector2f vMax) {
            Min = new Vector2f(Math.Min(vMin.x, vMax.x), Math.Min(vMin.y, vMax.y));
            Max = new Vector2f(Math.Max(vMin.x, vMax.x), Math.Max(vMin.y, vMax.y));
        }

        public Box2f(Vector2f vCenter, float fHalfWidth, float fHalfHeight) {
            Min = new Vector2f(vCenter.x - fHalfWidth, vCenter.y - fHalfHeight);
            Max = new Vector2f(vCenter.x + fHalfWidth, vCenter.y + fHalfHeight);
        }
        public Box2f(Vector2f vCenter, float fHalfWidth) {
            Min = new Vector2f(vCenter.x - fHalfWidth, vCenter.y - fHalfWidth);
            Max = new Vector2f(vCenter.x + fHalfWidth, vCenter.y + fHalfWidth);
        }

        public Box2f(Box2f o) {
            Min = new Vector2f(o.Min);
            Max = new Vector2f(o.Max);
        }

        public float Width {
            get { return Max.x - Min.x; }
        }
        public float Height {
            get { return Max.y - Min.y; }
        }

        public float Area {
            get { return Width * Height; }
        }
        public float DiagonalLength {
            get { return (float)Math.Sqrt((Max.x - Min.x) * (Max.x - Min.x) + (Max.y - Min.y) * (Max.y - Min.y)); }
        }
        public float MaxDim {
            get { return Math.Max(Width, Height); }
        }

        public Vector2f Diagonal
        {
            get { return new Vector2f(Max.x - Min.x, Max.y - Min.y); }
        }
        public Vector2f Center {
            get { return new Vector2f(0.5f * (Min.x + Max.x), 0.5f * (Min.y + Max.y)); }
        }

        //! 0 == bottom-left, 1 = bottom-right, 2 == top-right, 3 == top-left
        public Vector2f GetCorner(int i) {
            return new Vector2f((i % 3 == 0) ? Min.x : Max.x, (i < 2) ? Min.y : Max.y);
        }

        //! value is subtracted from min and added to max
        public void Expand(float fRadius) {
            Min.x -= fRadius; Min.y -= fRadius;
            Max.x += fRadius; Max.y += fRadius;
        }
        //! value is added to min and subtracted from max
        public void Contract(float fRadius) {
            Min.x += fRadius; Min.y += fRadius;
            Max.x -= fRadius; Max.y -= fRadius;
        }
        // values are all added
        public void Pad(float fPadLeft, float fPadRight, float fPadBottom, float fPadTop) {
            Min.x += fPadLeft; Min.y += fPadBottom;
            Max.x += fPadRight; Max.y += fPadTop;
        }

        public enum ScaleMode {
            ScaleRight,
            ScaleLeft,
            ScaleUp,
            ScaleDown,
            ScaleCenter
        }
        public void SetWidth( float fNewWidth, ScaleMode eScaleMode ) {
            switch (eScaleMode) {
                case ScaleMode.ScaleLeft:
                    Min.x = Max.x - fNewWidth;
                    break;
                case ScaleMode.ScaleRight:
                    Max.x = Min.x + fNewWidth;
                    break;
                case ScaleMode.ScaleCenter:
                    Vector2f vCenter = Center;
                    Min.x = vCenter.x - 0.5f * fNewWidth;
                    Max.x = vCenter.x + 0.5f * fNewWidth;
                    break;
                default:
                    throw new Exception("Invalid scale mode...");
            }
        }
        public void SetHeight(float fNewHeight, ScaleMode eScaleMode) {
            switch (eScaleMode) {
                case ScaleMode.ScaleDown:
                    Min.y = Max.y - fNewHeight;
                    break;
                case ScaleMode.ScaleUp:
                    Max.y = Min.y + fNewHeight;
                    break;
                case ScaleMode.ScaleCenter:
                    Vector2f vCenter = Center;
                    Min.y = vCenter.y - 0.5f * fNewHeight;
                    Max.y = vCenter.y + 0.5f * fNewHeight;
                    break;
                default:
                    throw new Exception("Invalid scale mode...");
            }
        }

        public void Contain(Vector2f v) {
            Min.x = Math.Min(Min.x, v.x);
            Min.y = Math.Min(Min.y, v.y);
            Max.x = Math.Max(Max.x, v.x);
            Max.y = Math.Max(Max.y, v.y);
        }

        public void Contain(Box2f box) {
            Contain(box.Min);
            Contain(box.Max);
        }

        public Box2f Intersect(Box2f box) {
            Box2f intersect = new Box2f(
                Math.Max(Min.x, box.Min.x), Math.Max(Min.y, box.Min.y),
                Math.Min(Max.x, box.Max.x), Math.Min(Max.y, box.Max.y));
            if (intersect.Height <= 0 || intersect.Width <= 0)
                return Box2f.Empty;
            else
                return intersect;
        }



        public bool Contains(Vector2f v) {
            return (Min.x < v.x) && (Min.y < v.y) && (Max.x > v.x) && (Max.y > v.y);
        }
        public bool Intersects(Box2f box) {
            return !((box.Max.x < Min.x) || (box.Min.x > Max.x) || (box.Max.y < Min.y) || (box.Min.y > Max.y));
        }



        public float Distance(Vector2f v)
        {
            float dx = (float)Math.Abs(v.x - Center.x);
            float dy = (float)Math.Abs(v.y - Center.y);
            float fWidth = Width * 0.5f;
            float fHeight = Height * 0.5f;
            if (dx < fWidth && dy < fHeight)
                return 0.0f;
            else if (dx > fWidth && dy > fHeight)
                return (float)Math.Sqrt((dx - fWidth) * (dx - fWidth) + (dy - fHeight) * (dy - fHeight));
            else if (dx > fWidth)
                return dx - fWidth;
            else if (dy > fHeight)
                return dy - fHeight;
            return 0.0f;
        }


        //! relative translation
        public void Translate(Vector2f vTranslate) {
            Min.Add(vTranslate);
            Max.Add(vTranslate);
        }

        public void MoveMin(Vector2f vNewMin) {
            Max.x = vNewMin.x + (Max.x - Min.x);
            Max.y = vNewMin.y + (Max.y - Min.y);
            Min.Set(vNewMin);
        }
        public void MoveMin(float fNewX, float fNewY) {
            Max.x = fNewX + (Max.x - Min.x);
            Max.y = fNewY + (Max.y - Min.y);
            Min.Set(fNewX, fNewY);
        }



        public override string ToString() {
            return string.Format("[{0:F8},{1:F8}] [{2:F8},{3:F8}]", Min.x, Max.x, Min.y, Max.y);
        }


    }
}
