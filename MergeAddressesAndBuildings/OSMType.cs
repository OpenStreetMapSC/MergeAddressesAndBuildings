using System;
using System.Collections.Generic;
using System.Text;

namespace MergeAddressesAndBuildings
{

    public class BaseOSM
    {
        public BaseOSM()
        {
            InnerAttributes = new Dictionary<string, string>();
            Tags = new Dictionary<string, string>();
        }

        public Dictionary<string, string> InnerAttributes { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public Int64 ID { get; set; }
        public double Lat { get; set; } // Address of node or center of polygon
        public double Lon { get; set; } // Address of node or center of polygon
    }

    //public class OSMTags
    //{
    //    public OSMTags()
    //    {
    //        InnerAttributes = new Dictionary<string, string>();
    //        Tags = new Dictionary<string, string>();
    //    }

    //    public Dictionary<string, string> InnerAttributes { get; set; }
    //    public Dictionary<string, string> Tags { get; set; }

    //}

    public class BBox
    {
        public BBox()
        {
            MinLat = 999.0;
            MaxLat = -999.0;
            MinLon = 999.0;
            MaxLon = -999.0;

        }

        public double MinLat { get; set; }
        public double MaxLat { get; set; }
        public double MinLon { get; set; }
        public double MaxLon { get; set; }
    }

    public class OSMNode : BaseOSM
    {
        public OSMNode() : base()
        {

        }
        public Int64 NewID { get; set; }
        public bool IsUsed { get; set; }
    }



    public class OSMWay : BaseOSM
    {



        public OSMWay() : base()
        {
            Bbox = new BBox();
            NodeList = new List<OSMNode>();
        }

        public void SetCenter()
        {
            Lat = (Bbox.MaxLat + Bbox.MinLat) / 2.0;
            Lon = (Bbox.MaxLon + Bbox.MinLon) / 2.0;
        }

        public List<OSMNode> NodeList { get; set; }
        public BBox Bbox { get; set; }
        public bool IsUsed { get; set; }
    }



    public class OSMRelation : BaseOSM
    {
        public OSMRelation() : base()
        {
            OSMWays = new List<OSMWay>();
            Members = new List<RelationMember>();
        }

        public void SetCenter()
        {
            Lat = (Bbox.MaxLat + Bbox.MinLat) / 2.0;
            Lon = (Bbox.MaxLon + Bbox.MinLon) / 2.0;
        }


        public List<OSMWay> OSMWays { get; set; }
        public List<RelationMember> Members { get; set; }
        public BBox Bbox { get; set; }
    }


    public class RelationMember
    {
        public string MemberType { get; set; }
        public Int64 Ref { get; set; }
        public string Role { get; set; }
    }


}
