using g3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace geometry3Sharp.Tests
{
    [TestClass]
    public class Polygon2d_ContainsPoint
    {

        private readonly Polygon2d PolyBase;

        // Constructor
        public Polygon2d_ContainsPoint()
        {
            /* Sets up a polygon with the following shape:

               3 ------------ 2
               |              |
               |              |
               |              |
               |              |
               |              |
               0 ------------ 1

            */

            PolyBase = new Polygon2d( new double[8] {
                0, 0,
                6, 0,
                6, 8,
                0, 8,
            });
        }

        [TestMethod]
        public void Polygon2d_ContainsPoint_Inside()
        {
            // Act
            bool actual = PolyBase.Contains(new Vector2d(3, 4));

            // Assert
            Assert.IsTrue(actual);
        }

        [TestMethod]
        public void Polygon2d_ContainsPoint_Outside()
        {
            // Act
            bool result = PolyBase.Contains(new Vector2d(3, 9));

            // Assert
            Assert.IsFalse(result); 
        }

        [TestMethod]
        public void Polygon2d_ContainsPoint_OnEdge()
        {
            // Act
            bool result = PolyBase.Contains(new Vector2d(3, 0));

            // Assert
            Assert.IsTrue(result);
        }
    }

    [TestClass]
    public class Polygon2d_ContainsPolgon
    {

        private readonly Polygon2d PolyBase;

        // Constructor
        public Polygon2d_ContainsPolgon()
        {
            /* Sets up a polygon with the following shape:

               3 ------------ 2
                \             |
                 \            |
                  4           |
                 /            |
                /             |
               0 ------------ 1

            */

            PolyBase = new Polygon2d(new double[10] {
                0, 0,
                6, 0,
                6, 8,
                0, 8,
                2, 4,
            });
        }

        [TestMethod]
        public void Polygon2d_ContainsPolyon_Inside()
        {
            // Arrange
            Polygon2d PolyOther = new Polygon2d(new double[8] {
                3, 3,
                5, 3,
                5, 7,
                3, 7,
            });

            // Act
            bool result = PolyBase.Contains(PolyOther);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Polygon2d_ContainsPolyon_Intersecting()
        {
            // Arrange
            Polygon2d PolyOther = new Polygon2d(new double[8] {
                3, 3,
                9, 3,
                9, 7,
                3, 7,
            });

            // Act
            bool result = PolyBase.Contains(PolyOther);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Polygon2d_ContainsPolyon_Outside()
        {
            // Arrange
            Polygon2d PolyOther = new Polygon2d(new double[8] {
                13, 3,
                15, 3,
                15, 7,
                13, 7,
            });

            // Act
            bool result = PolyBase.Contains(PolyOther);

            // Assert
            Assert.IsFalse(result);
        }
    }

    [TestClass]
    public class Polygon2d_ContainsSegment
    {

        private readonly Polygon2d PolyBase;

        // Constructor
        public Polygon2d_ContainsSegment()
        {
            /* Sets up a polygon with the following shape:

               3 ------------ 2
                \             |
                 \            |
                  4           |
                 /            |
                /             |
               0 ------------ 1

            */

            PolyBase = new Polygon2d(new double[10] {
                0, 0,
                6, 0,
                6, 8,
                0, 8,
                2, 4,
            });
        }

        [TestMethod]
        public void Polygon2d_ContainsSegment_Inside()
        {
            // Arrange
            Segment2d Seg = new Segment2d(
                new Vector2d(3, 3),
                new Vector2d(5, 3));

            // Act
            bool result = PolyBase.Contains(Seg);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Polygon2d_ContainsSegment_Intersecting()
        {
            // Arrange
            Segment2d Seg = new Segment2d(
                new Vector2d(3, 3),
                new Vector2d(9, 3));

            // Act
            bool result = PolyBase.Contains(Seg);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Polygon2d_ContainsSegment_Outside()
        {
            // Arrange
            Segment2d Seg = new Segment2d(
                new Vector2d(13, 3),
                new Vector2d(15, 3));

            // Act
            bool result = PolyBase.Contains(Seg);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Polygon2d_ContainsSegment_Crossing()
        {
            // Arrange
            Segment2d Seg = new Segment2d(
                new Vector2d(1, 1),
                new Vector2d(1, 7));

            // Act
            bool result = PolyBase.Contains(Seg);

            // Assert
            Assert.IsFalse(result);
        }
    }
}
