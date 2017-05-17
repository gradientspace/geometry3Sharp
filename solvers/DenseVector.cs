using System;

namespace g3
{
    public class DenseVector
    {
        double[] d;
        int N;


        public DenseVector(int N)
        {
            d = new double[N];
            Array.Clear(d, 0, d.Length);
            this.N = N;
        }


        public void Set(int i, double value)
        {
            d[i] = value;
        }


        public int Size { get { return N; } }
        public int Length { get { return N; } }

        public double this[int i]
        {
            get { return d[i]; }
            set { d[i] = value; }
        }

        public double[] Buffer
        {
            get { return d; }
        }


        public double Dot(DenseVector v2) {
            return Dot(v2.d);
        }
        public double Dot(double[] v2)
        {
            if (v2.Length != N)
                throw new Exception("DenseVector.Dot: incompatible lengths");
            double sum = 0;
            for (int k = 0; k < v2.Length; ++k)
                sum += d[k] * v2[k];
            return sum;
        }

    }
}