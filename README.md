
# MergeAddressesAndBuildings
C# Dotnet Core code to merge address and building data for an OpenStreetMap Import

Tested with Windows 64 bit, but should be able to generate a 64-bit Linux executable

A 64-bit Windows executable is in the PublishedBinary folder

Tools to change the source code: Visual Studio 2019 or VS Code, Dotnet core 5.0

* Input: .OSM file containing new point Address data (formatted and ready for upload) . 2 million max (because of starting ID numbering when creating new buildings)

* Buildings Input: .geojson file containing new building footprints  OR .OSM file obtained from a  GIS Shape file

* Action - attach address nodes to new buildings and existing OSM buildings when possible.  Remaining
addresses are uploaded as nodes.

* Action - Remove true duplicate address nodes - those at same position with same address info.  NOTE: OGR2OSM merges all nodes at identical positions, regardless of tagging.   Many GIS sources will stack address points from different building levels. 
A special patch to OGR2OSM is required to remove the duplicate node merge and allow duplicate nodes into the result file.   This modification should normally only be applied to the address points data and not buildings.

* Action - Separate address nodes at same location by approximately 1 meter to allow OSM editors access to each node.

* Action - Apply ReplaceGeometry to existing buildings that don't share nodes with other buildings.   Reuses existing building nodes and outline when possible.   This is most useful when importing building footprints from a high quality
data source.  Currently this has a hard coded filter in the GeometryReplaced() method to apply only to buildings most likely to be improved.

* Interim Result: One large .OSM file 'newBuildings.osm' containing all new buildings, clipped to county boundary.   For information only.

* Result: One large .OSM file ready for JOSM upload.   For information only - too large to validate and upload in one pass.

* Result: OSM US Task manager GeoJSON task definition file

* Result: Multiple task .OSM.GZ compressed files, ready for JOSM import with task.

## Usage

Upload the compressed Task data files to a public web site.  Note the URL needed to access the data, then put *{import_filename}* at the end.

In the US Tasking manager - for the Per Task Instructions, it might read like

    "This task involves loading extra data. Click [here](http://localhost:8111/import?new_layer=true&url=http://greenvilleopenmap.info/SpartanburgData/{import_filename})

![Block Diagram](https://raw.githubusercontent.com/OpenStreetMapSC/MergeAddressesAndBuildings/master/Doc/ProgramFlow.jpg)


Notes:
  Currently Fetch.cs is US-oriented regarding State / County administrative boundary hierarchy  - adapt as required.

MergeAddressesAndBuildings Usage:
  MergeAddressesAndBuildings /NewBuildings="filepath" /NewAddresses="filepath" /County="Name in OSM" /ResultFolder="Existing Directory" /State="2LetterStateAbbreviation" [ /TaskManagerSize=N ]

  For example:

  MergeAddressesAndBuildings  /NewBuildings="C:\users\me\OSM\SouthCarolina.geojson" /NewAddresses="C:\users\me\OSM\NewAddresses.osm" /County="Spartanburg County" /State="SC" /ResultFolder="C:\users\me\OSM\Merged" /TaskManagerSize=2000

(also accepts dash for parameter)

  MergeAddressesAndBuildings  -NewBuildings="C:\users\me\OSM\SouthCarolina.geojson" -NewAddresses="C:\users\me\OSM\NewAddresses.osm" -County="Spartanburg County" -State="SC" -ResultFolder="C:\users\me\OSM\Merged"

  TaskMagerSize parameter is the size of each task manager square in meters.  Multiple adjacent squares are combined if they contain relatively few objects.   Adjust to obtain the optimum task size for your area.
