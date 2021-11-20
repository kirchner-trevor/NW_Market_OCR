using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MW_Market_Model
{
    public class ItemDatabase
    {
        private const string DATABASE_FILE_NAME = "itemTrendData.json";

        private string DataDirectory;
        private string Server = "default";

        public ItemDatabase() : this(null) { }

        public ItemDatabase(string dataDirectory)
        {
            DataDirectory = dataDirectory;
        }

        public ItemDatabaseContents Contents { get; set; }

        public void SaveDatabaseToDisk()
        {
            Contents.Updated = DateTime.UtcNow;
            Console.WriteLine($"Saving {DATABASE_FILE_NAME} to disk...");
            string json = JsonSerializer.Serialize(Contents);
            File.WriteAllText(GetDataBasePathOnDisk(), json);
        }

        public void LoadDatabaseFromDisk()
        {
            Console.WriteLine($"Loading {DATABASE_FILE_NAME} from disk...");
            Contents = null;
            if (File.Exists(GetDataBasePathOnDisk()))
            {
                string json = File.ReadAllText(GetDataBasePathOnDisk());
                Contents = JsonSerializer.Deserialize<ItemDatabaseContents>(json);
            }
        }

        public void SetServer(string server)
        {
            Server = server;
        }

        public string GetDataBasePathOnDisk()
        {
            return Path.Combine(DataDirectory, Server, DATABASE_FILE_NAME);
        }
    }

    public class ItemDatabaseContents
    {
        public List<ItemStats> Items { get; set; } = new List<ItemStats>();
        public DateTime Updated { get; set; }
    }

    public class ItemStats
    {
        public string Name { get; set; }
        public List<PeriodicItemStats> DailyStats { get; set; } = new List<PeriodicItemStats>();

        public PeriodicItemStats GetLatest()
        {
            return DailyStats.OrderByDescending(_ => _.ToDate).FirstOrDefault(_ => _.NumListings > 0);
        }
    }

    public class PeriodicItemStats
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int TotalAvailable { get; set; }
        public float TotalMarket { get; set; }
        public float AveragePrice { get; set; }
        public float MinPrice { get; set; }
        public float MaxPrice { get; set; }
        public int TotalAvailableBelowMarketAverage { get; set; }
        public int NumListings { get; set; }
    }
}
