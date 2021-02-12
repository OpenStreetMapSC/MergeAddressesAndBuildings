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

        private int geometryReplaced;

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
            newBuildings.ResetUsedFlag();

            newBuildings.SetConnectedWays();
            osmData.SetConnectedWays();

            CheckDuplicateBuilding();

            DivideNewAddressesIntoBuckets();
            RemoveNewDuplicateAddresses();

            AttachInteriorAddresses();
            AttachNearbyAddresses();

            SpreadRemainingAddresses();
        }

        /// <summary>
        /// This is the step after other merge is complete so that 
        /// address nodes are not nudged outside of building boundaries
        /// before the building is analyzed
        /// </summary>
        private void SpreadRemainingAddresses()
        {
            foreach (var bucket in indexBuckets)
            {
                if (bucket != null)
                {
                    var addressList = new List<OSMNode>();
                    foreach (OSMNode addrNode in bucket)
                    {
                        if (addrNode.Tags.ContainsKey("addr:housenumber"))
                        {
                            addressList.Add(addrNode);
                        }
                    }
                    var spreadNodes = new NodeSpreader(addressList);
                    spreadNodes.SpreadNodes();
                }
            }
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
            if (building.Tags.ContainsKey("addr:housenumber") ) return; // Address already attached or already edited


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
            foreach (var building in buildings.osmRelations.Values)
            {
                AttachInteriorAddressTo(building, building.Bbox);
            }
            foreach (var building in buildings.osmWays.Values)
            {
                AttachInteriorAddressTo(building, building.Bbox);
            }

        }

        /// <summary>
        /// Look for unique address node defined on building object
        /// </summary>
        /// <param name="building">As way or relation</param>
        private void AttachInteriorAddressTo(BaseOSM building, BBox bbox)
        {
            if (building.Tags.ContainsKey("addr:housenumber")) return; // Address already attached

            List<BaseOSM> addrBucket = null; // Bucket containing add node
            List<OSMNode> containedAddressNodes = new List<OSMNode>();  // Addresses found inside building outline
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
                                containedAddressNodes.Add(addrNode);
                                addrBucket = indexBuckets[idx];
                            }

                        }
                    }
                }
            }

            switch (containedAddressNodes.Count)
            {
                case 0:
                    // No address for this building
                    break;
                case 1:
                    // Building contains exactly 1 node
                    AttachNode(building, containedAddressNodes[0], addrBucket);
                    break;
                case 2:
                    if (building.Tags.ContainsKey("building"))
                    {
                        if (building.Tags["building"] == "apartments")
                        {
                            // North American term 'Duplex'
                            building.Tags["building"] = "semidetached_house";
                        }
                    }
                    var spreadNodes = new NodeSpreader(containedAddressNodes);
                    spreadNodes.SpreadNodes();
                    break;
                default:
                    var spreadNodes2 = new NodeSpreader(containedAddressNodes);
                    spreadNodes2.SpreadNodes();
                    break;
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
            foreach (var building in osmData.osmRelations.Values)
            {
                CheckOverlappedBuilding(building, building.OuterWay, building.Bbox);
            }
            foreach (var building in osmData.osmWays.Values)
            {
                CheckOverlappedBuilding(building, building, building.Bbox);
            }

            Console.WriteLine($" - Removed {actionCount:N0} overlapping buildings, replaced geometry on {geometryReplaced:N0} buildings.");
        }


        /// <summary>
        /// See if any new building footprints conflict with an existing building - 
        /// remove new building from candidate set if so.
        /// </summary>
        /// <param name="osmObject">Existing building as way or relation</param>
        /// <param name="osmBuildingOutline">Outer building way</param>
        /// <param name="bbox">Overall bbox of existing building</param>
        private void CheckOverlappedBuilding(BaseOSM osmObject, OSMWay osmBuildingOutline, BBox bbox)
        {
            (var xList, var yList) = buckets.ReturnBucketList(osmObject.Lat, osmObject.Lon);
            for (int i = 0; i < xList.Count; i++)
            {
                var idx = ArrIndex(xList[i], yList[i]);
                if (indexBuckets[idx] != null)
                {

                    // Check relations and remove dups, along with member ways
                    foreach (BaseOSM newBuilding in indexBuckets[idx])
                    {
                        if (newBuilding.GetType() == typeof(OSMRelation))
                        {
                            var newRelation = newBuilding as OSMRelation;
                            // BBbox check is fast; if that shows possible conflict, check for actual overlap 
                            // using PolygonsTouchOrIntersect()
                            if (SpatialUtilities.BBoxIntersects(newRelation.Bbox, bbox) &&
                                SpatialUtilities.PolygonsTouchOrIntersect(newRelation.OuterWay, osmBuildingOutline) )
                            {
                                actionCount++;
                                foreach (var way in newRelation.OSMWays)
                                {
                                    newBuildings.osmWays.Remove(way.ID);
                                }
                                newBuildings.osmRelations.Remove(newRelation.ID);
                            }
                        }
                    }


                    // Remove duplicate buildings as ways
                    foreach (BaseOSM newBuilding in indexBuckets[idx])
                    {
                        if (newBuilding.GetType() != typeof(OSMRelation))
                        {
                            var newWay = newBuilding as OSMWay;
                            if (!newWay.IsUsed)
                            {
                                // BBbox check is fast; if that shows possible conflict, check for actual overlap 
                                // using PolygonsTouchOrIntersect()
                                if (SpatialUtilities.BBoxIntersects(newWay.Bbox, bbox) &&
                                    SpatialUtilities.PolygonsTouchOrIntersect(newWay, osmBuildingOutline) &&
                                    SpatialUtilities.BBoxOverlapPercent(newWay.Bbox, bbox) > 50.0)
                                {

                                    if (osmBuildingOutline.Tags.ContainsKey("building") &&
                                        newWay.Tags.ContainsKey("building") )
                                    {
                                        var existingBuildingTag = osmBuildingOutline.Tags["building"];
                                        var newBuildingTag = newBuilding.Tags["building"];
                                        if (existingBuildingTag == "yes")
                                        {
                                            // Replace generic building tag with possible better tag
                                            osmBuildingOutline.Tags["building"] = newBuilding.Tags["building"];
                                        }
                                    }

                                    if (GeometryReplaced(osmBuildingOutline, newWay))
                                    {
                                        newWay.IsUsed = true;
                                        geometryReplaced++;
                                    }
                                    actionCount++;
                                    newBuildings.osmWays.Remove(newBuilding.ID);

                                }
                            }

                        }
                    }


                }
            }
        }

        /// <summary>
        /// Replace geometry of existing building with new building if it does not share nodes
        /// with any other building.
        /// </summary>
        /// <param name="osmBuildingOutline"></param>
        /// <param name="newWay"></param>
        private bool GeometryReplaced(OSMWay osmBuildingOutline, OSMWay newWay)
        {
            if (osmBuildingOutline.InnerAttributes.ContainsKey("action") ||
                osmBuildingOutline.NodeList[0].InnerAttributes.ContainsKey("action"))
            {
                // Was already edited
                return false;
            }

            var lastEditDate = DateTime.Now;
            var lastEditor = "";
            // Special precondition check for Greenville City if building from
            // import account or older than 2013 and questionable imagery
            bool replace = false;
            if (osmBuildingOutline.InnerAttributes.ContainsKey("user"))
            {
                lastEditor = osmBuildingOutline.InnerAttributes["user"];
            }
            if (osmBuildingOutline.InnerAttributes.ContainsKey("timestamp"))
            {
                lastEditDate = DateTime.Parse(osmBuildingOutline.InnerAttributes["timestamp"]);
            }
            if (lastEditor.ToLower().Contains("import"))
            {
                replace = true;
            }
            if (lastEditDate.Year < 2013)
            {
                replace = true;
            }

            // Don't replace if possibly lower resolution
            // (Would also need to possibly dispose of unused nodes if so
            if (osmBuildingOutline.NodeList.Count > newWay.NodeList.Count) return false;

            // Don't replace if entrances, etc are marked.
            if (ExtraNodeTags(osmBuildingOutline)) return false;

            if (!replace) return false;

            // Don't replace if old or new building has shared node
            foreach (var node in osmBuildingOutline.NodeList)
            {
                if (osmData.connectedWays.ContainsKey(node))
                {
                    if (osmData.connectedWays[node].Count > 1)
                    {
                        // Shared node; don't replace
                        return false; 
                    }
                }
            }
            foreach (var node in newWay.NodeList)
            {
                if (newBuildings.connectedWays.ContainsKey(node))
                {
                    if (newBuildings.connectedWays[node].Count > 1)
                    {
                        // Shared node; don't replace
                        return false;
                    }
                }
            }


            // Proceed with geometry replace
            for (int i=0; i < newWay.NodeList.Count; i++)
            {
                if (i == newWay.NodeList.Count-1)
                {
                    // Make last node = first node to close polygon (would have been an original but relocated node)
                    if (newWay.NodeList.Count < osmBuildingOutline.NodeList.Count)
                    {
                        // New building has fewer data points
                        osmBuildingOutline.NodeList[i] = osmBuildingOutline.NodeList[0];
                    }
                    else
                    {
                        osmBuildingOutline.NodeList.Add(osmBuildingOutline.NodeList[0]);
                    }
                } else if (i >= osmBuildingOutline.NodeList.Count)
                {
                    // Add new node to end.
                    osmBuildingOutline.NodeList.Add(newWay.NodeList[i]);

                    // Remove node from new dataset
                    var newId = newWay.NodeList[i].ID;
                    if (!osmData.osmNodes.ContainsKey(newId))
                    {
                        osmData.osmNodes.Add(newId, newWay.NodeList[i]);
                        if (newBuildings.osmNodes.ContainsKey(newId))
                        {
                            newBuildings.osmNodes.Remove(newId);
                        }
                    }
;               } else if (i == (osmBuildingOutline.NodeList.Count-1) )
                {
                    // Last node in list
                    // Replace last node in old dataset (it's repeated in list)
                    osmBuildingOutline.NodeList[i] = newWay.NodeList[i];

                    // Remove node from new dataset
                    var newId = newWay.NodeList[i].ID;
                    if (!osmData.osmNodes.ContainsKey(newId))
                    {
                        osmData.osmNodes.Add(newId, newWay.NodeList[i]);
                        if (newBuildings.osmNodes.ContainsKey(newId))
                        {
                            newBuildings.osmNodes.Remove(newId);
                        }
                    }
                }
                else
                {
                    // Edit existing node position
                    RelocateNode(osmBuildingOutline.NodeList[i], newWay.NodeList[i]);
                }
            }

            // Remove extra nodes from end of old building
            while (osmBuildingOutline.NodeList.Count > newWay.NodeList.Count)
            {
                osmBuildingOutline.NodeList.RemoveAt(osmBuildingOutline.NodeList.Count - 1);
            }

            MarkEdited(osmBuildingOutline);
            SpatialUtilities.SetBboxFor(osmBuildingOutline);  // Reset Bbox for new outline
            return true;
        }

        private bool ExtraNodeTags(OSMWay osmBuildingOutline)
        {
            var extraTags = false;

            foreach (var node in osmBuildingOutline.NodeList)
            {
                if (node.Tags.Count > 0) return true;
            }

            return extraTags;
        }

        /// <summary>
        /// Edit node coordinates to new position
        /// </summary>
        /// <param name="existingNode"></param>
        /// <param name="newNode"></param>
        private void RelocateNode(OSMNode existingNode, OSMNode newNode)
        {
            existingNode.InnerAttributes["lat"] = newNode.InnerAttributes["lat"];
            existingNode.Lat = newNode.Lat;
            existingNode.InnerAttributes["lon"] = newNode.InnerAttributes["lon"];
            existingNode.Lon = newNode.Lon;

            MarkEdited(existingNode);

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
                        if (!UnitMatch(newAddrNode, osmAddrObject)) continue;

                        if (newAddrNode.Tags.ContainsKey("addr:housenumber") &&
                            newAddrNode.Tags.ContainsKey("addr:street"))
                        {
                            if (newAddrNode.Tags["addr:housenumber"] == osmAddrObject.Tags["addr:housenumber"] &&
                                newAddrNode.Tags["addr:street"].Equals(osmAddrObject.Tags["addr:street"], StringComparison.CurrentCultureIgnoreCase) )
                            {
                                newAddresses.osmNodes.Remove(newAddrNode.ID);
                                addrRemoveList.Add(newAddrNode);
                                actionCount++;
                            }
                        }
                    }

                    // Also remove node from bucket
                    foreach (var removeNode in addrRemoveList)
                    {
                        indexBuckets[idx].Remove(removeNode);
                    }
                }
            }
        }

        /// <summary>
        /// Check for differing units
        /// </summary>
        /// <param name="newAddrNode"></param>
        /// <param name="osmAddrObject"></param>
        /// <returns></returns>
        private bool UnitMatch(OSMNode newAddrNode, BaseOSM osmAddrObject)
        {
            var newUnit = "";
            var oldUnit = "";
            if (newAddrNode.Tags.ContainsKey("addr:unit")) newUnit = newAddrNode.Tags["addr:unit"];
            if (osmAddrObject.Tags.ContainsKey("addr:unit")) oldUnit = osmAddrObject.Tags["addr:unit"];

            return newUnit == oldUnit;
        }




        /// <summary>
        /// Update OSM object attributes to show edited and trigger upload
        /// </summary>
        /// <param name="osmOject"></param>
        private void MarkEdited(BaseOSM osmOject)
        {
            if (!osmOject.InnerAttributes.ContainsKey("action"))
            {
                osmOject.InnerAttributes.Add("action", "modify");
            }
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

            var relatedWays = new List<OSMWay>();
            foreach (var building in newBuildings.osmRelations.Values)
            {
                (var x, var y) = buckets.ReturnBucket(building.Lat, building.Lon);
                AddToBucket(building, x, y);
                // Be sure member ways are in same bucket
                foreach (var way in building.OSMWays)
                {
                    AddToBucket(way, x, y);
                    relatedWays.Add(way);
                }
            }
            foreach (var building in newBuildings.osmWays.Values)
            {
                (var x, var y) = buckets.ReturnBucket(building.Lat, building.Lon);
                if (!relatedWays.Contains(building))
                {
                    AddToBucket(building, x, y);
                }
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
            if (!indexBuckets[idx].Contains(osmObject))
                indexBuckets[idx].Add(osmObject);
        }
    }
}
