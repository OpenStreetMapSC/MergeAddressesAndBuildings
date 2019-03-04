using System;
using System.Collections.Generic;
using System.Text;

namespace MergeAddressesAndBuildings
{
    /// <summary>
    /// Used to index data into localized areas so that simple searches only search nearby
    /// data for hits rather than the entire dataset.
    /// 
    /// Not all buckets contain data if they lie outside of odd shaped polygons or no data applies
    /// 
    /// Lower left bucket is 0,0 outerBox.MinLat, outerBox.MinLon
    /// </summary>
    public class Buckets
    {
        private BBox outerBbox;

        public int NVertical { get; set; }
        public int NHorizontal { get; set; }
        private double bucketLat = 0.0; // Lat span of 1 bucket
        private double bucketLon = 0.0;

        /// <summary>
        /// Divide space into squares
        /// </summary>
        /// <param name="outerBoundary"></param>
        /// <param name="boxSize">Square size in Meters</param>
        public Buckets(Dictionary<Int64, OSMWay> outerBoundary, double boxSize)
        {
            // Determine complete bbox
            BBox bbox = new BBox();
            foreach (var way in outerBoundary.Values)
            {
                bbox = SpatialUtilities.BboxUnion(way.Bbox, bbox);
            }
            AddBuffer(bbox);
            outerBbox = bbox;

            CalculateSplit(boxSize);
        }


        /// <summary>
        /// Return bounding box defined by the cell X, Y
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public BBox GetBBox(int x, int y)
        {
            var bbox = new BBox();
            bbox.MinLat = outerBbox.MinLat + (y * bucketLat);
            bbox.MinLon = outerBbox.MinLon + (x * bucketLon);
            bbox.MaxLat = bbox.MinLat + bucketLat;
            bbox.MaxLon = bbox.MinLon + bucketLon;
            return bbox;
        }

        public double BucketWidth { get; private set; }
        public double BucketHeight { get; private set; }


        /// <summary>
        /// Divide into squares
        /// </summary>
        private void CalculateSplit(double boxSize)
        {

            double widthMeters = SpatialUtilities.Distance(
                outerBbox.MinLat,
                outerBbox.MinLat,
                outerBbox.MinLon,
                outerBbox.MaxLon);
            double heightMeters = SpatialUtilities.Distance(
                outerBbox.MinLat,
                outerBbox.MaxLat,
                outerBbox.MinLon,
                outerBbox.MinLon);

            NHorizontal = (int)(widthMeters / boxSize);
            NVertical = (int)(heightMeters / boxSize);

            bucketLat = (outerBbox.MaxLat - outerBbox.MinLat) / NVertical;
            bucketLon = (outerBbox.MaxLon - outerBbox.MinLon) / NHorizontal;

            BucketWidth = bucketLon;
            BucketHeight = bucketLat;

        }

        /// <summary>
        /// Add space around boundary to ensure all points will be within a block, and not on
        /// an outer edge.
        /// </summary>
        /// <param name=""></param>
        private void AddBuffer(BBox bbox)
        {
            var buffer = 0.001; // Degrees
            bbox.MinLat -= buffer;
            bbox.MaxLat += buffer;
            bbox.MinLon -= buffer;
            bbox.MaxLon += buffer;
        }


        /// <summary>
        /// Calculate which bucket contains the lat / lon
        /// </summary>
        /// <param name="lat"></param>
        /// <param name="lon"></param>
        /// <returns>X, Y bucket number</returns>
        public (int x, int y) ReturnBucket(double lat, double lon)
        {
            var x = (int)((lon - outerBbox.MinLon) / bucketLon);
            var y = (int)((lat - outerBbox.MinLat) / bucketLat);

            if (y >= NVertical)
            {
                y = NVertical-1;
            }
            if (x >= NHorizontal)
            {
                x = NHorizontal-1;
            }

            return (x, y);
        }


        /// <summary>
        /// Calculate which bucket contains the lat / lon and return
        /// along with adjoining buckets
        /// </summary>
        /// <param name="lat"></param>
        /// <param name="lon"></param>
        /// <returns>list of X, Y surrounding bucket numbers</returns>
        public (List<int> xList, List<int> yList) ReturnBucketList(double lat, double lon)
        {
            var xList = new List<int>();
            var yList = new List<int>();

            (var x, var y) = ReturnBucket(lat, lon);

            // Calculate surrounding positions and include all within array boundaries
            for (int xSurround = x-1; xSurround <= x+1; xSurround++)
            {
                for (int ySurround = y-1; ySurround <= y+1; ySurround++)
                {
                    if (xSurround > 0  && xSurround < NHorizontal &&
                        ySurround > 0 && ySurround < NVertical)
                    {
                        xList.Add(xSurround); yList.Add(ySurround);

                    }
                }

            }

            return (xList, yList);

        }

    }
}
