using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using GeoJSON.Net;
using Newtonsoft.Json;
using GeoJSON.Net.Geometry;
using GeoJSON.Net.Feature;

namespace MergeAddressesAndBuildings
{
    public class TaskingInterface
    {
        private const int Zoom = 14; // Approximately 2Km squares - not used otherwise

        public List<TaskingManagerTask> TaskingManagerTasks { get; set; }

        private OSMDataset[] osmDataSections;

        // For each node, the list of connected ways so that ways sharing nodes are properly handled
        private Dictionary<OSMNode, List<OSMWay>> connectedWays;


        /// <summary>
        /// Write task data file, as well as tasking manager GeoJSON definition file
        /// </summary>
        /// <param name="outputFilePath"></param>
        /// <param name="osmDataset"></param>
        /// <param name="taskBuckets"></param>
        public void WriteTasks(string outputFilePath, OSMDataset osmDataset, Buckets taskBuckets)
        {
            osmDataset.ResetUsedFlag();

            osmDataSections = new OSMDataset[taskBuckets.NHorizontal * taskBuckets.NVertical];

            InitializeConnectedWays(osmDataset);

            // Place data into indiividual sections
            foreach (var relation in osmDataset.osmRelations.Values)
            {
                (var x, var y) = taskBuckets.ReturnBucket(relation.Lat, relation.Lon);
                var osmDataSection = GetSectionData(taskBuckets, x, y);
                osmDataSection.osmRelations.Add(relation.ID, relation);

                // Store related data
                foreach (var way in relation.OSMWays)
                {
                    SaveWayTo(osmDataSection, way);
                }
            }

            // Assumption : no shared nodes between buildings
            foreach (var way in osmDataset.osmWays.Values)
            {
                if (!way.IsUsed)
                {
                    (var x, var y) = taskBuckets.ReturnBucket(way.Lat, way.Lon);
                    var osmDataSection = GetSectionData(taskBuckets, x, y);
                    SaveWayTo(osmDataSection, way);
                }
            }


            foreach (var node in osmDataset.osmNodes.Values)
            {
                if (!node.IsUsed)
                {
                    (var x, var y) = taskBuckets.ReturnBucket(node.Lat, node.Lon);
                    var osmDataSection = GetSectionData(taskBuckets, x, y);
                    osmDataSection.osmNodes.Add(node.ID, node);
                    node.IsUsed = true;
                }
            }

            WriteFileSections(taskBuckets, outputFilePath);

            WriteTaskGeoJSON(taskBuckets, outputFilePath);
        }

        private void InitializeConnectedWays(OSMDataset osmDataset)
        {
            // Set up empty list of connected ways to start
            connectedWays = new Dictionary<OSMNode, List<OSMWay>>();
            foreach ( var node in osmDataset.osmNodes.Values)
            {
                var wayList = new List<OSMWay>();
                connectedWays.Add(node, wayList);
            }

            foreach (var way in osmDataset.osmWays.Values)
            {
                foreach (var node in way.NodeList)
                {
                    var ways = connectedWays[node];
                    if (!ways.Contains(way))
                    {
                        ways.Add(way);
                    }
                }
            }
        }



        private void WriteTaskGeoJSON(Buckets taskBuckets, string outputFilePath)
        {
            FeatureCollection tasks = new FeatureCollection();

            for (int cell = 0; cell < osmDataSections.Length; cell++)
            {
                if (osmDataSections[cell] != null)
                {
                    (var x, var y) = GetXY(taskBuckets, cell);

                    tasks.Features.Add(CreateTask(taskBuckets, x, y));
                }
            }

            var jsonString = JsonConvert.SerializeObject(tasks);
            var outputFilename = Path.Combine(outputFilePath, "TaskGeoJSON.geojson");
            using (var outputFile = new StreamWriter(outputFilename))
            {
                outputFile.Write(jsonString);
            }

        }

        private Feature CreateTask(Buckets taskBuckets, int x, int y)
        {
            var bbox = taskBuckets.GetBBox(x, y);
            var properties = new Dictionary<string, object>();
            //properties.Add("taskX", x); // Ignored
            //properties.Add("taskY", y); // Ignored
            //properties.Add("taskZoom", Zoom); // Ignored
            //properties.Add("taskSplittable", false); // Ignored
            //properties.Add("taskStatus", "READY"); // Ignored
            properties.Add("import_url", $"Task-{x:000}-{y:000}-{Zoom:00}.osm");

            List<IPosition> segments = new List<IPosition>();
            segments.Add(new Position(bbox.MinLat, bbox.MinLon));
            segments.Add(new Position(bbox.MaxLat, bbox.MinLon));
            segments.Add(new Position(bbox.MaxLat, bbox.MaxLon));
            segments.Add(new Position(bbox.MinLat, bbox.MaxLon));
            segments.Add(new Position(bbox.MinLat, bbox.MinLon));

            var lineString = new LineString(segments);
            var lineStrings = new List<LineString>();
            lineStrings.Add(lineString);
            var polygon = new Polygon(lineStrings);
            var polygons = new List<Polygon>();
            polygons.Add(polygon);

            var geometry = new MultiPolygon(polygons);

            var feature = new Feature(geometry, properties);

            return feature;

        }




        private void WriteFileSections(Buckets taskBuckets, string outputFilePath)
        {
            for (int cell=0; cell < osmDataSections.Length; cell++)
            {
                if (osmDataSections[cell] != null)
                {
                    (var x, var y) = GetXY(taskBuckets, cell);
                    WriteFileSection(osmDataSections[cell], x, y, outputFilePath);
                }
            }
        }

        private void WriteFileSection(OSMDataset oSMDataset, int x, int y, string outputFilePath)
        {
            var taskFolder = Path.Combine(outputFilePath, "Tasks");
            if (!Directory.Exists(taskFolder)) Directory.CreateDirectory(taskFolder);

            var filename = $"Task-{x:000}-{y:000}-{Zoom:00}.osm";
            var filePath = Path.Combine(taskFolder, filename);
            var osmList = new List<OSMDataset>();
            osmList.Add(oSMDataset);
            WriteOSM.WriteDocument(filePath, osmList);
        }


        private void SaveWayTo(OSMDataset osmDataSection, OSMWay way)
        {
            if (!way.IsUsed)
            {
                osmDataSection.osmWays.Add(way.ID, way);
                way.IsUsed = true;
                foreach (var node in way.NodeList)
                {
                    if (!node.IsUsed)
                    {
                        osmDataSection.osmNodes.Add(node.ID, node);
                        node.IsUsed = true;

                        foreach (var connectedWay in connectedWays[node])
                        {
                            if (!connectedWay.IsUsed)
                            {
                                SaveWayTo(osmDataSection, connectedWay); // Recursive to include all ways sharing this node
                            }
                        }
                    }
                }

            }

        }


        /// <summary>
        /// Convert from cell back to X, Y of bucket
        /// </summary>
        /// <param name="cell"></param>
        /// <returns></returns>
        private (int x, int y) GetXY(Buckets taskBuckets, int cell)
        {
            int y = cell % taskBuckets.NVertical;
            int x = cell / taskBuckets.NVertical;

            return (x, y);
        }

        private OSMDataset GetSectionData(Buckets taskBuckets, int x, int y)
        {
            var cell = x * taskBuckets.NVertical + y;
            if (osmDataSections[cell] == null) osmDataSections[cell] = new OSMDataset();
            return osmDataSections[cell];
        }




        #region "Test to read GeoJSON of task definition - not used here "
               

        public void ReadTaskGeoJSON(string geoJsonFilePath)
        {

            string fileContents = "";
            using (var taskFile = new StreamReader(geoJsonFilePath))
            {
                fileContents = taskFile.ReadToEnd();
                taskFile.Close();
            }


            FeatureCollection tasks = JsonConvert.DeserializeObject<FeatureCollection>(fileContents);

            ReadTaskDefinitions(tasks);

        }

        private void ReadTaskDefinitions(FeatureCollection tasks)
        {

            TaskingManagerTasks = new List<TaskingManagerTask>();

            // Border tasks are an odd shape and will have their bounding box combined with the
            // nearest square task.
            List<Feature> borderTasks = new List<Feature>();

            foreach (Feature tmTaskFeature in tasks.Features)
            {
                if (tmTaskFeature.Properties["taskX"] == null)
                {
                    borderTasks.Add(tmTaskFeature);
                } else
                {
                    var tmTask = new TaskingManagerTask();
                    tmTask.X = Convert.ToInt32(tmTaskFeature.Properties["taskX"]);
                    tmTask.Y = Convert.ToInt32(tmTaskFeature.Properties["taskY"]);
                    tmTask.Z = Convert.ToInt32(tmTaskFeature.Properties["taskZoom"]);
                    tmTask.Bbox = FindBbox(tmTaskFeature.Geometry);
                    TaskingManagerTasks.Add(tmTask);
                }
            }

            AssignBorderTasksToTtask(borderTasks);
        }



        private void AssignBorderTasksToTtask(List<Feature> borderTasks)
        {

            var lastPassUnmatched = borderTasks;

            for (int tries = 0; tries < 10; tries++)
            {
                var thisPassUnmatched = new List<Feature>(); // May take 2 passes to assign all odd border shape

                foreach (var borderTask in lastPassUnmatched)
                {
                    TaskingManagerTask matchingCorner = null;
                    BBox borderBbox = null;
                    var match = CanAssign(borderTask, ref borderBbox, ref matchingCorner);
                    if (!match)
                    {
                        if (matchingCorner == null)
                        {
                            thisPassUnmatched.Add(borderTask);
                        }
                        else
                        {
                            // Extend task bbox by this border task bbox size
                            matchingCorner.Bbox = SpatialUtilities.BboxUnion(borderBbox, matchingCorner.Bbox);
                        }
                    }
                }
                lastPassUnmatched = thisPassUnmatched;
                if (lastPassUnmatched.Count == 0) break;
            }

        }


        private bool CanAssign(Feature borderTask, ref BBox borderBbox, ref TaskingManagerTask matchingCorner)
        {
            borderBbox = FindBbox(borderTask.Geometry);

            matchingCorner = null;  // For multiple odd shapes without line but has a matching corner

            var assigned = false;
            // Find square to attach via adjoining line
            foreach (var tmTask in TaskingManagerTasks)
            {
                // Left
                if (CloseTo(borderBbox.MaxLon, tmTask.Bbox.MinLon) && CloseTo(borderBbox.MinLat, tmTask.Bbox.MinLat)) assigned = true;
                // Right
                if (CloseTo(borderBbox.MinLon, tmTask.Bbox.MaxLon) && CloseTo(borderBbox.MinLat, tmTask.Bbox.MinLat)) assigned = true;
                // Top
                if (CloseTo(borderBbox.MinLat, tmTask.Bbox.MaxLat) && CloseTo(borderBbox.MinLon, tmTask.Bbox.MinLon)) assigned = true;
                // Bottom
                if (CloseTo(borderBbox.MaxLat, tmTask.Bbox.MinLat) && CloseTo(borderBbox.MinLon, tmTask.Bbox.MinLon)) assigned = true;

                if (assigned)
                {
                    // Expand that task bbox size
                    tmTask.Bbox = SpatialUtilities.BboxUnion(borderBbox, tmTask.Bbox);
                    break;
                }
                else
                {
                    if (HasMatchingEdge(borderBbox, tmTask.Bbox)) matchingCorner = tmTask;
                }
            }

            return assigned;
        }


        private bool HasMatchingEdge(BBox bbox1, BBox bbox2)
        {
            bool match = false;

            // bbox1       bbox2
            // Top Left-Bottom Left
            if (CloseTo(bbox1.MinLon, bbox2.MinLon) && CloseTo(bbox1.MaxLat, bbox2.MinLat)) match = true;
            // Top Right - Bottom Right
            if (CloseTo(bbox1.MaxLon, bbox2.MaxLon) && CloseTo(bbox1.MaxLat, bbox2.MinLat)) match = true;
            // Bottom Left - Top Left
            if (CloseTo(bbox1.MinLon, bbox2.MinLon) && CloseTo(bbox1.MinLat, bbox2.MaxLat)) match = true;
            // Bottom Right - Top Right
            if (CloseTo(bbox1.MaxLon, bbox2.MaxLon) && CloseTo(bbox1.MinLat, bbox2.MaxLat)) match = true;

            return match;
        }


        /// <summary>
        /// Compare 2 floating point values, to within Epsilon
        /// </summary>
        /// <param name="d1"></param>
        /// <param name="d2"></param>
        /// <returns></returns>
        private bool CloseTo(double d1, double d2)
        {
            var Epsilon = 1.0E-10;
            return (Math.Abs(d1 - d2) < Epsilon);
        }


        private BBox FindBbox(IGeometryObject geometry)
        {
            var bbox = new BBox();
            var multiPolygon = geometry as MultiPolygon;
            foreach (var mpCoordinate in multiPolygon.Coordinates)
            {
                foreach (var polyCoordinate in mpCoordinate.Coordinates)
                {
                    foreach (var coordinate in polyCoordinate.Coordinates)
                    {
                        if (coordinate.Latitude < bbox.MinLat) bbox.MinLat = coordinate.Latitude;
                        if (coordinate.Latitude > bbox.MaxLat) bbox.MaxLat = coordinate.Latitude;
                        if (coordinate.Longitude < bbox.MinLon) bbox.MinLon = coordinate.Longitude;
                        if (coordinate.Longitude > bbox.MaxLon) bbox.MaxLon = coordinate.Longitude;

                    }
                }

            }

            return bbox;
        }

#endregion

    }


    #region "Geojson task object, not used"
    public class TaskingManagerTask
    {
        public BBox Bbox { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }
    #endregion
}
