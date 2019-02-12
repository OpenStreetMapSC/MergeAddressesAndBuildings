using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;

namespace MergeAddressesAndBuildings
{
    public class WriteOSM
    {
        public static void WriteDocument(string filename, 
            List<OSMDataset> osmDataList)
        {
            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "  "; // note: default is two spaces
            settings.NewLineOnAttributes = false;
            settings.OmitXmlDeclaration = true;

            using (XmlWriter xmlWriter = XmlWriter.Create(filename, settings))
            {

                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement("osm");
                xmlWriter.WriteAttributeString("version", "0.6");
                xmlWriter.WriteAttributeString("generator", "MergeAddressesAndBuildings 1.0.0");

                foreach (var osmData in osmDataList)
                {
                    WriteNodes(xmlWriter, osmData.osmNodes);
                    WriteWays(xmlWriter, osmData.osmWays);
                    WriteRelations(xmlWriter, osmData.osmRelations);
                }

                xmlWriter.WriteEndDocument();
                xmlWriter.Close();
            }
        }

        private static void WriteRelations(XmlWriter xmlWriter, Dictionary<long, OSMRelation> osmRelations)
        {
            foreach (var relation in osmRelations.Values)
            {
                xmlWriter.WriteStartElement("relation");
                foreach (var attr in relation.InnerAttributes)
                {
                    xmlWriter.WriteAttributeString(attr.Key, attr.Value);
                }

                foreach (var member in relation.Members)
                {
                    xmlWriter.WriteStartElement("member");
                    xmlWriter.WriteAttributeString("type", member.MemberType);
                    xmlWriter.WriteAttributeString("ref", member.Ref.ToString());
                    xmlWriter.WriteAttributeString("role", member.Role);
                    xmlWriter.WriteEndElement();

                }

                foreach (var tag in relation.Tags)
                {
                    xmlWriter.WriteStartElement("tag");
                    xmlWriter.WriteAttributeString("k", tag.Key);
                    xmlWriter.WriteAttributeString("v", tag.Value);
                    xmlWriter.WriteEndElement();
                }
                xmlWriter.WriteEndElement();
            }
        }

        private static void WriteWays(XmlWriter xmlWriter, Dictionary<long, OSMWay> osmWays)
        {
            foreach (var way in osmWays.Values)
            {
                xmlWriter.WriteStartElement("way");
                foreach (var attr in way.InnerAttributes)
                {
                    xmlWriter.WriteAttributeString(attr.Key, attr.Value);
                }

                foreach (var node in way.NodeList)
                {
                    xmlWriter.WriteStartElement("nd");
                    xmlWriter.WriteAttributeString("ref", node.ID.ToString());
                    xmlWriter.WriteEndElement();

                }

                foreach (var tag in way.Tags)
                {
                    xmlWriter.WriteStartElement("tag");
                    xmlWriter.WriteAttributeString("k", tag.Key);
                    xmlWriter.WriteAttributeString("v", tag.Value);
                    xmlWriter.WriteEndElement();
                }
                xmlWriter.WriteEndElement();
            }
        }

        private static void WriteNodes(XmlWriter xmlWriter, Dictionary<long, OSMNode> osmNodes)
        {
            foreach(var node in osmNodes.Values)
            {
                xmlWriter.WriteStartElement("node");
                foreach (var attr in node.InnerAttributes)
                {
                    xmlWriter.WriteAttributeString(attr.Key, attr.Value);
                }
                foreach (var tag in node.Tags)
                {
                    xmlWriter.WriteStartElement("tag");
                    xmlWriter.WriteAttributeString("k", tag.Key);
                    xmlWriter.WriteAttributeString("v", tag.Value);
                    xmlWriter.WriteEndElement();
                }
                xmlWriter.WriteEndElement();
            }
        }
    }
}
