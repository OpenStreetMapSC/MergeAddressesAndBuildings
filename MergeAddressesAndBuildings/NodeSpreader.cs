using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MergeAddressesAndBuildings
{
    public class NodeSpreader
    {
        double separateDistance = 1.0; // Meters

        List<SortedList<string, OSMNode>> groups = new List<SortedList<string, OSMNode>>();
        List<OSMNode> addressNodes;


        /// <summary>
        /// Separate nodes at same position by at least some delta to allow OSM editors
        /// to click on and inspect or edit.
        /// </summary>
        /// <param name="addressNodes">One or more groups of nodes, often at the 
        ///        exact coordinates but with different unit numbers</param>
        public NodeSpreader(List<OSMNode> addressNodes)
        {

            this.addressNodes = addressNodes;

        }


        public void SpreadNodes()
        {
            // Separate nodes into groups
            foreach (var node in addressNodes)
            {
                PlaceNodeInGroup(node);
            }

            // Spread out nodes in each group
            foreach (var group in groups)
            {
                SpreadNodeGroup(group);
            }

        }

        /// <summary>
        /// Separate nodes into grid of rows and columns
        /// </summary>
        /// <param name="group"></param>
        private void SpreadNodeGroup(SortedList<string, OSMNode> group)
        {
            int maxRows = 5;

            if (group.Count < 2) return; // This group has no stacked nodes

            var curLoc = new Coordinate(group.Values[0].Lat, group.Values[0].Lon);  // Use first node as reference

            // Locate top so that middle of row lies near main position
            var moveUpDistance = (group.Count * separateDistance) / 2.0;
            curLoc = SpatialUtilities.MoveByToward(curLoc, moveUpDistance, 0.0); // Move up to begin
            var moveLeftDistance = (double)(group.Count / maxRows) * separateDistance / 2.0;
            curLoc = SpatialUtilities.MoveByToward(curLoc, moveLeftDistance, 270.0); // Move left to top left
            var topRef = curLoc;
            var count = 0;
            foreach (KeyValuePair<string, OSMNode> kvp in group)
            {
                var editNode = kvp.Value;
                editNode.Lat = curLoc.Lat;
                editNode.InnerAttributes["lat"] = curLoc.Lat.ToString();
                editNode.Lon = curLoc.Lon;
                editNode.InnerAttributes["lon"] = curLoc.Lon.ToString();
                count++;
                if (count % maxRows == 0)
                {
                    // Go back to top and move to new column
                    topRef = SpatialUtilities.MoveByToward(topRef, separateDistance, 90.0);
                    curLoc = topRef;
                }
                else
                {
                    curLoc = SpatialUtilities.MoveByToward(curLoc, separateDistance, 180.0);
                }
            }
        }

        private void PlaceNodeInGroup(OSMNode node)
        {
            // Find group for this node
            foreach (var group in groups)
            {
                if (node.GreatCircleDistance(group.Values[0]) < (separateDistance / 2.0))
                {
                    group.Add(AddressKey(node), node);
                    return;
                }
            }

            // No other nearby nodes found, create new group and add node
            var newGroup = new SortedList<string, OSMNode>();
            newGroup.Add(AddressKey(node), node);
            groups.Add(newGroup);
        }


        /// <summary>
        /// Address housenumber and unit should be unique for this position
        /// (Exact duplicates already removed)
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private string AddressKey(OSMNode node)
        {
            var unit = "";
            if (node.Tags.ContainsKey("addr:unit"))
            {
                unit = node.Tags["addr:unit"];
            }

            var key = node.Tags["addr:housenumber"] + "|" + unit;
            return key;
        }
    }
}
