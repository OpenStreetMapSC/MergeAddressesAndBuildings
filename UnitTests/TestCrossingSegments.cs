using Microsoft.VisualStudio.TestTools.UnitTesting;
using MergeAddressesAndBuildings;

namespace TestCrossingSegments
{
    [TestClass]
    public class TestCrossingSegments
    {
        [TestMethod]
        public void TestMethod1()
        {
            /// X = Lon, Y = Lat

            var x11 = 1.0; var y11 = 1.0; var x21 = 5.0; var y21 = 1.0;
            var x12 = 2.0; var y12 = 3.0; var x22 = 6.0; var y22 = 3.0;
            var parallelHorizontalSegments = new CrossingSegments(
                new Coordinate(y11, x11), new Coordinate(y21, x21),
                new Coordinate(y12, x12), new Coordinate(y22, x22));
            Assert.IsNull(parallelHorizontalSegments.IntersectionPoint);

            x11 = 1.0; y11 = 1.0; x21 = 1.0; y21 = 5.0;
            x12 = 3.0; y12 = 2.0; x22 = 3.0; y22 = 7.0;
            var parallelVerticalSegments = new CrossingSegments(
                new Coordinate(y11, x11), new Coordinate(y21, x21),
                new Coordinate(y12, x12), new Coordinate(y22, x22));
            Assert.IsNull(parallelVerticalSegments.IntersectionPoint);

            x11 = 1.0; y11 = 1.0; x21 = 1.0; y21 = 5.0;
            x12 = 1.0; y12 = 6.0; x22 = 1.0; y22 = 8.0;
            var verticalNontouchSegments = new CrossingSegments(
                new Coordinate(y11, x11), new Coordinate(y21, x21),
                new Coordinate(y12, x12), new Coordinate(y22, x22));
            Assert.IsNull(verticalNontouchSegments.IntersectionPoint);

            x11 = 1.0; y11 = 1.0; x21 = 1.0; y21 = 5.0;
            x12 = 1.0; y12 = 3.0; x22 = 1.0; y22 = 8.0;
            var verticalTouchSegments = new CrossingSegments(
                new Coordinate(y11, x11), new Coordinate(y21, x21),
                new Coordinate(y12, x12), new Coordinate(y22, x22));
            Assert.IsNotNull(verticalTouchSegments.IntersectionPoint);

            x11 = 1.0; y11 = 3.0; x21 = 3.0; y21 = 5.0;
            x12 = 3.0; y12 = 2.0; x22 = 5.0; y22 = 4.0;
            var parallelSegments = new CrossingSegments(
                new Coordinate(y11, x11), new Coordinate(y21, x21),
                new Coordinate(y12, x12), new Coordinate(y22, x22));
            Assert.IsNull(parallelSegments.IntersectionPoint);

            x11 = 1.0; y11 = 1.0; x21 = 5.0; y21 = 1.0;
            x12 = 4.0; y12 = 2.0; x22 = 2.0; y22 = 0.0;
            var crossingHorizontal = new CrossingSegments(
                new Coordinate(y11, x11), new Coordinate(y21, x21),
                new Coordinate(y12, x12), new Coordinate(y22, x22));
            Assert.AreEqual(crossingHorizontal.IntersectionPoint.Lon, 3.0, 1.0e-9);
            Assert.AreEqual(crossingHorizontal.IntersectionPoint.Lat, 1.0, 1.0e-9);

            x11 = 1.0; y11 = 1.0; x21 = 5.0; y21 = 1.0;
            x12 = 14.0; y12 = 12.0; x22 = 12.0; y22 = 10.0;
            var notCrossingHorizontal = new CrossingSegments(
                new Coordinate(y11, x11), new Coordinate(y21, x21),
                new Coordinate(y12, x12), new Coordinate(y22, x22));
            Assert.IsNull(notCrossingHorizontal.IntersectionPoint);

            x11 = 3.0; y11 = 0.0; x21 = 3.0; y21 = 5.0;
            x12 = 4.0; y12 = 2.0; x22 = 2.0; y22 = 0.0;
            var crossingVertical = new CrossingSegments(
                new Coordinate(y11, x11), new Coordinate(y21, x21),
                new Coordinate(y12, x12), new Coordinate(y22, x22));
            Assert.AreEqual(crossingVertical.IntersectionPoint.Lon, 3.0, 1.0e-9);
            Assert.AreEqual(crossingVertical.IntersectionPoint.Lat, 1.0, 1.0e-9);

            x11 = 11.0; y11 = 11.0; x21 = 11.0; y21 = 15.0;
            x12 = 4.0; y12 = 2.0; x22 = 2.0; y22 = 0.0;
            var notCrossingVertical = new CrossingSegments(
                new Coordinate(y11, x11), new Coordinate(y21, x21),
                new Coordinate(y12, x12), new Coordinate(y22, x22));
            Assert.IsNull(notCrossingVertical.IntersectionPoint);

            x11 = 3.0; y11 = 1.0; x21 = 5.0; y21 = 3.0;
            x12 = 3.0; y12 = 3.0; x22 = 5.0; y22 = 1.0;
            var crossing = new CrossingSegments(
                new Coordinate(y11, x11), new Coordinate(y21, x21),
                new Coordinate(y12, x12), new Coordinate(y22, x22));
            Assert.AreEqual(crossing.IntersectionPoint.Lon, 4.0, 1.0e-9);
            Assert.AreEqual(crossing.IntersectionPoint.Lat, 2.0, 1.0e-9);

            x11 = 3.0; y11 = 1.0; x21 = 5.0; y21 = 3.0;
            x12 = 0.0; y12 = 7.0; x22 = 2.0; y22 = 5.0;
            var notCrossing = new CrossingSegments(
                new Coordinate(y11, x11), new Coordinate(y21, x21),
                new Coordinate(y12, x12), new Coordinate(y22, x22));
            Assert.IsNull(notCrossing.IntersectionPoint);
        }
    }
}
