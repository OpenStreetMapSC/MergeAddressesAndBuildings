using Microsoft.VisualStudio.TestTools.UnitTesting;
using MergeAddressesAndBuildings;

namespace UnitTests
{
    [TestClass]
    public class TestClipBoundary
    {
        [TestMethod]
        public void TestPointInPolygon()
        {
            // For OSM file:
            //<osm version="0.6" generator="MergeAddressesAndBuildings 1.0.0">
            //  <node id="5494394157" lat="34.817149338" lon="-82.395971866" version="1" timestamp="2018-03-21T18:03:21Z" changeset="57394735" uid="7803438" user="Greenville_SC_City_MSImport2" action="modify" />
            //  <node id="5494394156" lat="34.817172161" lon="-82.395940894" version="1" timestamp="2018-03-21T18:03:21Z" changeset="57394735" uid="7803438" user="Greenville_SC_City_MSImport2" action="modify" />
            //  <node id="5494394155" lat="34.817182649" lon="-82.395952257" version="1" timestamp="2018-03-21T18:03:21Z" changeset="57394735" uid="7803438" user="Greenville_SC_City_MSImport2" action="modify" />
            //  <node id="5494394154" lat="34.817224386" lon="-82.395895622" version="1" timestamp="2018-03-21T18:03:21Z" changeset="57394735" uid="7803438" user="Greenville_SC_City_MSImport2" action="modify" />
            //  <node id="-20206811" lat="34.817216279" lon="-82.395886838" visible="true" />
            //  <node id="-20206812" lat="34.817239728" lon="-82.395855019" visible="true" />
            //  <node id="-20206813" lat="34.817207421" lon="-82.395820014" visible="true" />
            //  <node id="-20206814" lat="34.817218187" lon="-82.395805403" visible="true" />
            //  <node id="-20206815" lat="34.817194331" lon="-82.395779555" visible="true" />
            //  <node id="-20206816" lat="34.817163758" lon="-82.395821042" visible="true" />
            //  <node id="-20206817" lat="34.817146943" lon="-82.395802822" visible="true" />
            //  <node id="-20206818" lat="34.817112753" lon="-82.395849217" visible="true" />
            //  <node id="-20206819" lat="34.817124608" lon="-82.395862062" visible="true" />
            //  <node id="-20206820" lat="34.817090595" lon="-82.395908217" visible="true" />
            //  <node id="-10013548" lat="34.817178598" lon="-82.395867437" visible="true">
            //    <tag k="addr:housenumber" v="400" />
            //    <tag k="addr:postcode" v="29605" />
            //    <tag k="addr:city" v="Greenville" />
            //    <tag k="addr:state" v="SC" />
            //    <tag k="addr:street" v="Waccamaw Avenue" />
            //  </node>
            //  <way id="571771656" version="1" timestamp="2018-03-21T18:06:44Z" changeset="57394735" uid="7803438" user="Greenville_SC_City_MSImport2" action="modify">
            //    <nd ref="5494394157" />
            //    <nd ref="5494394156" />
            //    <nd ref="5494394155" />
            //    <nd ref="5494394154" />
            //    <nd ref="-20206811" />
            //    <nd ref="-20206812" />
            //    <nd ref="-20206813" />
            //    <nd ref="-20206814" />
            //    <nd ref="-20206815" />
            //    <nd ref="-20206816" />
            //    <nd ref="-20206817" />
            //    <nd ref="-20206818" />
            //    <nd ref="-20206819" />
            //    <nd ref="-20206820" />
            //    <nd ref="5494394157" />
            //    <tag k="building" v="yes" />
            //    <tag k="height" v="13.1" />
            //  </way>
            //</osm>

            var building = new OSMWay();
            building.NodeList.Add(new OSMNode(5494394157, 34.817149338, -82.395971866));
            building.NodeList.Add(new OSMNode(5494394156, 34.817172161, -82.395940894));
            building.NodeList.Add(new OSMNode(5494394155, 34.817182649, -82.395952257));
            building.NodeList.Add(new OSMNode(5494394154, 34.817224386, -82.395895622));
            building.NodeList.Add(new OSMNode(-20206811, 34.817216279, -82.395886838));
            building.NodeList.Add(new OSMNode(-20206812, 34.817239728, -82.395855019));
            building.NodeList.Add(new OSMNode(-20206813, 34.817207421, -82.395820014));
            building.NodeList.Add(new OSMNode(-20206814, 34.817218187, -82.395805403));
            building.NodeList.Add(new OSMNode(-20206815, 34.817194331, -82.395779555));
            building.NodeList.Add(new OSMNode(-20206816, 34.817163758, -82.395821042));
            building.NodeList.Add(new OSMNode(-20206817, 34.817146943, -82.395802822));
            building.NodeList.Add(new OSMNode(-20206818, 34.817112753, -82.395849217));
            building.NodeList.Add(new OSMNode(-20206819, 34.817124608, -82.395862062));
            building.NodeList.Add(new OSMNode(-20206820, 34.817090595, -82.395908217));
            building.NodeList.Add(building.NodeList[0]); // Close polygon

            var addrNodeInside = new OSMNode(-10013548, 34.817178598, -82.395867437);
            var addrNodeOutside = new OSMNode(-10013549, 34.8172953, -82.3957537);

            var clipBoundary = new ClipBoundary(building);
            Assert.IsTrue(clipBoundary.IsInBoundary(addrNodeInside));
            Assert.IsFalse(clipBoundary.IsInBoundary(addrNodeOutside));

            // Not comprehensive, - this is just the test case that failed, such as IsInBoundary0()
            // ** Algorithm fails Assert.IsTrue(clipBoundary.IsInBoundary0(addrNodeInside));
            // ** Algorighm fails Assert.IsTrue(clipBoundary.IsInBoundary1(addrNodeInside));
        }
    }
}
