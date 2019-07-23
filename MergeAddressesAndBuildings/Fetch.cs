using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace MergeAddressesAndBuildings
{
    /// <summary>
    /// Fetch OpenStreetMap buildings inside bounding box.
    /// </summary>
    public class Fetch
    {
        // 34.576856628768994,-82.25753534822164,35.20379711377327,-81.71899862122568 spartanburg_buildings.osm

        // Overpass script to fetch:
        //   1. All nodes, ways, and relations containing addr* tags, excluding highways
        //   2. All existing buildings (including demolished:building) 
        public const string BuildingAddrOverpassScript =
            @"[out:xml][timeout:300];
            area(%AREARELATIONID%)->.county;
            // gather results
            (
                node(area.county)[~""addr.*""~"".*""];
                (way(area.county)[~""addr.*""~"".*""]; - way(area.county)[""highway""~"".*""];);
                relation(area.county)[~""addr.*""~"".*""];

                node(area.county)[~"".*building.*""~"".*""];
                way(area.county)[~"".*building.*""~"".*""];
                relation(area.county)[~"".*building.*""~"".*""];
            );
            (._;>;);
            out meta;";

        // NOTE: Area within area is not implemented
        //public const string BuildingAddrOverpassScript =
        //   @"[out:xml][timeout:300];
        //    area[""ISO3166-2""=""US-%STATEABBR%""][boundary=administrative]->.state;
        //    (
        //      area (area.state)[name=""%COUNTYNAME%""][admin_level=6][boundary=administrative]->.county;
        //        // gather results
        //        (
        //        // query part for: (wildcard key like) == * anything
        //          node(area.county)[~""addr.*""~"".*""];
        //          (way(area.county)[~""addr.*""~"".*""]; - way(area.county)[""highway""~"".*""];);
        //          relation(area.county)[~""addr.*""~"".*""];

        //          node(area.county)[~"".*building.*""~"".*""];
        //          way(area.county)[~"".*building.*""~"".*""];
        //          relation(area.county)[~"".*building.*""~"".*""];
        //        );
        //    );
        //    (._;>;);
        //    out meta;";


        public static void FetchOSMAddrsAndBuildingsToFile(Int64 countyOsmRelationID, string outputFilename)
        {
            var overpassID = countyOsmRelationID + 3600000000;
            var overpassScript = BuildingAddrOverpassScript.Replace("%AREARELATIONID%", overpassID.ToString());

            // Debug
            var outputDir = Path.GetDirectoryName(outputFilename);
            var overpassDebugFile = Path.Combine(outputDir, "OverpassBuildingQuery.txt");
            using (var overpassDebugStream = new StreamWriter(overpassDebugFile))
            {
                overpassDebugStream.WriteLine(overpassScript);
            }

            try
            {
                using (var binaryStream = new BinaryWriter(File.Open(outputFilename, FileMode.Create)))
                {
                    FetchToStream(overpassScript, binaryStream);
                }
            }
            catch (Exception)
            {
                if (File.Exists(outputFilename)) File.Delete(outputFilename);
                throw;
            }
        }



        // Overpass script to fetch US County outline
        public const string CountyOutlineOverpassScript =
            @"[out:xml][timeout:25];
            area[""ISO3166-2""=""US-%STATEABBR%""][boundary=administrative]->.state;
            (
              rel (area.state)[name=""%COUNTYNAME%""][admin_level=6][boundary=administrative];
  
            );
            (._;>;);
            out meta;";
        public static void FetchCountyOutlineToFile(string countyName, string stateAbbreviation, string outputFilename)
        {
            var overpassScript = CountyOutlineOverpassScript.Replace("%COUNTYNAME%", countyName);
            overpassScript = overpassScript.Replace("%STATEABBR%", stateAbbreviation);
            try
            {
                using (var binaryStream = new BinaryWriter(File.Open(outputFilename, FileMode.Create)))
                {
                    FetchToStream(overpassScript, binaryStream);
                }
            }
            catch (Exception)
            {
                if (File.Exists(outputFilename)) File.Delete(outputFilename);
                throw;
            }

        }




        private static void FetchToStream(string overpassScript, BinaryWriter outputStream)
        {
            HttpClient httpClient = new HttpClient();

            var requestURL = "https://overpass-api.de/api/interpreter";
            var postValues = new Dictionary<string, string>();
            postValues.Add("data", overpassScript);
            var formData = new FormUrlEncodedContent(postValues);

            bool isMoreToRead = true;
            var totalRead = 0L;
            var totalReads = 0L;
            var buffer = new byte[8192];

            httpClient.Timeout = new TimeSpan(hours: 0, minutes: 10, seconds: 0);
            using (var result = httpClient.PostAsync(requestURL, formData).Result)
            {
                using (Stream contentStream = result.Content.ReadAsStreamAsync().Result)
                {

                    do
                    {
                        var nRead = contentStream.ReadAsync(buffer, 0, buffer.Length).Result;
                        if (nRead == 0)
                        {
                            isMoreToRead = false;
                        }
                        else
                        {
                            outputStream.Write(buffer, 0, nRead);

                            totalRead += nRead;
                            totalReads += 1;

                            if (totalReads % 1000 == 0)
                            {
                                Console.WriteLine(string.Format("Overpass API total bytes downloaded so far: {0:n0}", totalRead));
                            }
                        }
                    }
                    while (isMoreToRead);


                }

                string content = result.Content.ReadAsStringAsync().Result;
                if (!result.IsSuccessStatusCode)
                {
                    throw new Exception($"HTTP Error {result.StatusCode} querying Overpass API for '{requestURL}' returned '{content}'");
                }
            }

        }


    }
}
