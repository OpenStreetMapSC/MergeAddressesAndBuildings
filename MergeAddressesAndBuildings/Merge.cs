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

            DivideNewAddressesIntoBuckets();  // This effectively also removes the above duplicate address from index buckets
            AttachAddressesToNewBuildings();

            DivideNewAddressesIntoBuckets();  // This effectively also removes the above duplicate address from index buckets
            AttachAddressesToExistingBuildings();
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
                    foreach (OSMNode newAddrNode in indexBuckets[idx])
                    {
                        if (newAddrNode.Tags.ContainsKey("addr:housenumber") &&
                            newAddrNode.Tags.ContainsKey("addr:street"))
                        {
                            if (newAddrNode.Tags["addr:housenumber"] == osmAddrObject.Tags["addr:housenumber"] &&
                                newAddrNode.Tags["addr:street"] == osmAddrObject.Tags["addr:street"])
                            {
                                newAddresses.osmNodes.Remove(newAddrNode.ID);
                                actionCount++;
                            }
                        }
                    }
                }
            }
        }


        private void AttachAddressesToExistingBuildings()
        {
            actionCount = 0;

            // Buildings can be closed polygon or relation
            foreach (var building in osmData.osmWays.Values)
            {
                AttachAddressTo(building, building.Bbox);
            }
            foreach (var building in osmData.osmRelations.Values)
            {
                AttachAddressTo(building, building.Bbox);
            }

            Console.WriteLine($" - Attached {actionCount:N0} addresses to existing buildings");
        }

        private void AttachAddressesToNewBuildings()
        {
            actionCount = 0;

            // Buildings can be closed polygon or relation
            foreach ( var building in newBuildings.osmWays.Values)
            {
                AttachAddressTo(building, building.Bbox);
            }
            foreach (var building in newBuildings.osmRelations.Values)
            {
                AttachAddressTo(building, building.Bbox);
            }

            Console.WriteLine($" - Attached {actionCount:N0} addresses to new buildings");
        }

        /// <summary>
        /// Look for unique address node defined on building object
        /// </summary>
        /// <param name="building">As way or relation</param>
        private void AttachAddressTo(BaseOSM building, BBox bbox)
        {
            var searchRadius = GetSearchRadius(building.Lat, building.Lon, bbox);
            int nFound = 0;
            OSMNode addrNode = FindAttachedAddress(building, searchRadius, ref nFound);
            if (nFound == 0)
            {
                // Expand radius to catch address node not on building, but nearby
                // Assumption: most address nodes are in the center of buildings
                //    A few are on parcel but missed the building.
                searchRadius += 20.0; // Meters
                addrNode = FindAttachedAddress(building, searchRadius, ref nFound);
            }
            if (nFound == 1)
            {
                // Likely successful at locating the associated address:
                //  - Transfer tags to building and remove address node
                bool addrAdded = false;  // Whether tag was added
                foreach(var addrTag in addrNode.Tags.Keys)
                {
                    var addrValue = addrNode.Tags[addrTag];
                    if (!building.Tags.ContainsKey(addrTag))
                    {
                        building.Tags.Add(addrTag, addrValue);
                        addrAdded = true;
                    }
                }
                newAddresses.osmNodes.Remove(addrNode.ID); // Existing data already tagged
                if (addrAdded)
                {
                    actionCount++;
                    if (building.ID > 0)
                    {
                        MarkEdited(building);
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

        /// <summary>
        /// Search for a single address within specified radius
        /// </summary>
        /// <param name="building">As way or relation</param>
        /// <param name="radius">Radius, meters</param>
        /// <param name="nFound">Actual number found to distinguish between 0 or 2+ found</param>
        /// <returns>Single address, or null if zero or more than 1</returns>
        private OSMNode FindAttachedAddress(BaseOSM building, double radius, ref int nFound)
        {
            var addrNodes = new List<BaseOSM>();
            (var xList, var yList) = buckets.ReturnBucketList(building.Lat, building.Lon);
            for (int i=0; i< xList.Count; i++)
            {
                var idx = ArrIndex(xList[i], yList[i]);
                if (indexBuckets[idx] != null)
                {
                    foreach (var osmNode in indexBuckets[idx])
                    {
                        var distance = SpatialUtilities.Distance(building.Lat, osmNode.Lat, building.Lon, osmNode.Lon);
                        if (distance < radius)
                        {
                            addrNodes.Add(osmNode);
                        }
                    }
                }
            }

            if (addrNodes.Count > 1)
            {
                // Run longer exact algorithm to see if there really is just one node that doesn't 
                // match a simple radius check
                RemoveUncontainedNodes(building, addrNodes);
            }

            nFound = addrNodes.Count;
            if (nFound == 1)
            {
                return addrNodes[0] as OSMNode;
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="building">Way or relation</param>
        /// <param name="addrNodes"></param>
        private void RemoveUncontainedNodes(BaseOSM building, List<BaseOSM> addrNodes)
        {
            ClipBoundary clipBoundaryTest=null;
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
                        return;
                    }
                    break;
            }

            if (clipBoundaryTest == null) return;

            var removeList = new List<BaseOSM>();
            foreach (OSMNode addrNode in addrNodes)
            {
                if (!clipBoundaryTest.IsInBoundary(addrNode))
                {
                    removeList.Add(addrNode);
                }
            }
            
            // Remove uncontained nodes
            foreach (var removeNode in removeList)
            {
                addrNodes.Remove(removeNode);
            }
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
