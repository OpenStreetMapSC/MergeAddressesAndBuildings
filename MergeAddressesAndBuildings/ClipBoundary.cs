using System;
using System.Collections.Generic;
using System.Text;

namespace MergeAddressesAndBuildings
{
    public class ClipBoundary
    {
        List<Coordinate> boundaryCoordinates;
        BBox boundaryBbox;


        public ClipBoundary(Dictionary<long, OSMRelation> relations)
        {

            boundaryBbox = new BBox();
            boundaryCoordinates = new List<Coordinate>();
            // Expect only 1 relation in county boundary query
            if (relations.Count > 1)
            {
                throw new Exception($"Expected 1 relation for County boundary, got {relations.Count}\n\n");
            }

            foreach (var relation in relations.Values)
            {
                // Create working list of ways
                var ways = new List<OSMWay>();
                foreach (var way in relation.OSMWays)
                {
                    boundaryBbox = SpatialUtilities.BboxUnion(boundaryBbox, way.Bbox);
                    ways.Add(way);
                }


                // Walk the ways to get contiguous list of joining nodes
                OSMNode lastNode = null;
                while (ways.Count > 0)
                {
                    List<OSMNode> nodeList = FindConnectingNodeList(lastNode, ways);
                    foreach (var node in nodeList)
                    {
                        var coordinate = new Coordinate(node.Lat, node.Lon);
                        boundaryCoordinates.Add(coordinate);
                    }
                    lastNode = nodeList[nodeList.Count-1];
                }

            }

        }

        /// <summary>
        /// Find next connecting node in the list of ways.
        /// </summary>
        /// <param name="lastNode"></param>
        /// <param name="ways">List of ways to search.   Remove way when found</param>
        /// <returns>List of nodes, in ascending order (reversed from way if necessary)</returns>
        private List<OSMNode> FindConnectingNodeList(OSMNode lastNode, List<OSMWay> ways)
        {


            List<OSMNode> nodeList = null;

            // Test for first pass
            if (lastNode == null)
            {
                nodeList = ways[0].NodeList;
                ways.Remove(ways[0]);
                return nodeList;
            }


            OSMWay foundWay = null;
            foreach(var way in ways)
            {
                // See if forward order
                var testNode = way.NodeList[0];
                if (SamePosition(testNode, lastNode))
                {
                    nodeList = way.NodeList;
                    foundWay = way;
                    break;
                }

                // See if reverse order
                testNode = way.NodeList[way.NodeList.Count-1];
                if (SamePosition(testNode, lastNode))
                {
                    nodeList = way.NodeList;
                    nodeList.Reverse(); // This reverses the list object in way also, but the way node list is not used again
                    foundWay = way;
                    break;
                }
            }

            if (foundWay == null)
            {
                throw new Exception($"Gap in county border - could not connect ways into linestring.");
            }

            ways.Remove(foundWay);

            return nodeList;
        }


        private bool SamePosition(OSMNode n1, OSMNode n2)
        {
            var isDuplicate = false;
            double Epsilon = 1.0E-10;

            if ((Math.Abs(n1.Lat - n2.Lat) < Epsilon) &&
                    (Math.Abs(n1.Lon - n2.Lon) < Epsilon))
            {
                return true;
            }

            return isDuplicate;

        }

        public bool IsInBoundary(OSMNode node)
        {
            if (!SpatialUtilities.BBoxContains(boundaryBbox, node)) return false;

            Coordinate p1, p2;
            bool inside = false;

            if (boundaryCoordinates.Count < 3)
            {
                return inside;
            }

            var oldCoordinate = boundaryCoordinates[boundaryCoordinates.Count - 1];

            for (int i = 0; i < boundaryCoordinates.Count; i++)
            {
                var newCoordinate = boundaryCoordinates[i];

                if (newCoordinate.Lon > oldCoordinate.Lon)
                {
                    p1 = oldCoordinate;
                    p2 = newCoordinate;
                }
                else
                {
                    p1 = newCoordinate;
                    p2 = oldCoordinate;
                }

                if ((newCoordinate.Lon < node.Lon) == (node.Lon <= oldCoordinate.Lon)
                    && (node.Lat - p1.Lat) * (p2.Lon - p1.Lon)
                    < (p2.Lat - p1.Lat) * (node.Lon - p1.Lon))
                {
                    inside = !inside;
                }

                oldCoordinate = newCoordinate;
            }

            return inside;
        }

    }
}
