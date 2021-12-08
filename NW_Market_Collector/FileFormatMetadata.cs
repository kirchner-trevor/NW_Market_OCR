using System;
using System.IO;

namespace NW_Market_Collector
{
    public static class FileFormatMetadata
    {
        public static FileMetadata GetMetadataFromFile(string path, ApplicationConfiguration configuration = null)
        {
            FileMetadata metadata = new FileMetadata();

            string fileNamePath = Path.GetFileNameWithoutExtension(path);
            string[] fileNameParts = fileNamePath.Split("_");

            if (long.TryParse(fileNameParts[^1] ?? "", out long fileNameTime))
            {
                metadata.CreationTime = DateTime.FromFileTimeUtc(fileNameTime);
            }
            else
            {
                metadata.CreationTime = File.GetCreationTimeUtc(path);
            }

            if (fileNameParts.Length >= 3)
            {
                metadata.ServerId = fileNameParts[^2];
            }
            else
            {
                metadata.ServerId = configuration?.Server;
            }

            return metadata;
        }
    }

    public class FileMetadata
    {
        public DateTime CreationTime { get; set; }
        public string ServerId { get; set; }
    }
}
