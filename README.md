# MergeAddressesAndBuildings
C# Dotnet Core code to merge address and building data for an OpenStreetMap Import

Input: .OSM file containing new point Address data (formatted and ready for upload)
Input: .OSM file containing new building footprints

Action - attach address nodes to new buildings and existing OSM buildings when possible.  Remaining
addresses are uploaded as nodes.

Result: One large .OSM file ready for JOSM upload.   For information only - too large to validate and upload in one pass.
Result: OSM US Task manager GeoJSON task definition file
Result: Multiple task .OSM files, ready for JOSM import with task.

![Block Diagram](https://raw.githubusercontent.com/OpenStreetMapSC/MergeAddressesAndBuildings/master/Doc/ProgramFlow.jpg)



