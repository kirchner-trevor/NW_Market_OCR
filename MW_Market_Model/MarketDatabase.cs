using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MW_Market_Model
{
    [Serializable]
    public class MarketDatabase
    {
        private const string DATABASE_FILE_NAME = "database.json";
        private static readonly JsonSerializerOptions JSON_SERIALIZER_OPTIONS = new JsonSerializerOptions
        {
            Converters = { new TimeSpanJsonConverter(), }
        };

        private string DataDirectory;

        public MarketDatabase() : this(null) { }

        public MarketDatabase(string dataDirectory)
        {
            DataDirectory = dataDirectory;
        }

        public List<MarketListing> Listings { get; set; } = new List<MarketListing>();

        public DateTime Updated { get; set; }

        public MarketItemSummary GetItemSummary(string name, string location = default, DateTime onOrAfter = default, bool includeExpired = false)
        {
            List<IGrouping<string, MarketListing>> listingsForItem = Listings.Where(_ => _.Name == name)
                .Where(_ => location == default || _.Location == location)
                .Where(_ => onOrAfter == default || _.Latest.Time >= onOrAfter)
                .Where(_ => includeExpired || _.IsFresh())
                .GroupBy(_ => _.Location).ToList();

            MarketItemSummary summary = new MarketItemSummary();
            summary.Name = name;

            foreach (IGrouping<string, MarketListing> locationListings in listingsForItem)
            {
                List<IGrouping<DateTime, MarketListing>> timeListings = locationListings.GroupBy(_ => _.Latest.Time.Date).ToList();

                LocationPrices locationPrices = new LocationPrices();
                locationPrices.Location = locationListings.Key;

                foreach (IGrouping<DateTime, MarketListing> timeListing in timeListings)
                {
                    TimePrices timePrices = new TimePrices
                    {
                        Average = timeListing.Average(_ => _.Price),
                        Maximum = timeListing.Max(_ => _.Price),
                        Minimum = timeListing.Min(_ => _.Price),
                        EndTime = timeListing.Max(_ => _.Latest.Time),
                        StartTime = timeListing.Min(_ => _.Latest.Time),
                        TotalQuantity = timeListing.Sum(_ => _.Latest.Available),
                        TotalMarket = timeListing.Sum(_ => _.Latest.Available * _.Price),
                        Listings = timeListing.ToList(),
                    };

                    locationPrices.TimePrices.Add(timePrices);
                }

                summary.LocationPrices.Add(locationPrices);
            }

            return summary;
        }

        public void SaveDatabaseToDisk()
        {
            Updated = DateTime.UtcNow;
            Console.WriteLine("Saving database to disk...");
            string json = JsonSerializer.Serialize(this, JSON_SERIALIZER_OPTIONS);
            File.WriteAllText(GetDataBasePathOnDisk(), json);
        }

        public void LoadDatabaseFromDisk()
        {
            Console.WriteLine("Loading database from disk...");
            if (File.Exists(GetDataBasePathOnDisk()))
            {
                string json = File.ReadAllText(GetDataBasePathOnDisk());
                MarketDatabase loadedDatabase = JsonSerializer.Deserialize<MarketDatabase>(json, JSON_SERIALIZER_OPTIONS);
                Listings = loadedDatabase.Listings;
                Updated = loadedDatabase.Updated;
            }
        }

        public string GetDataBasePathOnDisk()
        {
            return Path.Combine(DataDirectory, DATABASE_FILE_NAME); ;
        }
    }

    public class MarketItemSummary
    {
        public string Name;
        public List<LocationPrices> LocationPrices = new List<LocationPrices>();
    }

    public class LocationPrices
    {
        public string Location;
        public List<TimePrices> TimePrices = new List<TimePrices>();
    }

    public class TimePrices
    {
        public DateTime StartTime;
        public DateTime EndTime;
        public float Minimum;
        public float Maximum;
        public float Average;
        public float TotalMarket;
        public int TotalQuantity;

        public List<MarketListing> Listings = new List<MarketListing>();
    }
}
