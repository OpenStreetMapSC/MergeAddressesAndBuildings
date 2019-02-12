using System;
using System.Collections.Generic;
using System.Text;

namespace MergeAddressesAndBuildings
{
    public class TrimOSMData
    {
        /// <summary>
        /// Given: 'new' OSM file containing just ways/relations - if changed by
        /// removing ways, remove unused now orphan nodes
        /// </summary>
        /// <param name="osmData"></param>
        public static void RemoveOrphanNodes(OSMDataset osmData)
        {
            // Find used nodes
            foreach (var way in osmData.osmWays.Values)
            {
                foreach (var node in way.NodeList)
                {
                    node.IsUsed = true;
                }
            }

            var removeList = new List<Int64>();
            // Remove unused nodes
            foreach (var node in osmData.osmNodes.Values)
            {
                if (!node.IsUsed) removeList.Add(node.ID);
            }

            foreach (var nodeID in removeList)
            {
                osmData.osmNodes.Remove(nodeID);
            }
        }


        /// <summary>
        /// Given: Downloaded OSM objects that may or may not have been edited - 
        /// remove unedited objects, while retaining nodes needed to support ways.
        /// </summary>
        /// <param name="osmData"></param>
        public static void RemoveUneditedData(OSMDataset osmData)
        {
            var relationRemoveList = new List<Int64>();
            foreach(var relation in osmData.osmRelations.Values)
            {
                if (WasEdited(relation))
                {
                    foreach (var way in relation.OSMWays)
                    {
                        way.IsUsed = true;
                    }
                }
                else
                {
                    relationRemoveList.Add(relation.ID);
                }
            }

            var wayRemoveList = new List<Int64>();
            foreach (var way in osmData.osmWays.Values)
            {
                if (way.IsUsed || WasEdited(way))
                {
                    foreach (var node in way.NodeList)
                    {
                        node.IsUsed = true;
                    }
                } else
                {
                    wayRemoveList.Add(way.ID);
                }
            }

            var nodeRemoveList = new List<Int64>();
            foreach (var node in osmData.osmNodes.Values)
            {
                if (!node.IsUsed && !WasEdited(node)) nodeRemoveList.Add(node.ID);
            }

            foreach (var relationID in relationRemoveList)
            {
                osmData.osmRelations.Remove(relationID);
            }

            foreach (var wayID in wayRemoveList)
            {
                osmData.osmWays.Remove(wayID);
            }

            foreach (var nodeID in nodeRemoveList)
            {
                osmData.osmNodes.Remove(nodeID);
            }
        }


        private static bool WasEdited(BaseOSM osmObject)
        {
            return osmObject.InnerAttributes.ContainsKey("action");
        }



    }
}
