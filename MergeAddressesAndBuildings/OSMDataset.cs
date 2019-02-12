using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;

namespace MergeAddressesAndBuildings
{
    /// <summary>
    /// Note: this currently accepts incomplete .OSM files (missing some referenced nodes) -
    /// therefore calculated bboxes could be incorrect
    /// </summary>
    public class OSMDataset
    {
        public Dictionary<Int64, OSMNode> osmNodes { get; set; }
        public Dictionary<Int64, OSMWay> osmWays { get; set; }
        public Dictionary<Int64, OSMRelation> osmRelations { get; set; }


        public OSMDataset()
        {
            osmNodes = new Dictionary<Int64, OSMNode>();
            osmWays = new Dictionary<Int64, OSMWay>();
            osmRelations = new Dictionary<Int64, OSMRelation>();
        }

        public void ReadOSMDocument(string inFile)
        {

            using (XmlReader reader = XmlReader.Create(inFile))
            {
                while (reader.Read())
                {
                    var xli = (IXmlLineInfo)reader;
                    switch (reader.NodeType)
                    {
                        case System.Xml.XmlNodeType.Element:
                            {
                                switch (reader.Name)
                                {
                                    case "node":
                                        HandleNodeRead(reader);
                                        break;

                                    case "way":
                                        HandleWayRead(reader);
                                        break;
                                    case "relation":
                                        HandlRelationRead(reader);
                                        break;
                                }

                            }
                            break;

                    }

                }
            }
        }

        /// <summary>
        /// Reset IsUsed flag on all linked OSM objects (ways and nodes)
        /// </summary>
        public void ResetUsedFlag()
        {
            foreach (var node in osmNodes.Values)
            {
                node.IsUsed = false;
            }
            foreach (var way in osmWays.Values)
            {
                way.IsUsed = false;
            }
        }


        //  <relation id = "1084743" version="2" timestamp="2015-05-30T03:46:23Z" changeset="31576221" uid="2311536" user="JordanKepler">
        //  <member type = "way" ref="69127471" role="outer"/>
        //  <member type = "way" ref="69127440" role="inner"/>
        //  <tag k = "building" v="yes"/>
        //  <tag k = "type" v="multipolygon"/>
        //</relation>


        private void HandlRelationRead(XmlReader reader)
        {

            var osmRelation = new OSMRelation();

            osmRelation.ID = Convert.ToInt64(reader.GetAttribute("id"));

            var doc = new System.Xml.XmlDocument();
            XmlNode node = doc.ReadNode(reader);

            foreach (XmlAttribute attr in node.Attributes)
            {
                osmRelation.InnerAttributes.Add(attr.Name, attr.Value);
            }

            if (node.HasChildNodes)
            {
                var members = node.SelectNodes("member");
                foreach (XmlNode member in members)
                {
                    var relationType = member.Attributes["type"].Value;
                    var id = Convert.ToInt64(member.Attributes["ref"].Value);
                    var role = member.Attributes["role"].Value;
                    var osmMember = new RelationMember();
                    osmMember.MemberType = relationType;
                    osmMember.Ref = id;
                    osmMember.Role = role;
                    osmRelation.Members.Add(osmMember);

                    if (relationType == "way")
                    {
                        if (osmWays.ContainsKey(id))
                        {
                            osmRelation.OSMWays.Add(osmWays[id]);
                        }
                    }

                }

                var tags = node.SelectNodes("tag");
                foreach (XmlNode tag in tags)
                {
                    var key = tag.Attributes["k"].Value;
                    var val = tag.Attributes["v"].Value;

                    osmRelation.Tags.Add(key, val);
                }



            }

            var bbox = new BBox();
            foreach (var way in osmRelation.OSMWays)
            {
                bbox = SpatialUtilities.BboxUnion(bbox, way.Bbox);
            }
            osmRelation.Bbox = bbox;
            osmRelation.SetCenter();
            osmRelations.Add(osmRelation.ID, osmRelation);
        }


        //<way id = "4526699" version="9" timestamp="2016-09-15T08:28:48Z" uid="1745400" user="bendenn" changeset="42167009">
        //  <nd ref="28099395"/>
        //  <nd ref="1583440533"/>
        //  <nd ref="299356652"/>
        //  <nd ref="28099387"/>
        //  <nd ref="1583440406"/>
        //  <nd ref="28099388"/>
        //  <nd ref="28099389"/>
        //  <nd ref="28099390"/>
        //  <tag k = "highway" v="primary_link"/>
        //  <tag k = "oneway" v="yes"/>
        //</way>

        private void HandleWayRead(XmlReader reader)
        {
            var osmWay = new OSMWay();

            osmWay.ID = Convert.ToInt64(reader.GetAttribute("id"));

            var doc = new System.Xml.XmlDocument();
            XmlNode node = doc.ReadNode(reader);

            foreach (XmlAttribute attr in node.Attributes)
            {
                osmWay.InnerAttributes.Add(attr.Name, attr.Value);
            }

            if (node.HasChildNodes)
            {
                var nodeRefs = node.SelectNodes("nd");
                foreach (XmlNode osmNode in nodeRefs)
                {
                    var id = Convert.ToInt64(osmNode.Attributes["ref"].Value);
                    if (osmNodes.ContainsKey(id))
                    {
                        osmWay.NodeList.Add(osmNodes[id]);
                    }
                }


                var tags = node.SelectNodes("tag");
                foreach (XmlNode tag in tags)
                {
                    var key = tag.Attributes["k"].Value;
                    var val = tag.Attributes["v"].Value;
                    osmWay.Tags.Add(key, val);
                }

            }
            SpatialUtilities.SetBboxFor(osmWay);
            osmWay.SetCenter();
            osmWays.Add(osmWay.ID, osmWay);

        }

        //   <node id="29568287" version="1" timestamp="2007-05-24T23:55:56Z" uid="7591" user="sadam" changeset="65544" lat="34.7661891" lon="-82.3672466">
        //      <tag k="created_by" v="YahooApplet 1.1"/>
        //   </node>
        private void HandleNodeRead(XmlReader reader)
        {
            var osmNode = new OSMNode();
            osmNode.ID = Convert.ToInt64(reader.GetAttribute("id"));
            osmNode.Lat = Convert.ToDouble(reader.GetAttribute("lat"));
            osmNode.Lon = Convert.ToDouble(reader.GetAttribute("lon"));

            osmNodes.Add(osmNode.ID, osmNode);

            var doc = new System.Xml.XmlDocument();
            XmlNode node = doc.ReadNode(reader);

            foreach (XmlAttribute attr in node.Attributes)
            {
                osmNode.InnerAttributes.Add(attr.Name, attr.Value);
            }

            if (node.HasChildNodes)
            {
                var tags = node.SelectNodes("tag");
                foreach (XmlNode tag in tags)
                {
                    var key = tag.Attributes["k"].Value;
                    var val = tag.Attributes["v"].Value;
                    osmNode.Tags.Add(key, val);
                }

            }
        }

        /// <summary>
        /// Combine into single dataset.   There is no check for duplicate
        /// IDs.
        /// </summary>
        /// <param name="osmDataList"></param>
        /// <returns></returns>
        public static OSMDataset CombineDatasets(List<OSMDataset> osmDataList)
        {
            var allNodes = new Dictionary<Int64, OSMNode>();
            var allWays = new Dictionary<Int64, OSMWay>();
            var allRelations = new Dictionary<Int64, OSMRelation>();

            foreach (var osmDataSet in osmDataList)
            {
                foreach (var node in osmDataSet.osmNodes.Values)
                {
                    if (!allNodes.TryAdd(node.ID, node))
                    {
                        throw new Exception($"Duplicate node id {node.ID} found across datasets.  Use ID parameter in OGR2OSM to assign different ranges");
                    }

                }
                foreach (var way in osmDataSet.osmWays.Values)
                {
                    if (!allWays.TryAdd(way.ID, way))
                    {
                        throw new Exception($"Duplicate way id {way.ID} found across datasets.  Use ID parameter in OGR2OSM to assign different ranges");
                    }

                }
                foreach (var relation in osmDataSet.osmRelations.Values)
                {
                    if (!allRelations.TryAdd(relation.ID, relation))
                    {
                        throw new Exception($"Duplicate relation id {relation.ID} found across datasets.  Use ID parameter in OGR2OSM to assign different ranges");
                    }

                }
            }


            var mergedOSM = new OSMDataset();
            mergedOSM.osmNodes = allNodes;
            mergedOSM.osmWays = allWays;
            mergedOSM.osmRelations = allRelations;

            return mergedOSM;
        }


    }
}
