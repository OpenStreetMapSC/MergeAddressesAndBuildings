using System;
using System.Collections.Generic;
using System.Text;

namespace MergeAddressesAndBuildings
{
    public class SpatialUtilities
    {


        /// <summary>
        /// Compute distance between this coordinate and another point on the earth when
        /// the two points are close to each other (within a Km) making the error
        /// from omitting the earth radius normally less than any land slope.
        /// </summary>
        /// <param name="other">The other point</param>
        /// <returns>Distance in meters</returns>
        public static double Distance(double lat1, double lat2, double lon1, double lon2)
        {
            var lengthOfDegree = 110250.0;  // Average, in Meters
            var x = lat1 - lat2;
            var y = (lon1 - lon2) * Math.Cos(lat2);
            return lengthOfDegree * Math.Sqrt(x * x + y * y);
        }


        public static double DegreeToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }


        public static double RadianToDegree(double angle)
        {
            return angle * (180.0 / Math.PI);
        }


        /// <summary>
        /// Move toward desired angle
        /// </summary>
        /// <param name="currentPosition">Starting position</param>
        /// <param name="distance"></param>
        /// <param name="bearing">degrees clockwise from true north</param>
        /// <returns></returns>
        public static Coordinate MoveByToward(Coordinate currentPosition, double distance, double bearing)
        {
            double R = 6378.1; // Radius of the Earth

            double bearingRadians = bearing * Math.PI / 180.0;

            double distanceKm = distance / 1000.0;

            var latRadians = currentPosition.Lat * Math.PI / 180.0;
            var lonRadians = currentPosition.Lon * Math.PI / 180.0;

            var lat2 = Math.Asin(Math.Sin(latRadians) * Math.Cos(distanceKm / R) +
                Math.Cos(latRadians) * Math.Sin(distanceKm / R) * Math.Cos(bearingRadians));

            var lon2 = lonRadians + Math.Atan2(Math.Sin(bearingRadians) * Math.Sin(distanceKm / R) * Math.Cos(latRadians),
             Math.Cos(distanceKm / R) - Math.Sin(latRadians) * Math.Sin(lat2));

            var latDegrees = lat2 * 180.0 / Math.PI;
            var lonDegrees = lon2 * 180.0 / Math.PI;

            return new Coordinate(latDegrees, lonDegrees);
        }

        /// <summary>
        /// Computes the distance between this coordinate and another point on the earth.
        /// Uses spherical law of cosines formula, not Haversine.
        /// </summary>
        /// <param name="other">The other point</param>
        /// <returns>Distance in meters</returns>
        public static double GreatCircleDistance(double lat1, double lat2, double lon1, double lon2)
        {
            var epsilon = Math.Abs(lon1-lon2) + Math.Abs(lat1 - lat2);
            if (epsilon < 1.0e-6) return 0.0;

            double meters = (Math.Acos(
                    Math.Sin(DegreeToRadians(lat1)) * Math.Sin(DegreeToRadians(lat2)) +
                    Math.Cos(DegreeToRadians(lat1)) * Math.Cos(DegreeToRadians(lat2)) *
                    Math.Cos(DegreeToRadians(lon2 - lon1))) * 6378135);

            return (meters);
        }

        public static void SetBboxFor(OSMWay osmWay)
        {
            foreach (var osmNode in osmWay.NodeList)
            {
                if (osmNode.Lat < osmWay.Bbox.MinLat) osmWay.Bbox.MinLat = osmNode.Lat;
                if (osmNode.Lat > osmWay.Bbox.MaxLat) osmWay.Bbox.MaxLat = osmNode.Lat;
                if (osmNode.Lon < osmWay.Bbox.MinLon) osmWay.Bbox.MinLon = osmNode.Lon;
                if (osmNode.Lon > osmWay.Bbox.MaxLon) osmWay.Bbox.MaxLon = osmNode.Lon;
            }
        }

        public static bool BBoxIntersects(BBox bbox1, BBox bbox2)
        {
            if ((bbox1.MaxLat < bbox2.MinLat) ||
               (bbox1.MaxLon < bbox2.MinLon) ||
               (bbox1.MinLon > bbox2.MaxLon) ||
               (bbox1.MinLat > bbox2.MaxLat))
            {
                return false;
            }
            return true;
        }


        /// <summary>
        /// Return largest percentage of any bbox overlap for 
        /// previously known intersecting bboxes.   
        /// If one box contains the other, overlap is 100%
        /// </summary>
        /// <param name="bbox1"></param>
        /// <param name="bbox2"></param>
        /// <returns></returns>
        public static double BBoxOverlapPercent(BBox bbox1, BBox bbox2)
        {
            if (BboxContains(bbox1, bbox2) ||
                BboxContains(bbox2, bbox1))
            {
                // One complete box in common.
                return 100.0;
            }

            var bboxOverlap = BboxOverlap(bbox1, bbox2);
            var overlapArea = BboxArea(bboxOverlap);

            var smallestArea = Math.Min(BboxArea(bbox1), BboxArea(bbox2));

            var percentage = (overlapArea / smallestArea) * 100.0;
            return percentage;
        }


        /// <summary>
        /// Calculate overlapping bbox area covered by both boxes
        /// </summary>
        /// <param name="bbox1"></param>
        /// <param name="bbox2"></param>
        /// <returns></returns>
        public static BBox BboxOverlap(BBox bbox1, BBox bbox2)
        {
            var bbox = new BBox();

            bbox.MinLat = Math.Max(bbox1.MinLat, bbox2.MinLat);
            bbox.MinLon = Math.Max(bbox1.MinLon, bbox2.MinLon);

            bbox.MaxLat = Math.Min(bbox1.MaxLat, bbox2.MaxLat);
            bbox.MaxLon = Math.Min(bbox1.MaxLon, bbox2.MaxLon);

            return bbox;
        }

        /// <summary>
        /// Calculate total bbox area covered by both boxes
        /// </summary>
        /// <param name="bbox1"></param>
        /// <param name="bbox2"></param>
        /// <returns></returns>
        public static BBox BboxIntersection(BBox bbox1, BBox bbox2)
        {
            var bbox = new BBox();

            bbox.MinLat = bbox1.MinLat < bbox2.MinLat ? bbox1.MinLat : bbox2.MinLat;
            bbox.MinLon = bbox1.MinLon < bbox2.MinLon ? bbox1.MinLon : bbox2.MinLon;

            bbox.MaxLat = bbox1.MaxLat > bbox2.MaxLat ? bbox1.MaxLat : bbox2.MaxLat;
            bbox.MaxLon = bbox1.MaxLon > bbox2.MaxLon ? bbox1.MaxLon : bbox2.MaxLon;

            return bbox;
        }

        /// <summary>
        /// See if one bbox lies entirely within other
        /// </summary>
        /// <param name="bbox1">Possibly containing box</param>
        /// <param name="bbox2">Possibly contained box</param>
        /// <returns></returns>
        public static bool BboxContains(BBox bbox1, BBox bbox2)
        {
            return (bbox1.MaxLon < bbox2.MaxLon &&
                bbox1.MinLon > bbox2.MinLon &&
                bbox1.MaxLat < bbox2.MaxLat &&
                bbox1.MinLat > bbox2.MinLat) ;
        }


        public static double BboxArea(BBox bbox)
        {
            var width = new Coordinate(bbox.MinLat, bbox.MinLon).GreatCircleDistance(new Coordinate(bbox.MinLat, bbox.MaxLon));
            var height = new Coordinate(bbox.MinLat, bbox.MinLon).GreatCircleDistance(new Coordinate(bbox.MaxLat, bbox.MinLon));
            return width * height;
        }

        /// <summary>
        /// Calculate union of area covered by both boxes, 
        /// including any area between boxes
        /// </summary>
        /// <param name="bbox1"></param>
        /// <param name="bbox2"></param>
        /// <returns></returns>
        public static BBox BboxUnion(BBox bbox1, BBox bbox2)
        {
            var bbox = new BBox();

            bbox.MinLat = bbox1.MinLat < bbox2.MinLat ? bbox1.MinLat : bbox2.MinLat;
            bbox.MinLon = bbox1.MinLon < bbox2.MinLon ? bbox1.MinLon : bbox2.MinLon;

            bbox.MaxLat = bbox1.MaxLat > bbox2.MaxLat ? bbox1.MaxLat : bbox2.MaxLat;
            bbox.MaxLon = bbox1.MaxLon > bbox2.MaxLon ? bbox1.MaxLon : bbox2.MaxLon;

            return bbox;
        }

        /// <summary>
        /// set or expand box if node not already contained
        /// </summary>
        /// <param name="bbox1"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        public static BBox BboxUnion(BBox bbox1, OSMNode node)
        {
            var bbox = new BBox();
            bbox.MinLat = bbox1.MinLat < node.Lat ? bbox1.MinLat : node.Lat;
            bbox.MinLon = bbox1.MinLon < node.Lon ? bbox1.MinLon : node.Lon;

            bbox.MaxLat = bbox1.MaxLat > node.Lat ? bbox1.MaxLat : node.Lat;
            bbox.MaxLon = bbox1.MaxLon > node.Lon ? bbox1.MaxLon : node.Lon;

            return bbox;
        }



        public static bool BBoxContains(BBox bbox, OSMNode osmNode)
        {
            if ((osmNode.Lat >= bbox.MinLat) &&
                 (osmNode.Lon >= bbox.MinLon) &&
                 (osmNode.Lat < bbox.MaxLat) &&
                 (osmNode.Lon < bbox.MaxLon))
            {
                return true;
            }
            return false;
        }


        /// <summary>
        /// Might be buggy (not used)
        /// </summary>
        /// <param name="testPoint"></param>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public static bool IsInPolygon(Coordinate testPoint, IList<Coordinate> vertices)
        {
            if (vertices.Count < 3) return false;
            bool isInPolygon = false;
            var lastVertex = vertices[vertices.Count - 1];
            foreach (var vertex in vertices)
            {
                if (IsBetween(testPoint.Lon, lastVertex.Lon, vertex.Lon))
                {
                    double t = (testPoint.Lon - lastVertex.Lon) / (vertex.Lon - lastVertex.Lon);
                    double x = t * (vertex.Lat - lastVertex.Lat) + lastVertex.Lat;
                    if (x >= testPoint.Lat) isInPolygon = !isInPolygon;
                }
                else
                {
                    if (testPoint.Lon == lastVertex.Lon && testPoint.Lat < lastVertex.Lat && vertex.Lon > testPoint.Lon) isInPolygon = !isInPolygon;
                    if (testPoint.Lon == vertex.Lon && testPoint.Lat < vertex.Lat && lastVertex.Lon > testPoint.Lon) isInPolygon = !isInPolygon;
                }

                lastVertex = vertex;
            }

            return isInPolygon;
        }

        public static bool IsBetween(double x, double a, double b)
        {
            return (x - a) * (x - b) < 0;
        }


        /// <summary>
        ///
        /// </summary>
        /// <param name="way1"></param>
        /// <param name="way2"></param>
        /// <returns></returns>
        public static bool PolygonsTouchOrIntersect(OSMWay way1, OSMWay way2)
        {

            // Convert to coordinate lists
            var coords1 = new List<Coordinate>();
            foreach (var node1 in way1.NodeList)
            {
                coords1.Add(new Coordinate(node1.Lat, node1.Lon));
            }
            var coords2 = new List<Coordinate>();
            foreach (var node2 in way2.NodeList)
            {
                coords2.Add(new Coordinate(node2.Lat, node2.Lon));
            }


            // Check for shared coordinates
            foreach (var node1 in way1.NodeList)
            {
                foreach (var node2 in way2.NodeList)
                {
                    if (node1.LocatedAt(node2)) return true;  // Touching
                }
            }



            // Check for polygon contained entirely within other
            var clipBoundaryTest = new ClipBoundary(way1);
            if (clipBoundaryTest.IsInBoundary(way2.NodeList[0])) return true;
            clipBoundaryTest = new ClipBoundary(way2);
            if (clipBoundaryTest.IsInBoundary(way1.NodeList[0])) return true;


            // Check for any intersection with any other segment
            Coordinate lastPoint1 = null;
            foreach (var point1 in coords1)
            {
                if (lastPoint1 != null)
                {
                    // Make segment with previous point

                    Coordinate lastPoint2 = null;
                    foreach (var point2 in coords2)
                    {
                        if (lastPoint2 != null)
                        {

                            var testCrossingSegment = new CrossingSegments(point1, lastPoint1, point2, lastPoint2);
                            if (testCrossingSegment.IntersectionPoint != null)
                            {
                                return true;  // Have intersection
                            }

                        }
                        lastPoint2 = point2;
                    }
                }
                lastPoint1 = point1;
            }

            return false;
        }

    }
}
