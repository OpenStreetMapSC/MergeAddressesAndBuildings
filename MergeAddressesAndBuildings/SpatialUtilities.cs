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


    }
}
