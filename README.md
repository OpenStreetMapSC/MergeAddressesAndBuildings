
# MergeAddressesAndBuildings
C# Dotnet Core code to merge address and building data for an OpenStreetMap Import

Tested with Windows 64 bit, but should be able to generate a 64-bit Linux executable

Tools: Visual Studio 2017

Input: .OSM file containing new point Address data (formatted and ready for upload)

Input: .OSM file containing new building footprints

Action - attach address nodes to new buildings and existing OSM buildings when possible.  Remaining
addresses are uploaded as nodes.

Result: One large .OSM file ready for JOSM upload.   For information only - too large to validate and upload in one pass.

Result: OSM US Task manager GeoJSON task definition file

Result: Multiple task .OSM files, ready for JOSM import with task.

![Block Diagram](https://raw.githubusercontent.com/OpenStreetMapSC/MergeAddressesAndBuildings/master/Doc/ProgramFlow.jpg)


Notes:
  Currently Fetch.cs is US-oriented regarding State / County administrative boundary hierarchy  - adapt as required.

MergeAddressesAndBuildings Usage:
  MergeAddressesAndBuildings /NewBuildings="filepath" /NewAddresses="filepath" /County="Name in OSM" /ResultFolder="Existing Directory" /State="2LetterStateAbbreviation"

  For example:

  MergeAddressesAndBuildings  /NewBuildings="C:\users\me\OSM\NewBuildings.osm" /NewAddresses="C:\users\me\OSM\NewAddresses.osm" /County="Spartanburg County" /State="SC" /ResultFolder="C:\users\me\OSM\Merged"
(also accepts dash for parameter)

  MergeAddressesAndBuildings  -NewBuildings="C:\users\me\OSM\NewBuildings.osm" -NewAddresses="C:\users\me\OSM\NewAddresses.osm" -County="Spartanburg County" -State="SC" -ResultFolder="C:\users\me\OSM\Merged"

