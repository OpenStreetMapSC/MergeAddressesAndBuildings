using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Compression;
using System.IO;

namespace MergeAddressesAndBuildings
{
    public class WriteGZ
    {

        /// <summary>
        /// Compress specified file with GZip compression, into file of extension .gz (any existing will be overwritten)
        /// </summary>
        /// <param name="filePath"></param>
        public static void Compress(string filePath)
        {
            var fileToCompress = new FileInfo(filePath);
            using (FileStream originalFileStream = fileToCompress.OpenRead())
            {
                var compressedFilename = fileToCompress.FullName + ".gz";
                using (FileStream compressedFileStream = File.Create(compressedFilename))
                {
                    using (GZipStream compressionStream = new GZipStream(compressedFileStream,
                       CompressionMode.Compress))
                    {
                        originalFileStream.CopyTo(compressionStream);

                    }
                }
                //FileInfo compressedInfo = new FileInfo(compressedFilename);
                //Console.WriteLine($"Compressed {fileToCompress.Name} from {fileToCompress.Length.ToString()} to {compressedInfo.Length.ToString()} bytes.");

            }
        }

    }

}
