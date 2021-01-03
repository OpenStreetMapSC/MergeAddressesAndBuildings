using System;
using System.Collections.Generic;
using System.Text;


namespace MergeAddressesAndBuildings
{
    public class Coordinate
    {
        public double Lat { get; set; }
        public double Lon { get; set; }

        public Coordinate(double lat, double lon)
        {
            Lat = lat;
            Lon = lon;
        }
        public Coordinate(string lat, string lon)
        {
            Lat = Convert.ToDouble(lat);
            Lon = Convert.ToDouble(lon);
        }

        public double DegreeToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }


        public double RadianToDegree(double angle)
        {
            return angle * (180.0 / Math.PI);
        }

        public bool Equals(Coordinate otherCoordinate)
        {
            return (this.Lat == otherCoordinate.Lat) && (this.Lon == otherCoordinate.Lon);
        }

        /// <summary>
        /// Calculate angle in degrees from this coordinate to the other coordinate.
        /// No data valildity pre-check included
        /// </summary>
        /// <param name="otherCoordinate"></param>
        /// <returns>Angle (0..360.0), 0 = North</returns>
        public double DirectionToward(Coordinate otherCoordinate)
        {
            // atan2(y2−y1,x2−x1)
            var radDirection = Math.Atan2(otherCoordinate.Lon - Lon, otherCoordinate.Lat - Lat);
            var angle = RadianToDegree(radDirection);
            if (angle < 0) angle = 360.0 + angle;
            if (angle >= 360.0) angle -= 360.0;
            return angle;
        }

        /// <summary>
        /// Computes the distance between this coordinate and another point on the earth.
        /// Uses spherical law of cosines formula, not Haversine.
        /// </summary>
        /// <param name="other">The other point</param>
        /// <returns>Distance in meters</returns>
        public double GreatCircleDistance(Coordinate other)
        {
            var epsilon = Math.Abs(other.Lon - Lon) + Math.Abs(other.Lat - Lat);
            if (epsilon < 1.0e-6) return 0.0;

            double meters = (Math.Acos(
                    Math.Sin(DegreeToRadians(Lat)) * Math.Sin(DegreeToRadians(other.Lat)) +
                    Math.Cos(DegreeToRadians(Lat)) * Math.Cos(DegreeToRadians(other.Lat)) *
                    Math.Cos(DegreeToRadians(other.Lon - Lon))) * 6378135);

            return (meters);
        }

        /// <summary>
        /// Compute distance between this coordinate and another point on the earth when
        /// the two points are close to each other (within a Km) making the error
        /// from omitting the earth radius normally less than any land slope.
        /// </summary>
        /// <param name="other">The other point</param>
        /// <returns>Distance in meters</returns>
        public double Distance(Coordinate other)
        {
            var lengthOfDegree = 110250.0;  // Average, in Meters
            var x = Lat - other.Lat;
            var y = (Lon - other.Lon) * Math.Cos(other.Lat);
            return lengthOfDegree * Math.Sqrt(x * x + y * y);
        }

    }
}