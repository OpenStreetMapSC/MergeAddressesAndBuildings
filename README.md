
# MergeAddressesAndBuildings
C# Dotnet Core code to merge address and building data for an OpenStreetMap Import

Tested with Windows 64 bit, but should be able to generate a 64-bit Linux executable

Tools: Visual Studio 2017

Input: .OSM file containing new point Address data (formatted and ready for upload) . 2 million max (because of starting ID numbering when creating new buildings)

Input: .geojson file containing new building footprints

Action - attach address nodes to new buildings and existing OSM buildings when possible.  Remaining
addresses are uploaded as nodes.

Interim Result: One large .OSM file 'newBuildings.osm' containing all new buildings, clipped to county boundary.   For information only.

Result: One large .OSM file ready for JOSM upload.   For information only - too large to validate and upload in one pass.

Result: OSM US Task manager GeoJSON task definition file

Result: Multiple task .OSM.GZ compressed files, ready for JOSM import with task.

![Block Diagram](https://raw.githubusercontent.com/OpenStreetMapSC/MergeAddressesAndBuildings/master/Doc/ProgramFlow.jpg)


Notes:
  Currently Fetch.cs is US-oriented regarding State / County administrative boundary hierarchy  - adapt as required.

MergeAddressesAndBuildings Usage:
  MergeAddressesAndBuildings /NewBuildings="filepath" /NewAddresses="filepath" /County="Name in OSM" /ResultFolder="Existing Directory" /State="2LetterStateAbbreviation" [ /TaskManagerSize=N ]

  For example:

  MergeAddressesAndBuildings  /NewBuildings="C:\users\me\OSM\SouthCarolina.geojson" /NewAddresses="C:\users\me\OSM\NewAddresses.osm" /County="Spartanburg County" /State="SC" /ResultFolder="C:\users\me\OSM\Merged" /TaskManagerSize=2000
(also accepts dash for parameter)

  MergeAddressesAndBuildings  -NewBuildings="C:\users\me\OSM\SouthCarolina.geojson" -NewAddresses="C:\users\me\OSM\NewAddresses.osm" -County="Spartanburg County" -State="SC" -ResultFolder="C:\users\me\OSM\Merged"

