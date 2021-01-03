
using System;
using System.Collections.Generic;
using CommandLine.Utility;
using System.IO;
using System.Linq;


namespace MergeAddressesAndBuildings
{


    /// <summary>
    /// Given:  
    ///    Set of new buildings from MS  (only building=yes, no height data)
    ///    Set of address points from GIS (OSM-compatible license or permission)
    ///    Existing OSM data containing buildings and adresses
    /// Merge all into upload data sections ready for the OSM Tasking manager.
    ///    Eliminate duplicate buildings from the new building dataset.  Include any new buildings in
    ///       changeset to be uploaded.
    ///    For buildings containing a single address point, merge address into building outline if building does not already
    ///        define the address, otherwise ignore new address point.  (Note: a different address already assigned to building
    ///        results in 2 addresses for building).
    ///    For buildings containing multiple address points, leave as multiple address points, do not merge.
    /// </summary>
    class Program
    {

        private static Arguments CommandLine;
        private static uint taskManagerSize = 2000; // Meters, default


        static void Main(string[] args)
        {
            try
            {
                RunProgram(args);

                if (CommandLine["Pause"] != null)
                {
                    Console.Write("Press Enter");
                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n\n********\nProblem encountered: " + ex.ToString());
            }

        }


        private static void RunProgram(string[] args)
        {
            // Command line parsing
            CommandLine = new Arguments(args);

            var newInputBuildingsFilePath = ValidateArgument("NewBuildings", true, true);
            var newAddressesFilePath = ValidateArgument("NewAddresses", true, true);

            var strTaskManagerSize = ValidateArgument("TaskManagerSize", false, false);
            if (strTaskManagerSize != null)
            {
                if (!UInt32.TryParse(strTaskManagerSize, out taskManagerSize))
                {
                    throw new Exception($"Expected positive number for TaskManagerSize, got {strTaskManagerSize}\n\n");
                }
                if (taskManagerSize < 10 || taskManagerSize > 50000)
                {
                    throw new Exception($"Unexpected size of {taskManagerSize} for argument TaskMangerSize.  A good number would be between 500 to 5000 to get the desired task data sizes.\n\n");
                }
            }

            var resultFolder = ValidateArgument("ResultFolder", true, false);
            if (!Directory.Exists(resultFolder))
            {
                Directory.CreateDirectory(resultFolder);
            }
            var countyName = ValidateArgument("County", true, false);
            var stateAbbreviation = ValidateArgument("State", true, false);
            if (stateAbbreviation.Length != 2)
            {
                throw new Exception($"Expected 2 letter US abbreviation for state instead of {stateAbbreviation}");
            }

            var countyFile = Path.Combine(resultFolder, countyName + ".osm");
            if (!File.Exists(countyFile))
            {
                Console.WriteLine($"Downloading boundary for {countyName}...");
                Fetch.FetchCountyOutlineToFile(countyName, stateAbbreviation, countyFile);
            }

            var osmCountyBorder = new OSMDataset();
            osmCountyBorder.ReadOSMDocument(countyFile);
            Int64 osmCountyRelationID = GetCountyOsmID(osmCountyBorder);
            if (osmCountyBorder.osmNodes.Count == 0)
            {
                throw new Exception($"Got no data from Overpass query for {countyName} in file {countyFile} \n\n");
            }


            var newOsmBuildingsFilename = Path.Combine(resultFolder, "newBuildings.OSM");

            if (!File.Exists(newOsmBuildingsFilename))
            {
                if (newInputBuildingsFilePath.EndsWith("JSON", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Reading Microsoft Building data...");
                    var readMSBuildings = new ReadMSBuildings();
                    var newMSBuildingData = readMSBuildings.ReadGeoJSON(osmCountyBorder, newInputBuildingsFilePath);

                    // Save a copy for inspection or to save time when rerunning
                    var writeList = new List<OSMDataset>();
                    writeList.Add(newMSBuildingData);
                    WriteOSM.WriteDocument(newOsmBuildingsFilename, writeList);
                } else
                {
                    // Assume new file is .OSM file and it will be read directly
                    newOsmBuildingsFilename = newInputBuildingsFilePath;
                }
            }


            var currentOSMFile = Path.Combine(resultFolder, "CurrentAddrBuildings.osm");

            if (!File.Exists(currentOSMFile) || OutOfDate(currentOSMFile))
            {
                Console.WriteLine($"Downloading existing building and address data from {countyName}...");
                Fetch.FetchOSMAddrsAndBuildingsToFile(osmCountyRelationID, currentOSMFile);
            }
            Console.WriteLine("Reading downloaded OSM data...");
            var osmExistingData = new OSMDataset();
            osmExistingData.ReadOSMDocument(currentOSMFile);
            if (osmExistingData.osmNodes.Count == 0)
            {
                throw new Exception($"Got no data from Overpass query for {countyName} in file {currentOSMFile} \n\n");
            }

            Console.WriteLine($"Existing data: {osmExistingData.osmNodes.Count:N0} nodes, {osmExistingData.osmWays.Count:N0} ways, {osmExistingData.osmRelations.Count:N0} relations");

            Console.Write("Reading new buildings...");
            var newBuildings = new OSMDataset();
            newBuildings.ReadOSMDocument(newOsmBuildingsFilename);
            Console.WriteLine($"Found {newBuildings.osmWays.Count:N0} new buildings");
            if (newBuildings.osmNodes.Count == 0)
            {
                throw new Exception($"Got no data from new buildings file {currentOSMFile} \n\n");
            }

            Console.Write("Reading new addresses...");
            var newAddresses = new OSMDataset();
            newAddresses.ReadOSMDocument(newAddressesFilePath);
            Console.WriteLine($"Found {newAddresses.osmNodes.Count:N0} new addresses");

            var dataBbox = newAddresses.OuterBbox();
            dataBbox = SpatialUtilities.BboxUnion(dataBbox, newBuildings.OuterBbox());

            // Special for greenville city:
            //dataBbox = SpatialUtilities.BboxUnion(dataBbox, osmExistingData.OuterBbox());
            dataBbox = SpatialUtilities.BboxUnion(dataBbox, osmCountyBorder.OuterBbox());

            var duplicateAddresses = new DuplicateAddress(newAddresses);
            var removeAddrCount = duplicateAddresses.RemoveDuplicateAddresses();
            Console.WriteLine($"Removed {removeAddrCount:N0} duplicate addresses");

            var buckets = new Buckets(osmCountyBorder.osmWays, dataBbox, 100 /* meters */);

            Console.WriteLine("Merging data...");
            var merge = new Merge(buckets, newAddresses, newBuildings, osmExistingData);
            merge.PerformMerge();

            TrimOSMData.RemoveOrphanNodes(newBuildings);
            TrimOSMData.RemoveUneditedData(osmExistingData);



            Console.WriteLine("Saving merged data...");
            var mergedFile = Path.Combine(resultFolder, "MergedOSMData.osm");
            var osmDataList = new List<OSMDataset>();
            osmDataList.Add(newAddresses);
            osmDataList.Add(newBuildings);
            osmDataList.Add(osmExistingData);

            WriteOSM.WriteDocument(mergedFile, osmDataList);

            Console.WriteLine("Combining and dividing data into task squares...");
            var mergedDataset = OSMDataset.CombineDatasets(osmDataList);

            var taskingInterface = new TaskingInterface();
            var taskBuckets = new Buckets(osmCountyBorder.osmWays, dataBbox, taskManagerSize /* meters */);
            taskingInterface.WriteTasks(resultFolder, mergedDataset, taskBuckets);

        }

        private static Int64 GetCountyOsmID(OSMDataset osmCountyBorder)
        {
            // Assume county relation ID is first and only relation
            if (osmCountyBorder.osmRelations.Count != 1)
            {
                throw new Exception($"Expected 1 relation for county border, found {osmCountyBorder.osmRelations.Count} relations");
            }
            return osmCountyBorder.osmRelations.Values.First().ID;
        }

        /// <summary>
        /// Don't re-fetch overpass data on every time - wait interval before re-fetching
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static bool OutOfDate(string filePath)
        {
            double MaxDaysOld = 5;

            var fileInfo = new FileInfo(filePath);
            if ((DateTime.Now - fileInfo.LastWriteTime).TotalDays > MaxDaysOld)
            {
                return true;
            }
            return false;
        }

        private static BBox FindOverallBbox(Dictionary<long, OSMWay> osmWays)
        {
            BBox bbox = new BBox();
            foreach (var way in osmWays.Values)
            {
                bbox = SpatialUtilities.BboxUnion(way.Bbox, bbox);
            }
            return bbox;
        }

        private static string ValidateArgument(string name, bool required, bool isFile)
        {
            var strArgument = CommandLine[name];
            if (strArgument == null && required)
            {
                ShowHelp();
                throw new Exception($"Required Argument {name} is missing.");
            }

            if (strArgument != null && isFile)
            {
                if (!File.Exists(strArgument))
                {
                    ShowHelp();
                    throw new Exception($"File '{strArgument}' from argument {name} not found.");
                }
            }

            return strArgument;
        }


        private static void ShowHelp()
        {

            Console.WriteLine();
            Console.WriteLine( "MergeAddressesAndBuildings Usage:");
            Console.WriteLine(@"  MergeAddressesAndBuildings /NewBuildings=""filepath.geojson"" /NewAddresses=""filepath.osm"" /County=""Name in OSM"" /ResultFolder=""Existing Directory"" /State=""2LetterStateAbbreviation"" [/TaskManagerSize=N] ");
            Console.WriteLine("  TaskManagerSize is in Meters, defaults to 2000 unless specified ");
            Console.WriteLine( "  For example: ");
            Console.WriteLine(@"  MergeAddressesAndBuildings  /NewBuildings=""C:\users\me\OSM\SouthCarolina.geojson"" /NewAddresses=""C:\users\me\OSM\NewAddresses.osm"" /County=""Spartanburg County"" /State=""SC"" /ResultFolder=""C:\users\me\OSM\Merged"" ");

        }

    }


}
