// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;

namespace g3
{
    public struct Matrix4d
    {
        public Vector4d Row0;
        public Vector4d Row1;
        public Vector4d Row2;
        public Vector4d Row3;

        public static readonly Matrix4d Identity = new Matrix4d(true);
        public static readonly Matrix4d Zero = new Matrix4d(false);

        public Matrix4d(bool bIdentity) {
            if (bIdentity) {
                Row0 = new Vector4d(1, 0, 0, 0);
                Row1 = new Vector4d(0, 1, 0, 0);
                Row2 = new Vector4d(0, 0, 1, 0);
                Row3 = new Vector4d(0, 0, 0, 1);
            } else {
                Row0 = Row1 = Row2 = Row3 = Vector4d.Zero;
            }
        }

        // assumes input is row-major...
        public Matrix4d(float[,] mat) {
            Row0 = new Vector4d(mat[0, 0], mat[0, 1], mat[0, 2], mat[0, 3]);
            Row1 = new Vector4d(mat[1, 0], mat[1, 1], mat[1, 2], mat[1, 3]);
            Row2 = new Vector4d(mat[2, 0], mat[2, 1], mat[2, 2], mat[2, 3]);
            Row3 = new Vector4d(mat[3, 0], mat[3, 1], mat[3, 2], mat[3, 3]);
        }
        public Matrix4d(float[] mat) {
            Row0 = new Vector4d(mat[0], mat[1], mat[2], mat[3]);
            Row1 = new Vector4d(mat[4], mat[5], mat[6], mat[7]);
            Row2 = new Vector4d(mat[8], mat[9], mat[10], mat[11]);
            Row3 = new Vector4d(mat[12], mat[13], mat[14], mat[15]);
        }
        public Matrix4d(Span<float> mat) {
            Row0 = new Vector4d(mat[0], mat[1], mat[2], mat[3]);
            Row1 = new Vector4d(mat[4], mat[5], mat[6], mat[7]);
            Row2 = new Vector4d(mat[8], mat[9], mat[10], mat[11]);
            Row3 = new Vector4d(mat[12], mat[13], mat[14], mat[15]);
        }
        public Matrix4d(double[,] mat) {
            Row0 = new Vector4d(mat[0, 0], mat[0, 1], mat[0, 2], mat[0, 3]);
            Row1 = new Vector4d(mat[1, 0], mat[1, 1], mat[1, 2], mat[1, 3]);
            Row2 = new Vector4d(mat[2, 0], mat[2, 1], mat[2, 2], mat[2, 3]);
            Row3 = new Vector4d(mat[3, 0], mat[3, 1], mat[3, 2], mat[3, 3]);
        }
        public Matrix4d(double[] mat) {
            Row0 = new Vector4d(mat[0], mat[1], mat[2], mat[3]);
            Row1 = new Vector4d(mat[4], mat[5], mat[6], mat[7]);
            Row2 = new Vector4d(mat[8], mat[9], mat[10], mat[11]);
            Row3 = new Vector4d(mat[12], mat[13], mat[14], mat[15]);
        }
        public Matrix4d(Span<double> mat) {
            Row0 = new Vector4d(mat[0], mat[1], mat[2], mat[3]);
            Row1 = new Vector4d(mat[4], mat[5], mat[6], mat[7]);
            Row2 = new Vector4d(mat[8], mat[9], mat[10], mat[11]);
            Row3 = new Vector4d(mat[12], mat[13], mat[14], mat[15]);
        }
        public Matrix4d(Func<int,double> matBufferF)
        {
            Row0 = new Vector4d(matBufferF(0), matBufferF(1), matBufferF(2), matBufferF(3));
            Row1 = new Vector4d(matBufferF(4), matBufferF(5), matBufferF(6), matBufferF(7));
            Row2 = new Vector4d(matBufferF(8), matBufferF(9), matBufferF(10), matBufferF(11));
            Row3 = new Vector4d(matBufferF(12), matBufferF(13), matBufferF(14), matBufferF(15));
        }
        public Matrix4d(Func<int, int, double> matF)
        {
            Row0 = new Vector4d(matF(0,0), matF(0,1), matF(0,2), matF(0,3));
            Row1 = new Vector4d(matF(1,0), matF(1,1), matF(1,2), matF(1,3));
            Row2 = new Vector4d(matF(2,0), matF(2,1), matF(2,2), matF(2,3));
            Row3 = new Vector4d(matF(3,0), matF(3,1), matF(3,2), matF(3,3));
        }
        public Matrix4d(double m00, double m11, double m22, double m33)
        {
            Row0 = new Vector4d(m00, 0, 0, 0);
            Row1 = new Vector4d(0, m11, 0, 0);
            Row2 = new Vector4d(0, 0, m22, 0);
            Row3 = new Vector4d(0, 0, 0, m33);
        }
        public Matrix4d(Vector4d v0, Vector4d v1, Vector4d v2, Vector4d v3, bool bRows)
        {
            if (bRows) {
                Row0 = v0; Row1 = v1; Row2 = v2; Row3 = v3;
            } else {
                Row0 = new Vector4d(v0.x, v1.x, v2.x, v3.x);
                Row1 = new Vector4d(v0.y, v1.y, v2.y, v3.y);
                Row2 = new Vector4d(v0.z, v1.z, v2.z, v3.z);
                Row3 = new Vector4d(v0.w, v1.w, v2.w, v3.w);
            }
        }
        public Matrix4d(ref readonly Vector4d v0, ref readonly Vector4d v1, ref readonly Vector4d v2, ref readonly Vector4d v3, bool bRows)
        {
            if (bRows) {
                Row0 = v0; Row1 = v1; Row2 = v2; Row3 = v3;
            } else {
                Row0 = new Vector4d(v0.x, v1.x, v2.x, v3.x);
                Row1 = new Vector4d(v0.y, v1.y, v2.y, v3.y);
                Row2 = new Vector4d(v0.z, v1.z, v2.z, v3.z);
                Row3 = new Vector4d(v0.w, v1.w, v2.w, v3.w);
            }
        }
        public Matrix4d(ref readonly Matrix3d mat3x3)
        {
            Row0 = new Vector4d(mat3x3.Row0.x, mat3x3.Row0.y, mat3x3.Row0.z, 0);
            Row1 = new Vector4d(mat3x3.Row1.x, mat3x3.Row1.y, mat3x3.Row1.z, 0);
            Row2 = new Vector4d(mat3x3.Row2.x, mat3x3.Row2.y, mat3x3.Row2.z, 0);
            Row3 = new Vector4d(0, 0, 0, 1);
        }
        public Matrix4d(ref readonly Matrix3d mat3x3, ref readonly Vector3d Translation)
        {
            Row0 = new Vector4d(mat3x3.Row0.x, mat3x3.Row0.y, mat3x3.Row0.z, Translation.x);
            Row1 = new Vector4d(mat3x3.Row1.x, mat3x3.Row1.y, mat3x3.Row1.z, Translation.y);
            Row2 = new Vector4d(mat3x3.Row2.x, mat3x3.Row2.y, mat3x3.Row2.z, Translation.z);
            Row3 = new Vector4d(0, 0, 0, 1);
        }

        /// <summary>
        /// Construct outer-product of u*transpose(v) of u and v
        /// result is that Mij = u_i * v_j
        /// </summary>
        public Matrix4d(ref readonly Vector4d u, ref readonly Vector4d v)
        {
            Row0 = new Vector4d(u.x*v.x, u.x*v.y, u.x*v.z, u.x*v.w);
            Row1 = new Vector4d(u.y*v.x, u.y*v.y, u.y*v.z, u.y*v.w);
            Row2 = new Vector4d(u.z*v.x, u.z*v.y, u.z*v.z, u.z*v.w);
            Row2 = new Vector4d(u.w*v.x, u.w*v.y, u.w*v.z, u.w*v.w);
        }


        public double this[int r, int c] {
            get {
                if (r == 0)         return Row0[c];
                else if (r == 1)    return Row1[c];
                else if (r == 2)    return Row2[c];
                else                return Row3[c];
            }
            set {
                if (r == 0)         Row0[c] = value;
                else if (r == 1)    Row1[c] = value;
                else if (r == 2)    Row2[c] = value;
                else                Row3[c] = value;
            }
        }


        public double this[int i] {
            get {
                if (i > 11)        return Row3[i%4];
                else if (i > 7)    return Row2[i%4];
                else if (i > 3)    return Row1[i%4];
                else               return Row0[i%4];
            }
            set {
                if (i > 11)        Row3[i%4] = value;
                else if (i > 7)    Row2[i%4] = value;
                else if (i > 3)    Row1[i%4] = value;
                else               Row0[i%4] = value;
            }
        }


        public readonly Vector4d Row(int i) {
            return (i == 0) ? Row0 : ((i == 1) ? Row1 : ((i == 2) ? Row2 : Row3));
        }
        public readonly Vector4d Column(int i) {
            if (i == 0)         return new Vector4d(Row0.x, Row1.x, Row2.x, Row3.x);
            else if (i == 1)    return new Vector4d(Row0.y, Row1.y, Row2.y, Row3.y);
            else if (i == 2)    return new Vector4d(Row0.z, Row1.z, Row2.z, Row3.z);
            else                return new Vector4d(Row0.w, Row1.w, Row2.w, Row3.w);
        }


        public readonly void GetColumns(out Vector4d Column0, out Vector4d Column1, out Vector4d Column2, out Vector4d Column3)
        {
            Column0 = new Vector4d(Row0.x, Row1.x, Row2.x, Row3.x);
            Column1 = new Vector4d(Row0.y, Row1.y, Row2.y, Row3.y);
            Column2 = new Vector4d(Row0.z, Row1.z, Row2.z, Row3.z);
            Column3 = new Vector4d(Row0.w, Row1.w, Row2.w, Row3.w);
        }


        public readonly double[] ToBuffer() {
            return new double[16] {
                Row0.x, Row0.y, Row0.z, Row0.w,
                Row1.x, Row1.y, Row1.z, Row1.w,
                Row2.x, Row2.y, Row2.z, Row2.w,
                Row3.x, Row3.y, Row3.z, Row3.w };
        }
        public readonly void ToBuffer(double[] buf) {
            buf[0] = Row0.x; buf[1] = Row0.y; buf[2] = Row0.z; buf[3] = Row0.w;
            buf[4] = Row1.x; buf[5] = Row1.y; buf[6] = Row1.z; buf[7] = Row1.w;
            buf[8] = Row2.x; buf[9] = Row2.y; buf[10] = Row2.z; buf[11] = Row2.w;
            buf[12] = Row3.x; buf[13] = Row3.y; buf[14] = Row3.z; buf[15] = Row3.w;
        }




        public static Matrix4d operator *(Matrix4d mat, double f) {
            return new Matrix4d(mat.Row0 * f, mat.Row1 * f, mat.Row2 * f, mat.Row3 * f, true);
        }
        public static Matrix4d operator *(double f, Matrix4d mat) {
            return new Matrix4d(mat.Row0 * f, mat.Row1 * f, mat.Row2 * f, mat.Row3 * f, true);
        }


        public static Vector4d operator *(Matrix4d mat, Vector4d v) {
            return new Vector4d(mat.Row0.Dot(v), mat.Row1.Dot(v), mat.Row2.Dot(v), mat.Row3.Dot(v));
        }

        public readonly Vector4d Multiply(Vector4d v) {
            return new Vector4d(Row0.Dot(v), Row1.Dot(v), Row2.Dot(v), Row3.Dot(v));
        }

        public readonly void Multiply(Vector4d v, out Vector4d vOut) {
            vOut = new Vector4d(
                Row0.x * v.x + Row0.y * v.y + Row0.z * v.z + Row0.w * v.w,
                Row1.x * v.x + Row1.y * v.y + Row1.z * v.z + Row1.w * v.w,
                Row2.x * v.x + Row2.y * v.y + Row2.z * v.z + Row2.w * v.w,
                Row3.x * v.x + Row3.y * v.y + Row3.z * v.z + Row3.w * v.w );
        }

        public readonly Vector3d TransformPointAffine(Vector3d v)
        {
            Vector4d vv = new(v.x, v.y, v.z, 1.0);
            return new Vector3d(Row0.Dot(vv), Row1.Dot(vv), Row2.Dot(vv));
        }
        public readonly Vector3f TransformPointAffine(Vector3f v)
        {
            Vector4d vv = new(v.x, v.y, v.z, 1.0);
            return new Vector3f(Row0.Dot(vv), Row1.Dot(vv), Row2.Dot(vv));
        }
        public readonly Vector3d TransformVectorAffine(Vector3d v)
        {
            Vector4d vv = new(v.x, v.y, v.z, 0.0);
            return new Vector3d(Row0.Dot(vv), Row1.Dot(vv), Row2.Dot(vv));
        }
        public readonly Vector3f TransformVectorAffine(Vector3f v)
        {
            Vector4d vv = new(v.x, v.y, v.z, 0.0);
            return new Vector3f(Row0.Dot(vv), Row1.Dot(vv), Row2.Dot(vv));
        }

        public static Matrix4d operator *(Matrix4d mat1, Matrix4d mat2)
		{
            mat2.GetColumns(out Vector4d Col0, out Vector4d Col1, out Vector4d Col2, out Vector4d Col3);
            Vector4d row0 = new Vector4d(mat1.Row0.Dot(Col0), mat1.Row0.Dot(Col1), mat1.Row0.Dot(Col2), mat1.Row0.Dot(Col3));
            Vector4d row1 = new Vector4d(mat1.Row1.Dot(Col0), mat1.Row1.Dot(Col1), mat1.Row1.Dot(Col2), mat1.Row1.Dot(Col3));
            Vector4d row2 = new Vector4d(mat1.Row2.Dot(Col0), mat1.Row2.Dot(Col1), mat1.Row2.Dot(Col2), mat1.Row2.Dot(Col3));
            Vector4d row3 = new Vector4d(mat1.Row3.Dot(Col0), mat1.Row3.Dot(Col1), mat1.Row3.Dot(Col2), mat1.Row3.Dot(Col3));
            return new Matrix4d(row0, row1, row2, row3, true);
		}



        public static Matrix4d operator +(Matrix4d mat1, Matrix4d mat2) {
            return new Matrix4d(
                mat1.Row0 + mat2.Row0, 
                mat1.Row1 + mat2.Row1, 
                mat1.Row2 + mat2.Row2, 
                mat1.Row3 + mat2.Row3, true);
        }
        public static Matrix4d operator -(Matrix4d mat1, Matrix4d mat2) {
            return new Matrix4d(
                mat1.Row0 - mat2.Row0, 
                mat1.Row1 - mat2.Row1, 
                mat1.Row2 - mat2.Row2, 
                mat1.Row3 - mat2.Row3, true);
        }



        public readonly double InnerProduct(ref readonly Matrix4d m2)
        {
            return Row0.Dot(in m2.Row0) + Row1.Dot(in m2.Row1) + Row2.Dot(in m2.Row2) + Row3.Dot(in m2.Row3);
        }



        public readonly double Determinant() {
            throw new NotImplementedException();
            //double a11 = Row0.x, a12 = Row0.y, a13 = Row0.z, a21 = Row1.x, a22 = Row1.y, a23 = Row1.z, a31 = Row2.x, a32 = Row2.y, a33 = Row2.z;
            //double i00 = a33 * a22 - a32 * a23;
            //double i01 = -(a33 * a12 - a32 * a13);
            //double i02 = a23 * a12 - a22 * a13;
            //return a11 * i00 + a21 * i01 + a31 * i02;
		}


        public readonly bool IsAffine {
            get { return Row3 == Vector4d.UnitW; }
        }

        public readonly bool IsIdentity {
            get {  return Row0 == Vector4d.UnitX && Row1 == Vector4d.UnitY && Row2 == Vector4d.UnitZ && Row3 == Vector4d.UnitW; }
        }


        public readonly Matrix4d Inverse() {
            if ( IsAffine ) {
                // can invert more efficiently
                // https://stackoverflow.com/questions/2624422/efficient-4x4-matrix-inverse-affine-transform
                throw new NotImplementedException();
            } 
            else 
            {
                throw new NotImplementedException();
            }
        }

        public readonly Matrix4d Transpose()
        {
            return new Matrix4d(in Row0, in Row1, in Row2, in Row3, false);
        }


        public readonly Matrix3d GetAffineTransform() 
        {
            return new Matrix3d(
                Row0.x, Row0.y, Row0.z,
                Row1.x, Row1.y, Row1.z,
                Row2.x, Row2.y, Row2.z);
        }
        public readonly Vector3d GetAffineTranslation()
        {
            return new Vector3d(Row0.w, Row1.w, Row2.w);
        }


        public readonly bool GetAffineNormalTransform(out Matrix3d NormalTransform)
        {
            if (IsAffine == false) {
                NormalTransform = Matrix3d.Identity;
                return false;
            }
            Matrix3d XForm = GetAffineTransform();
            NormalTransform = XForm.Inverse().Transpose();
            return true;
        }


        public readonly bool EpsilonEqual(ref readonly Matrix4d m2, double epsilon)
        {
            return Row0.EpsilonEqual(in m2.Row0, epsilon) &&
                Row1.EpsilonEqual(in m2.Row1, epsilon) &&
                Row2.EpsilonEqual(in m2.Row2, epsilon) &&
                Row3.EpsilonEqual(in m2.Row3, epsilon);
        }



        public static Matrix4d AxisAngleDeg(Vector3d axis, double angleDeg)
        {
            Matrix3d rot = Matrix3d.AxisAngleD(axis, angleDeg);
            return new Matrix4d(ref rot);
        }
        public static Matrix4d Translation(Vector3d Translation)
        {
            return new Matrix4d(Vector4d.UnitX, Vector4d.UnitY, Vector4d.UnitZ,
                new Vector4d(Translation.x, Translation.y, Translation.z, 1.0), false);
        }
        public static Matrix4d Scale(Vector3d Scale)
        {
            return new Matrix4d(Scale.x, Scale.y, Scale.z, 1.0);
        }
        public static Matrix4d Affine(ref readonly Matrix3d AffineTransform)
        {
            return new Matrix4d(in AffineTransform);
        }

        public override string ToString() {
            return string.Format("[{0}] [{1}] [{2}] [{3}]", Row0, Row1, Row2, Row3);
        }
        public string ToString(string fmt) {
            return string.Format("[{0}] [{1}] [{2}] [{3}]", Row0.ToString(fmt), Row1.ToString(fmt), Row2.ToString(fmt), Row3.ToString(fmt));
        }
    }
}
