using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MergeAddressesAndBuildings
{

    /// <summary>
    /// Remove identical address points at same location
    /// (Identical address points at different locations will remain)
    /// </summary>
    public class DuplicateAddress
    {
        private OSMDataset addrPoints;

        private const double MaxDistanceForDuplicate = 1.0; // Meters

        public DuplicateAddress(OSMDataset addrPoints)
        {
            this.addrPoints = addrPoints;
        }

        /// <summary>
        /// Removed identical addresses at same location
        /// </summary>
        /// <returns>Number of duplicates removed</returns>
        public int RemoveDuplicateAddresses()
        {

            // All nodes with given housenumber + street (may be from multiple parts of region)
            var removeList = new List<OSMNode>();  

            var addressHash = new Dictionary<string, List <OSMNode>> ();

            foreach (var node in addrPoints.osmNodes.Values)
            {

                var unit = "";
                if (node.Tags.ContainsKey("addr:unit"))
                {
                    unit = node.Tags["addr:unit"];
                }
                var hashStr = $"{node.Tags["addr:housenumber"]}|{node.Tags["addr:street"]}|{unit}";
                if (addressHash.ContainsKey(hashStr) ) {

                    // Possible Duplicate
                    var nodeList = addressHash[hashStr];
                    foreach (var testNode in nodeList)
                    {
                        var refLoc = new Coordinate(node.Lat, node.Lon);
                        var testLoc = new Coordinate(testNode.Lat, testNode.Lon);
                        if (testLoc.GreatCircleDistance(refLoc) < MaxDistanceForDuplicate)
                        {
                            removeList.Add(node);// Too close to another of the same address
                        }
                    }
                } else
                {
                    var nodeList = new List<OSMNode>();
                    nodeList.Add(node);
                    addressHash.Add(hashStr, nodeList);
                }
            }

            foreach (var node in removeList)
            {
                if (addrPoints.osmNodes.ContainsKey(node.ID))
                {
                    addrPoints.osmNodes.Remove(node.ID);
                }
            }

            return removeList.Count;
        }

    }
}
