using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MergeAddressesAndBuildings
{
    public class ReadMSBuildings
    {
        public OSMDataset ReadGeoJSON(OSMDataset boundary, string filepath)
        {
            var clipBoundary = new ClipBoundary(boundary.osmRelations);

            var osmDataset = new OSMDataset();

            var newWayIndex = -2000000;
            var newNodeIndex = -2000000;

            var serializer = new JsonSerializer();
            using (var inStream = new StreamReader(filepath))
            {
                using (JsonTextReader reader = new JsonTextReader(inStream))
                {
                    // Advance the reader to start of first array, 
                    // which should be the building definitions
                    while (reader.TokenType != JsonToken.StartArray)
                        reader.Read();


                    // Now process each building individually
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.StartObject)
                        {
                            var way = new OSMWay();
                            way.ID = newWayIndex--;
                            MSBuildingFeature msBuildingFeature = serializer.Deserialize<MSBuildingFeature>(reader);
                            foreach (var nodeList in msBuildingFeature.geometry.coordinates)
                            {
                                foreach (var coordinate in nodeList)
                                {
                                    var node = new OSMNode();
                                    node.ID = newNodeIndex--;
                                    node.Lon = coordinate[0];
                                    node.Lat = coordinate[1];
                                    node.InnerAttributes.Add("id", node.ID.ToString());
                                    node.InnerAttributes.Add("lat", node.Lat.ToString());
                                    node.InnerAttributes.Add("lon", node.Lon.ToString());
                                    node.InnerAttributes.Add("visible", "true");
                                    if (!DuplicateNode(node, way.NodeList))
                                    {
                                        way.NodeList.Add(node);
                                    } else
                                    {
                                        // Normally this is the closing node
                                        way.NodeList.Add(way.NodeList[0]);
                                    }
                                }
                            }
                            way.Tags.Add("building", "yes");
                            way.InnerAttributes.Add("id", way.ID.ToString());
                            way.InnerAttributes.Add("visible", "true");
                            SpatialUtilities.SetBboxFor(way);
                            way.SetCenter();

                            // For now, just pick a single point for boundary check - consider it inside boundary if first point is inside
                            if (clipBoundary.IsInBoundary(way.NodeList[0]))
                            {
                                osmDataset.osmWays.Add(way.ID, way);
                                foreach (var node in way.NodeList)
                                {
                                    if (!osmDataset.osmNodes.ContainsKey(node.ID))
                                        osmDataset.osmNodes.Add(node.ID, node);
                                }
                            }

                        }
                    }
                }
            }

            return osmDataset;
        }

        /// <summary>
        /// See if last node of a 'lineString' (same coordinate as first node)
        /// </summary>
        /// <param name="node"></param>
        /// <param name="nodeList"></param>
        /// <returns></returns>
        private bool DuplicateNode(OSMNode node, List<OSMNode> nodeList)
        {
            var isDuplicate = false;
            double Epsilon = 1.0E-10;

            if (nodeList.Count > 0)
            {
                var firstNode = nodeList[0];
                if ( (Math.Abs(node.Lat - firstNode.Lat) < Epsilon) &&
                     (Math.Abs(node.Lon - firstNode.Lon) < Epsilon) )
                {
                    return true;
                }
            }

            return isDuplicate;
        }

        public class MSBuildingGeometry
        {
            public string type { get; set; }
            public List<List<List<double>>> coordinates { get; set; }
        }

        public class MSBuildingProperties
        {
        }

        public class MSBuildingFeature
        {
            public string type { get; set; }
            public MSBuildingGeometry geometry { get; set; }
            public MSBuildingProperties properties { get; set; }
        }

    }
}
