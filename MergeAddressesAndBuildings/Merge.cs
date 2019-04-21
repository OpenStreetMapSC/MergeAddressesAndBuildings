using System;
using System.Collections.Generic;
using System.Text;

namespace MergeAddressesAndBuildings
{
    /// <summary>
    /// Attach addresses to new or existing buildings that don't already have an address
    /// Check for address already defined; don't import if so.
    /// Result is combined data set of new and edited OSM data ready for validation and upload
    /// </summary>
    public class Merge
    {

        private Buckets buckets;
        private OSMDataset newAddresses;
        private OSMDataset newBuildings;
        private OSMDataset osmData;

        private List<BaseOSM>[] indexBuckets;

        private int actionCount;


        /// <summary>
        /// Perform merge
        /// </summary>
        /// <param name="buckets">Sections covering full data area</param>
        /// <param name="newAddresses"></param>
        /// <param name="newBuildings"></param>
        /// <param name="osmData">Existing buildings and objects tagged with addr:*</param>
        public Merge(Buckets buckets,
            OSMDataset newAddresses, OSMDataset newBuildings, OSMDataset osmData)
        {
            this.buckets = buckets;
            this.newAddresses = newAddresses;
            this.newBuildings = newBuildings;
            this.osmData = osmData;
        }

        public void PerformMerge()
        {
            CheckDuplicateBuilding();

            DivideNewAddressesIntoBuckets();
            RemoveNewDuplicateAddresses();

            AttachInteriorAddresses();
            AttachNearbyAddresses();
        }

        private void AttachNearbyAddresses()
        {
            actionCount = 0;
            AttachNearbyAddressesTo(osmData);
            AttachNearbyAddressesTo(newBuildings);

            Console.WriteLine($" - Attached {actionCount:N0} nearby addresses");
        }

        /// <summary>
        /// Attach addresses to buildings near a single address
        /// </summary>
        private void AttachNearbyAddressesTo(OSMDataset buildings)
        {
            foreach (var building in buildings.osmWays.Values)
            {
                AttachNearbyhAddressTo(building, building.Bbox);
            }
            foreach (var building in buildings.osmRelations.Values)
            {
                AttachNearbyhAddressTo(building, building.Bbox);
            }

        }



        /// <summary>
        /// Look for unique address node defined near building object
        /// </summary>
        /// <param name="building">As way or relation</param>
        private void AttachNearbyhAddressTo(BaseOSM building, BBox bbox)
        {
            if (building.Tags.ContainsKey("addr:housenumber") ||
                building.InnerAttributes.ContainsKey("action")) return; // Address already attached or already edited


            var searchRadius = GetSearchRadius(building.Lat, building.Lon, bbox);
            searchRadius += 5.0; // Meters - include area around building - can't extend too far without including multiple neighbor addresses

            OSMNode addNode = null;  // Possible node to add
            List<BaseOSM> addrBucket = null; // Bucket containing add node
            int nodeCount = 0; // # of nodes found
            (var xList, var yList) = buckets.ReturnBucketList(building.Lat, building.Lon);
            for (int i = 0; i < xList.Count; i++)
            {
                var idx = ArrIndex(xList[i], yList[i]);
                if (indexBuckets[idx] != null)
                {
                    foreach (OSMNode addrNode in indexBuckets[idx])
                    {
                        var distance = SpatialUtilities.Distance(building.Lat, addrNode.Lat, building.Lon, addrNode.Lon);
                        if (distance < searchRadius)
                        {
                            addNode = addrNode;
                            addrBucket = indexBuckets[idx];
                            nodeCount++;
                        }
                    }
                }
            }

            if (nodeCount == 1)
            {
                // Building near exactly 1 node
                AttachNode(building, addNode, addrBucket);
                actionCount++;
            }

        }




        /// <summary>
        /// Attach addresses to buildings containing just that single address
        /// </summary>
        private void AttachInteriorAddresses()
        {
            actionCount = 0;
            AttachInteriorAddressesTo(osmData);
            AttachInteriorAddressesTo(newBuildings);

            Console.WriteLine($" - Attached {actionCount:N0} interior addresses");
        }

        /// <summary>
        /// Attach addresses to buildings containing just that single address
        /// </summary>
        private void AttachInteriorAddressesTo(OSMDataset buildings)
        {
            foreach (var building in buildings.osmWays.Values)
            {
                AttachInteriorhAddressTo(building, building.Bbox);
            }
            foreach (var building in buildings.osmRelations.Values)
            {
                AttachInteriorhAddressTo(building, building.Bbox);
            }

        }



        /// <summary>
        /// Look for unique address node defined on building object
        /// </summary>
        /// <param name="building">As way or relation</param>
        private void AttachInteriorhAddressTo(BaseOSM building, BBox bbox)
        {
            if (building.Tags.ContainsKey("addr:housenumber")) return; // Address already attached

            OSMNode addNode = null;  // Possible node to add
            List<BaseOSM> addrBucket = null; // Bucket containing add node
            int nodeCount = 0; // # of nodes found
            (var xList, var yList) = buckets.ReturnBucketList(building.Lat, building.Lon);
            for (int i = 0; i < xList.Count; i++)
            {
                var idx = ArrIndex(xList[i], yList[i]);
                if (indexBuckets[idx] != null)
                {
                    foreach (OSMNode addrNode in indexBuckets[idx])
                    {
                        // Confirm that node is within building outline
                        if (SpatialUtilities.BBoxContains(bbox, addrNode)) // Fast test to exclude outliers
                        {
                            if (BuildingContainsNode(building, addrNode))
                            {
                                addNode = addrNode;
                                addrBucket = indexBuckets[idx];
                                nodeCount++;
                            }

                        }
                    }
                }
            }

            if (nodeCount == 1)
            {
                // Building contains exactly 1 node
                AttachNode(building, addNode, addrBucket);
                actionCount++;
            }

        }

        private void AttachNode(BaseOSM building, OSMNode addrNode, List<BaseOSM> addrBucket)
        {

            //  - Transfer tags to building and remove address node
            bool addrAdded = false;  // Whether tag was added
            foreach (var addrTag in addrNode.Tags.Keys)
            {
                var addrValue = addrNode.Tags[addrTag];
                if (!building.Tags.ContainsKey(addrTag))
                {
                    building.Tags.Add(addrTag, addrValue);
                    addrAdded = true;
                }
            }
            newAddresses.osmNodes.Remove(addrNode.ID); // Existing data already tagged
            addrBucket.Remove(addrNode);
            if (addrAdded)
            {
                actionCount++;
                if (building.ID > 0)
                {
                    MarkEdited(building);
                }
            }
        }




            /// <summary>
            /// See if any existing building conflicts with new building
            /// </summary>
            private void CheckDuplicateBuilding()
        {
            actionCount = 0;
            DivideNewBuildingsIntoBuckets();
            foreach (var building in osmData.osmWays.Values)
            {
                CheckOverlappedBuilding(building, building.Bbox);
            }
            foreach (var building in osmData.osmRelations.Values)
            {
                CheckOverlappedBuilding(building, building.Bbox);
            }

            Console.WriteLine($" - Removed {actionCount:N0} overlapping buildings");
        }

        private void CheckOverlappedBuilding(BaseOSM osmObject, BBox bbox)
        {
            (var xList, var yList) = buckets.ReturnBucketList(osmObject.Lat, osmObject.Lon);
            for (int i = 0; i < xList.Count; i++)
            {
                var idx = ArrIndex(xList[i], yList[i]);
                if (indexBuckets[idx] != null)
                {
                    foreach (OSMWay newBuilding in indexBuckets[idx])
                    {
                        if (SpatialUtilities.BBoxIntersects(newBuilding.Bbox, bbox))
                        {
                            newBuildings.osmWays.Remove(newBuilding.ID);
                            actionCount++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Remove new addresses already defined in OSM
        /// </summary>
        private void RemoveNewDuplicateAddresses()
        {
            actionCount = 0;

            foreach (var node in osmData.osmNodes.Values)
            {
                CheckForDuplicateAddress(node);
            }
            foreach (var way in osmData.osmWays.Values)
            {
                CheckForDuplicateAddress(way);
            }
            foreach (var relation in osmData.osmRelations.Values)
            {
                CheckForDuplicateAddress(relation);
            }

            Console.WriteLine($" - Removed {actionCount:N0} duplicate addresses");

        }


        /// <summary>
        /// Check existing OSM data for duplicate new address node.
        ///    Remove new node if found
        /// </summary>
        /// <param name="osmAddrObject"></param>
        private void CheckForDuplicateAddress(BaseOSM osmAddrObject)
        {

            if (!osmAddrObject.Tags.ContainsKey("addr:housenumber") || !osmAddrObject.Tags.ContainsKey("addr:street")) return; // Incomplete address

            (var xList, var yList) = buckets.ReturnBucketList(osmAddrObject.Lat, osmAddrObject.Lon);
            for (int i = 0; i < xList.Count; i++)
            {
                var idx = ArrIndex(xList[i], yList[i]);
                if (indexBuckets[idx] != null)
                {
                    var addrRemoveList = new List<OSMNode>();
                    foreach (OSMNode newAddrNode in indexBuckets[idx])
                    {
                        if (newAddrNode.Tags.ContainsKey("addr:housenumber") &&
                            newAddrNode.Tags.ContainsKey("addr:street"))
                        {
                            if (newAddrNode.Tags["addr:housenumber"] == osmAddrObject.Tags["addr:housenumber"] &&
                                newAddrNode.Tags["addr:street"] == osmAddrObject.Tags["addr:street"])
                            {
                                newAddresses.osmNodes.Remove(newAddrNode.ID);
                                addrRemoveList.Add(newAddrNode);
                                actionCount++;
                            }
                        }
                    }

                    // Also remove node from bucket
                    foreach(var removeNode in addrRemoveList)
                    {
                        indexBuckets[idx].Remove(removeNode);
                    }
                }
            }
        }



       
        /// <summary>
        /// Update OSM object attributes to show edited and trigger upload
        /// </summary>
        /// <param name="building"></param>
        private void MarkEdited(BaseOSM building)
        {
            building.InnerAttributes.Add("action", "modify");
            //Int64 version = Convert.ToInt64(building.InnerAttributes["version"]);
            //version++;
            //building.InnerAttributes["version"] = version.ToString();
        }



        private bool BuildingContainsNode(BaseOSM building, OSMNode testNode)
        {
            ClipBoundary clipBoundaryTest = null;
            switch (building)
            {
                case OSMWay buildingWay:
                    clipBoundaryTest = new ClipBoundary(buildingWay);
                    break;
                case OSMRelation buildingRelation:
                    var relationList = new Dictionary<long, OSMRelation>();  // As expected by function
                    relationList.Add(buildingRelation.ID, buildingRelation);

                    try
                    {
                        clipBoundaryTest = new ClipBoundary(relationList);
                    }
                    catch (Exception)
                    {
                        // Building contains multiple outer ways or not closed object
                        return false;
                    }
                    break;
            }

            if (clipBoundaryTest == null) return false;

            return clipBoundaryTest.IsInBoundary(testNode);
        }



        /// <summary>
        /// Calculate search radius around bbox based on center
        /// </summary>
        /// <param name="lat">Center</param>
        /// <param name="lon">Center</param>
        /// <param name="bbox">Overall bbox</param>
        /// <returns></returns>
        private double GetSearchRadius(double lat, double lon, BBox bbox)
        {
            // From center to a corner
            var latLen = SpatialUtilities.Distance(lat, bbox.MinLat, lon, bbox.MinLon);
            return latLen;
        }


        /// <summary>
        /// Get multi dimensional index into a single dimensional array
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private int ArrIndex(int x, int y)
        {
            return (y * buckets.NHorizontal) + x;
        }


        private void DivideNewBuildingsIntoBuckets()
        {
            indexBuckets = new List<BaseOSM>[buckets.NHorizontal * buckets.NVertical];

            foreach (var building in newBuildings.osmWays.Values)
            {
                (var x, var y) = buckets.ReturnBucket(building.Lat, building.Lon);
                AddToBucket(building, x, y);
            }


        }

        private void DivideNewAddressesIntoBuckets()
        {
            indexBuckets = new List<BaseOSM>[buckets.NHorizontal * buckets.NVertical];

            foreach (var addrNode in newAddresses.osmNodes.Values)
            {
                (var x, var y) = buckets.ReturnBucket(addrNode.Lat, addrNode.Lon);
                AddToBucket(addrNode, x, y);
            }


        }

        private void AddToBucket(BaseOSM osmObject, int x, int y)
        {
            var idx = ArrIndex(x, y);
            if (indexBuckets[idx] == null) indexBuckets[idx] = new List<BaseOSM>();
            indexBuckets[idx].Add(osmObject);
        }
    }
}
