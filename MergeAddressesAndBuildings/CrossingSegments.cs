using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MergeAddressesAndBuildings
{
    public class CrossingSegments
    {
        public CrossingSegments(Coordinate seg1_a, Coordinate seg1_b,
                                Coordinate seg2_a, Coordinate seg2_b)
        {

            var segment1 = new Segment(seg1_a, seg1_b);
            var segment2 = new Segment(seg2_a, seg2_b);

            // Calculate intersection
            if ((segment1.Slope == segment2.Slope) ||
                (double.IsNaN(segment1.Slope) && double.IsNaN(segment2.Slope)))
            {
                // Check for vertical overlap
                if (seg1_a.Lon == seg2_a.Lon)
                {
                    if ((seg1_a.Lat >= seg2_a.Lat) && (seg1_a.Lat <= seg2_b.Lat) ||
                            (seg2_a.Lat >= seg1_a.Lat) && (seg2_a.Lat <= seg1_b.Lat))
                    {
                        IntersectionPoint = seg1_a;  // Pick random point on vertical overlap area
                    }
                }
                return;
            }

            var lon = double.NaN;
            var lat = double.NaN;

            if (double.IsNaN(segment1.Slope))
            {
                lat = segment2.SolveForY(seg1_a.Lon);
                lon = seg1_a.Lon;
            }
            else if (double.IsNaN(segment2.Slope))
            {
                lat = segment1.SolveForY(seg2_a.Lon);
                lon = seg2_a.Lon;
            }
            else if (segment1.Slope == 0.0)
            {
                lat = seg1_a.Lat;
                lon = segment2.SolveForX(seg1_a.Lat);
            }
            else if (segment2.Slope == 0.0)
            {
                lat = seg2_a.Lat;
                lon = segment1.SolveForX(seg2_a.Lat);
            }
            else
            {
                lon = (segment1.YIntercept - segment2.YIntercept) / (segment2.Slope - segment1.Slope);
                lat = segment1.SolveForY(lon);
            }

            if (lon >= Math.Min(seg1_a.Lon, seg1_b.Lon) && lon <= Math.Max(seg1_a.Lon, seg1_b.Lon) &&
                lon >= Math.Min(seg2_a.Lon, seg2_b.Lon) && lon <= Math.Max(seg2_a.Lon, seg2_b.Lon))
            {
                // Intersection lies on both lines
                IntersectionPoint = new Coordinate(lat, lon);
            }

        }


        /// <summary>
        /// Intersection point of 2 segments.  null if no intersection
        /// Assumes small segment lengths instead of great circle distance
        /// </summary>
        public Coordinate IntersectionPoint { get; private set; }



        private class Segment
        {

            public double Slope { get; set; }
            public double YIntercept { get; set; }


            Coordinate point1 { get; set; }
            Coordinate point2 { get; set; }


            public Segment(Coordinate p1, Coordinate p2)
            {
                point1 = p1;
                point2 = p2;
                CalculateSlopeIntercept();

            }


            public double SolveForX(double y)
            {
                var x = double.NaN;

                if (Slope == 0.0)
                {
                    // Horizontal Line
                    if (y == point1.Lon || y == point2.Lon)
                    {
                        x = 0.0;
                    }
                    else
                    {
                        x = double.NaN;
                    }
                }
                else if (double.IsNaN(Slope))
                {
                    // Vertical line
                    x = point1.Lon;
                }
                else
                {
                    x = (y - YIntercept) / Slope;
                }

                return x;
            }



            public double SolveForY(double x)
            {
                var y = double.NaN;

                if (Slope == 0.0)
                {
                    // Horizontal line
                    y = point1.Lat;
                }
                else if (double.IsNaN(Slope))
                {
                    // Vertical line
                    if (x == point1.Lon || x == point2.Lon)
                    {
                        y = 0.0;
                    }
                    else
                    {
                        y = double.NaN;
                    }
                }
                else
                {
                    y = Slope * x + YIntercept;
                }
                return y;
            }


            /// <summary>
            /// Calculate slope and Y intercept
            /// Set Slope=0: horizontal line, NaN: vertical line
            /// </summary>
            /// <param name="point1"></param>
            /// <param name="point2"></param>
            private void CalculateSlopeIntercept()
            {
                if (point1.Lon == point2.Lon)
                {
                    // Vertical line
                    Slope = double.NaN;
                    YIntercept = double.NaN;
                    return;
                }

                Slope = (point1.Lat - point2.Lat) / (point1.Lon - point2.Lon);

                YIntercept = point1.Lat - Slope * point1.Lon;

            }

        }


    }

}
