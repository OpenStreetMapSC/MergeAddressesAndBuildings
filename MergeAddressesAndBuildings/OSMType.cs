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

        /// <summary>
        /// Calculate great circle distance to other object
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public double GreatCircleDistance(BaseOSM other)
        {
            var thisCoordinate = new Coordinate(Lat, Lon);
            var otherCoordinate = new Coordinate(other.Lat, other.Lon);
            return thisCoordinate.GreatCircleDistance(otherCoordinate);
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

        public double Area()
        {
            var corner1 = new Coordinate(MinLat, MinLon);
            var corner2 = new Coordinate(MinLat, MaxLon);
            var corner3 = new Coordinate(MaxLat, MinLon);

            var length = corner1.GreatCircleDistance(corner2);
            var width = corner1.GreatCircleDistance(corner3);

            return length * width;
        }
    }


    public class OSMNode : BaseOSM
    {
        public OSMNode() : base()
        {

        }
        public OSMNode(Int64 id, double lat, double lon) : base()
        {
            this.ID = id;
            this.Lat = lat;
            this.Lon = lon;
        }
        public Int64 NewID { get; set; }
        public bool IsUsed { get; set; }

        /// <summary>
        /// Test if coordinates are near, within tiny error
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool LocatedAt(BaseOSM other)
        {
            return (Math.Abs(this.Lat - other.Lat) < 1.0e-9) &&
                (Math.Abs(this.Lon - other.Lon) < 1.0e-9);
        }
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

        public OSMWay OuterWay { get; set; }  // Assume single major outer way for relations

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
