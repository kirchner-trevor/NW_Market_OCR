using System;
using System.IO;

namespace NW_Market_Collector
{
    public static class FileFormatMetadata
    {
        public static DateTime GetSourceDateFromFile(string path)
        {
            string fileNamePath = Path.GetFileNameWithoutExtension(path);
            DateTime fileCreationTime;
            if (long.TryParse(fileNamePath.Split("_")?[1] ?? "", out long fileNameTime))
            {
                fileCreationTime = DateTime.FromFileTimeUtc(fileNameTime);
            }
            else
            {
                fileCreationTime = File.GetCreationTimeUtc(path);
            }

            return fileCreationTime;
        }
    }
}
