using System;

namespace g3
{
    /// <summary>
    /// Matrix2f is a float-storage variant of Matrix2d, intended to be used for serialization and buffer storage (to reduce memory usage)
    /// Matrix2d should be used for any geometric calculations.
    /// </summary>
    public class Matrix2f
    {
        public float m00, m01, m10, m11;

        public static readonly Matrix2f Identity = new Matrix2f(true);
        public static readonly Matrix2f Zero = new Matrix2f(false);
        public static readonly Matrix2f One = new Matrix2f(1, 1, 1, 1);

        public Matrix2f(bool bIdentity)
        {
            if (bIdentity) {
                m00 = m11 = 1;
                m01 = m10 = 0;
            } else
                m00 = m01 = m10 = m11 = 0;
        }
        public Matrix2f(float m00, float m01, float m10, float m11) {
            this.m00 = m00; this.m01 = m01; this.m10 = m10; this.m11 = m11;
        }
        public Matrix2f(float m00, float m11) {
            this.m00 = m00; this.m11 = m11; this.m01 = this.m10 = 0;
        }

        // Create a tensor product U*V^T.
        public Matrix2f(Vector2f u, Vector2f v) {
            m00 = u.x * v.x;
            m01 = u.x * v.y;
            m10 = u.y * v.x;
            m11 = u.y * v.y;
        }
    }
}
