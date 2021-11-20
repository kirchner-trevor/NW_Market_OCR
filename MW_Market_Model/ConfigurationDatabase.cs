using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MW_Market_Model
{
    public class ConfigurationDatabase
    {
        private const string DATABASE_FILE_NAME = "configurationData.json";
        private string DataDirectory;

        public ConfigurationDatabase() : this(Directory.GetCurrentDirectory())
        {
        }

        public ConfigurationDatabase(string dataDirectory)
        {
            DataDirectory = dataDirectory;

            LoadDatabaseFromDisk();
        }

        public ConfigurationContent Content { get; set; }

        private void LoadDatabaseFromDisk()
        {
            Console.WriteLine($"Loading {DATABASE_FILE_NAME} from disk...");
            if (File.Exists(GetDataBasePathOnDisk()))
            {
                string json = File.ReadAllText(GetDataBasePathOnDisk());
                Content = JsonSerializer.Deserialize<ConfigurationContent>(json);
            }
        }

        public void SaveDatabaseToDisk()
        {
            Content.Updated = DateTime.UtcNow;
            Console.WriteLine($"Saving {DATABASE_FILE_NAME} to disk...");
            string json = JsonSerializer.Serialize(Content);
            File.WriteAllText(GetDataBasePathOnDisk(), json);
        }

        public string GetDataBasePathOnDisk()
        {
            return Path.Combine(DataDirectory, DATABASE_FILE_NAME);
        }
        
        public string GetDataBasePathOnServer()
        {
            return DATABASE_FILE_NAME;
        }
    }

    public class ConfigurationContent
    {
        public List<ServerListInfo> ServerList { get; set; }
        public DateTime Updated { get; set; }
    }

    public class ServerListInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string WorldSet { get; set; }
        public string Region { get; set; }
        public int? Listings { get; set; }
    }
}
