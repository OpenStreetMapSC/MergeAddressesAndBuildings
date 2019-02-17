﻿
using System;
using System.Collections.Generic;
using CommandLine.Utility;
using System.IO;


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


        static void Main(string[] args)
        {
            try
            {
                RunProgram(args);
            } catch (Exception ex)
            {
                Console.WriteLine("\n\n********\nProblem encountered: " + ex.ToString());
            }
            Console.Write("Press Enter");
            Console.ReadLine();

        }


        private static void RunProgram(string[] args)
        {
            // Command line parsing
            CommandLine = new Arguments(args);

            var newMSBuildingsFilePath = ValidateArgument("NewBuildings", true, true);
            var newAddressesFilePath = ValidateArgument("NewAddresses", true, true);


            var resultFolder = ValidateArgument("ResultFolder", true, false);
            if (!Directory.Exists(resultFolder))
            {
                Console.WriteLine($"Directory '{resultFolder}' from argument 'ResultFolder' not found.");
                ShowHelp();
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
            if (osmCountyBorder.osmNodes.Count == 0)
            {
                throw new Exception($"Got no data from Overpass query for {countyName} in file {countyFile} \n\n");
            }


            var newOsmBuildingsFilename = Path.Combine(resultFolder, "newBuildings.OSM");

            if (!File.Exists(newOsmBuildingsFilename))
            {
                Console.WriteLine("Reading Microsoft Building data...");
                var readMSBuildings = new ReadMSBuildings();
                var newMSBuildingData = readMSBuildings.ReadGeoJSON(osmCountyBorder, newMSBuildingsFilePath);
 
                // Save a copy for inspection or to save time when rerunning
                var writeList = new List<OSMDataset>();
                writeList.Add(newMSBuildingData);
                WriteOSM.WriteDocument(newOsmBuildingsFilename, writeList);
            }


            var buckets = new Buckets(osmCountyBorder.osmWays, 100 /* meters */);

            var currentOSMFile = Path.Combine(resultFolder, "CurrentAddrBuildings.osm");

            if (!File.Exists(currentOSMFile) || OutOfDate(currentOSMFile))
            {
                Console.WriteLine($"Downloading existing building and address data from {countyName}...");
                Fetch.FetchOSMAddrsAndBuildingsToFile(countyName, stateAbbreviation, currentOSMFile);
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
            var taskBuckets = new Buckets(osmCountyBorder.osmWays, 2000 /* meters */);
            taskingInterface.WriteTasks(resultFolder, mergedDataset, taskBuckets);

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
            Console.WriteLine(@"  MergeAddressesAndBuildings /NewBuildings=""filepath.geojson"" /NewAddresses=""filepath.osm"" /County=""Name in OSM"" /ResultFolder=""Existing Directory"" /State=""2LetterStateAbbreviation"" ");
            Console.WriteLine( "  For example: ");
            Console.WriteLine(@"  MergeAddressesAndBuildings  /NewBuildings=""C:\users\me\OSM\SouthCarolina.geojson"" /NewAddresses=""C:\users\me\OSM\NewAddresses.osm"" /County=""Spartanburg County"" /State=""SC"" /ResultFolder=""C:\users\me\OSM\Merged"" ");

        }

    }


}
