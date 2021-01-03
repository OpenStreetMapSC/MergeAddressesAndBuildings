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
                // Create list of outer ways for quick match to way
                var outerWays = new Dictionary<long, RelationMember>();
                foreach (var member in relation.Members)
                {
                    if (member.MemberType == "way" && member.Role == "outer")
                    {
                        outerWays.Add(member.Ref, member);
                    }
                }


                // Create working list of ways
                var ways = new List<OSMWay>();
                foreach (var way in relation.OSMWays)
                {
                    if (outerWays.ContainsKey(way.ID))
                    {
                        boundaryBbox = SpatialUtilities.BboxUnion(boundaryBbox, way.Bbox);
                        ways.Add(way);
                    }
                }


                // Walk the ways to get contiguous list of joining nodes
                OSMNode lastNode = null;
                while (ways.Count > 0)
                {
                    List<OSMNode> nodeList = FindConnectingNodeList(relation.ID, lastNode, ways);
                    foreach (var node in nodeList)
                    {
                        var coordinate = new Coordinate(node.Lat, node.Lon);
                        boundaryCoordinates.Add(coordinate);
                    }
                    lastNode = nodeList[nodeList.Count-1];
                }

            }

        }


        public ClipBoundary(OSMWay osmWay)
        {

            boundaryBbox = osmWay.Bbox;
            boundaryCoordinates = new List<Coordinate>();
            foreach (var node in osmWay.NodeList)
            {
                var coordinate = new Coordinate(node.Lat, node.Lon);
                boundaryCoordinates.Add(coordinate);
            }
        }



        /// <summary>
        /// Find next connecting node in the list of ways.
        /// </summary>
        /// <param name="lastNode"></param>
        /// <param name="ways">List of ways to search.   Remove way when found</param>
        /// <returns>List of nodes, in ascending order (reversed from way if necessary)</returns>
        private List<OSMNode> FindConnectingNodeList(long relationID, OSMNode lastNode, List<OSMWay> ways)
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
                throw new Exception($"Gap in relation ID {relationID} outer ways - could not connect ways into linestring.");
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

        /// <summary>
        /// Algorithm has bugs - see test
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool IsInBoundary0(OSMNode node)
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


        /// <summary>
        /// Algorithm has bugs - see test
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool IsInBoundary1(OSMNode point)
        {
            bool inside = false;

            if (boundaryCoordinates.Count < 3)
            {
                return inside;
            }
            var a = boundaryCoordinates[boundaryCoordinates.Count-1]; // Start at end
            foreach (var b in boundaryCoordinates)
            {
                if ((b.Lon == point.Lon) && (b.Lat == point.Lat))
                    return true;

                if ((b.Lat == a.Lat) && (point.Lat == a.Lat) && (a.Lon <= point.Lon) && (point.Lon <= b.Lon))
                    return true;

                if ((b.Lat < point.Lat) && (a.Lat >= point.Lat) || (a.Lat < point.Lat) && (b.Lat >= point.Lat))
                {
                    if (b.Lon + (point.Lat - b.Lat) / (a.Lat - b.Lat) * (a.Lon - b.Lon) <= point.Lon)
                        inside = !inside;
                }
                a = b;
            }
            return inside;
        }




        public bool IsInBoundary(OSMNode node)
        {
            return wn_PnPoly(node) != 0;
        }

        /// <summary>
        ///  isLeft(): tests if a point is Left|On|Right of an infinite line.
        ///    Input:  three points P0, P1, and P2
        ///    Return: >0 for P2 left of the line through P0 and P1
        ///            =0 for P2  on the line
        ///            <0 for P2  right of the line
        ///    See: Algorithm 1 "Area of Triangles and Polygons" @ http://geomalgorithms.com/a03-_inclusion.html
        /// </summary>
        /// <param name="P0"></param>
        /// <param name="P1"></param>
        /// <param name="P2"></param>
        /// <returns></returns>
        private double isLeft(Coordinate P0, Coordinate P1, Coordinate P2)
        {
            return ((P1.Lon - P0.Lon) * (P2.Lat - P0.Lat)
                    - (P2.Lon - P0.Lon) * (P1.Lat - P0.Lat));
        }


        /// <summary>
        /// winding number test for a point in a polygon
        //      Input:   P = a point,
        //               boundaryCoordinates[] = vertex points of a polygon V[n+1] with V[n]=V[0]
        //      Return:  wn = the winding number (=0 only when P is outside)
        /// </summary>
        /// <param name="P">Point </param>
        /// <returns></returns>
        public int wn_PnPoly(OSMNode P)
        {
            Coordinate point = new Coordinate(P.Lat, P.Lon);

            int wn = 0;    // the  winding number counter

            // loop through all edges of the polygon
            for (int i = 0; i < boundaryCoordinates.Count-1; i++)
            {   // edge from V[i] to  V[i+1]

                //var next = V[(i + 1) % V.Count];
                var next = boundaryCoordinates[i + 1];

                if (boundaryCoordinates[i].Lat <= point.Lat)
                {          // start y <= P.Lat
                    if (next.Lat > point.Lat)      // an upward crossing
                        if (isLeft(boundaryCoordinates[i], next, point) > 0)  // P left of  edge
                            ++wn;            // have  a valid up intersect
                }
                else
                {                        // start y > P.Lat (no test needed)
                    if (next.Lat <= point.Lat)     // a downward crossing
                        if (isLeft(boundaryCoordinates[i], next, point) < 0)  // P right of  edge
                            --wn;            // have  a valid down intersect
                }
            }
            return wn;
        }
    }



}
