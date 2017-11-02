using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // ported from WildMagic5 MinCircle2
    //
    // Compute the minimum area circle containing the input set of points.  The
    // algorithm randomly permutes the input points so that the construction
    // occurs in 'expected' O(N) time.
    public class ContMinCircle2
    {
        double mEpsilon;
        Func<int, int[], Support, Circle2d>[] mUpdate = new Func<int, int[], Support, Circle2d>[4];

        Vector2d[] Points;

        public Circle2d Minimal;

        public ContMinCircle2(Vector2d[] pointsIn, double epsilon = 1e-05) {
            mEpsilon = epsilon;

            mUpdate[0] = null;
            mUpdate[1] = UpdateSupport1;
            mUpdate[2] = UpdateSupport2;
            mUpdate[3] = UpdateSupport3;

            Support support = new Support();
            double distDiff = 0;

            Points = pointsIn;
            int numPoints = pointsIn.Length;

            int[] permutation = null;
            Random r = new Random();

            if (numPoints >= 1) {
                // Create identity permutation (0,1,..,numPoints-1).
                permutation = new int[numPoints];
                for (int i = 0; i < numPoints; ++i) {
                    permutation[i] = i;
                }

                // Generate random permutation.
                for (int i = numPoints - 1; i > 0; --i) {
                    int j = r.Next() % (i + 1);
                    if (j != i) {
                        int save = permutation[i];
                        permutation[i] = permutation[j];
                        permutation[j] = save;
                    }
                }

                Minimal = ExactCircle1(ref Points[permutation[0]]);
                support.Quantity = 1;
                support.Index[0] = 0;

                // The previous version of the processing loop is
                //  i = 1;
                //  while (i < numPoints)
                //  {
                //      if (!support.Contains(i, permutation, mEpsilon))
                //      {
                //          if (!Contains(*permutation[i], minimal, distDiff))
                //          {
                //              UpdateFunction update = mUpdate[support.Quantity];
                //              Circle2d circle = (this->*update)(i, permutation,
                //                  support);
                //              if (circle.Radius > minimal.Radius)
                //              {
                //                  minimal = circle;
                //                  i = 0;
                //                  continue;
                //              }
                //          }
                //      }
                //      ++i;
                //  }
                // This loop restarts from the beginning of the point list each time
                // the circle needs updating.  Linus Källberg (Computer Science at
                // Mälardalen University in Sweden) discovered that performance is
                // better when the remaining points in the array are processed before
                // restarting.  The points processed before the point that caused the
                // update are likely to be enclosed by the new circle (or near the
                // circle boundary) because they were enclosed by the previous circle.
                // The chances are better that points after the current one will cause
                // growth of the bounding circle.
                for (int i = 1 % numPoints, n = 0; i != n; i = (i + 1) % numPoints) {
                    if (!support.Contains(i, Points, permutation, mEpsilon)) {
                        if (!Contains(ref Points[permutation[i]], ref Minimal, ref distDiff)) {
                            var updateF = mUpdate[support.Quantity];
                            Circle2d circle = updateF(i, permutation, support);
                            if (circle.Radius > Minimal.Radius) {
                                Minimal = circle;
                                n = i;
                            }
                        }
                    }
                }


            } else {
                throw new Exception("ContMinCircle2: Input must contain points\n");
            }

            Minimal.Radius = Math.Sqrt(Minimal.Radius);
        }


        bool Contains(ref Vector2d point, ref Circle2d circle, ref double distDiff)
        {
            Vector2d diff = point - circle.Center;
            double test = diff.LengthSquared;

            // NOTE:  In this algorithm, Circle2 is storing the *squared radius*,
            // so the next line of code is not in error.
            distDiff = test - circle.Radius;

            return distDiff <= 0;
        }


        Circle2d ExactCircle1(ref Vector2d P) {
            return new Circle2d(P, 0);
        }


        Circle2d ExactCircle2(ref Vector2d P0, ref Vector2d P1) {
            return new Circle2d(
                0.5 * (P0 + P1), 0.25 * P1.DistanceSquared(P0));
        }


        Circle2d ExactCircle3(ref Vector2d P0, ref Vector2d P1, ref Vector2d P2)
        {
            Vector2d E10 = P1 - P0;
            Vector2d E20 = P2 - P0;

            double[,] A = new double[2,2] {
                { E10.x, E10.y },
                { E20.x, E20.y } };

            Vector2d B = new Vector2d(
                ((double)0.5) * E10.LengthSquared,
                ((double)0.5) * E20.LengthSquared);

            double det = A[0,0] * A[1,1] - A[0,1] * A[1,0];

            if (Math.Abs(det) > mEpsilon) {
                double invDet = ((double)1) / det;
                Vector2d Q;
                Q.x = (A[1,1] * B[0] - A[0,1] * B[1]) * invDet;
                Q.y = (A[0,0] * B[1] - A[1,0] * B[0]) * invDet;

                return new Circle2d(P0 + Q, Q.LengthSquared);
            } else {
                return new Circle2d(Vector2d.Zero, double.MaxValue);
            }
        }


        Circle2d UpdateSupport1(int i, int[] permutation, Support support)
        {
            Vector2d P0 = Points[permutation[support.Index[0]]];
            Vector2d P1 = Points[permutation[i]];

            Circle2d minimal = ExactCircle2(ref P0, ref P1);
            support.Quantity = 2;
            support.Index[1] = i;

            return minimal;
        }


        Circle2d UpdateSupport2(int i, int[] permutation, Support support)
{
            Vector2d[] point = new Vector2d[2] {
                Points[permutation[support.Index[0]]],  // P0
                Points[permutation[support.Index[1]]]   // P1
            };
            Vector2d P2 = Points[permutation[i]];

            // Permutations of type 2, used for calling ExactCircle2(..).
            int numType2 = 2;
            int[,] type2 = new int[2,2] {
                {0, /*2*/ 1},
                {1, /*2*/ 0}
            };

            // Permutations of type 3, used for calling ExactCircle3(..).
            int numType3 = 1;  // {0, 1, 2}

            Circle2d[] circle = new Circle2d[numType2 + numType3];
            int indexCircle = 0;
            double minRSqr = double.MaxValue;
            int indexMinRSqr = -1;
            double distDiff = 0, minDistDiff = double.MaxValue;
            int indexMinDistDiff = -1;

            // Permutations of type 2.
            int j;
            for (j = 0; j<numType2; ++j, ++indexCircle)
            {
                circle[indexCircle] = ExactCircle2(ref point[type2[j,0]], ref P2);
                if (circle[indexCircle].Radius<minRSqr)
                {
                    if (Contains(ref point[type2[j,1]], ref circle[indexCircle], ref distDiff)) {
                        minRSqr = circle[indexCircle].Radius;
                        indexMinRSqr = indexCircle;
                    }
                    else if (distDiff < minDistDiff)
                    {
                        minDistDiff = distDiff;
                        indexMinDistDiff = indexCircle;
                    }
                }
            }

            // Permutations of type 3.
            circle[indexCircle] = ExactCircle3(ref point[0], ref point[1], ref P2);
            if (circle[indexCircle].Radius < minRSqr)
            {
                minRSqr = circle[indexCircle].Radius;
                indexMinRSqr = indexCircle;
            }

            // Theoreticaly, indexMinRSqr >= 0, but floating-point round-off errors
            // can lead to indexMinRSqr == -1.  When this happens, the minimal sphere
            // is chosen to be the one that has the minimum absolute errors between
            // the sphere and points (barely) outside the sphere.
            if (indexMinRSqr == -1)
            {
                indexMinRSqr = indexMinDistDiff;
            }

            Circle2d minimal = circle[indexMinRSqr];
            switch (indexMinRSqr)
            {
            case 0:
                support.Index[1] = i;
                break;
            case 1:
                support.Index[0] = i;
                break;
            case 2:
                support.Quantity = 3;
                support.Index[2] = i;
                break;
            }

            return minimal;
        }


        Circle2d UpdateSupport3(int i, int[] permutation, Support support)
        {
            Vector2d[] point = new Vector2d[3]
            {
                Points[permutation[support.Index[0]]],  // P0
                Points[permutation[support.Index[1]]],  // P1
                Points[permutation[support.Index[2]]]   // P2
            };
            Vector2d P3 = Points[permutation[i]];

            // Permutations of type 2, used for calling ExactCircle2(..).
            int numType2 = 3;
            int[,] type2 = new int[3,3] {
                { 0, /*3*/ 1, 2},
                { 1, /*3*/ 0, 2},
                { 2, /*3*/ 0, 1}
            };

            // Permutations of type 2, used for calling ExactCircle3(..).
            int numType3 = 3;
            int[,] type3 = new int[3, 3] {
                {0, 1, /*3*/ 2},
                {0, 2, /*3*/ 1},
                {1, 2, /*3*/ 0}
            };

            Circle2d[] circle = new Circle2d[numType2 + numType3];
            int indexCircle = 0;
            double minRSqr = double.MaxValue;
            int indexMinRSqr = -1;
            double distDiff = 0, minDistDiff = double.MaxValue;
            int indexMinDistDiff = -1;

            // Permutations of type 2.
            int j;
            for (j = 0; j<numType2; ++j, ++indexCircle)
            {
                circle[indexCircle] = ExactCircle2(ref point[type2[j, 0]], ref P3);
                if (circle[indexCircle].Radius<minRSqr)
                {
                    if (Contains(ref point[type2[j,1]], ref circle[indexCircle], ref distDiff)
                         &&  Contains(ref point[type2[j, 2]], ref circle[indexCircle], ref distDiff))
                    {
                        minRSqr = circle[indexCircle].Radius;
                        indexMinRSqr = indexCircle;
                    }
                    else if (distDiff < minDistDiff)
                    {
                        minDistDiff = distDiff;
                        indexMinDistDiff = indexCircle;
                    }
                }
            }

            // Permutations of type 3.
            for (j = 0; j<numType3; ++j, ++indexCircle)
            {
                circle[indexCircle] = ExactCircle3(ref point[type3[j, 0]], ref point[type3[j, 1]], ref P3);
                if (circle[indexCircle].Radius < minRSqr)
                {
                    if (Contains(ref point[type3[j, 2]], ref circle[indexCircle], ref distDiff))
                    {
                        minRSqr = circle[indexCircle].Radius;
                        indexMinRSqr = indexCircle;
                    }
                    else if (distDiff < minDistDiff)
                    {
                        minDistDiff = distDiff;
                        indexMinDistDiff = indexCircle;
                    }
                }
            }

            // Theoreticaly, indexMinRSqr >= 0, but floating-point round-off errors
            // can lead to indexMinRSqr == -1.  When this happens, the minimal circle
            // is chosen to be the one that has the minimum absolute errors between
            // the circle and points (barely) outside the circle.
            if (indexMinRSqr == -1)
            {
                indexMinRSqr = indexMinDistDiff;
            }

            Circle2d minimal = circle[indexMinRSqr];
            switch (indexMinRSqr)
            {
            case 0:
                support.Quantity = 2;
                support.Index[1] = i;
                break;
            case 1:
                support.Quantity = 2;
                support.Index[0] = i;
                break;
            case 2:
                support.Quantity = 2;
                support.Index[0] = support.Index[2];
                support.Index[1] = i;
                break;
            case 3:
                support.Index[2] = i;
                break;
            case 4:
                support.Index[1] = i;
                break;
            case 5:
                support.Index[0] = i;
                break;
            }

            return minimal;
        }



        // Indices of points that support current minimum area circle.
        protected class Support
        {
            public bool Contains(int index, Vector2d[] Points, int[] permutation, double epsilon)
            {
                for (int i = 0; i < Quantity; ++i) {
                    Vector2d diff = Points[permutation[index]] - Points[permutation[Index[i]]];
                    if (diff.LengthSquared < epsilon) {
                        return true;
                    }
                }
                return false;
            }

            public int Quantity;
            public Index3i Index;
        };

    }
}
