using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MW_Market_Model
{
    [Serializable]
    public class MarketDatabase
    {
        private const string DATABASE_FILE_NAME = "database.json";

        private string DataDirectory;

        public MarketDatabase() : this(null) { }

        public MarketDatabase(string dataDirectory)
        {
            DataDirectory = dataDirectory;
        }

        public List<MarketListing> Listings { get; set; } = new List<MarketListing>();

        public MarketItemSummary GetItemSummary(string name, string location = default, DateTime onOrAfter = default)
        {
            List<IGrouping<string, MarketListing>> listingsForItem = Listings.Where(_ => _.Name == name)
                .Where(_ => location == default || _.Location == location)
                .Where(_ => onOrAfter == default || _.Time >= onOrAfter)
                .GroupBy(_ => _.Location).ToList();

            MarketItemSummary summary = new MarketItemSummary();
            summary.Name = name;

            foreach (IGrouping<string, MarketListing> locationListings in listingsForItem)
            {
                List<IGrouping<DateTime, MarketListing>> timeListings = locationListings.GroupBy(_ => _.Time.Date).ToList();

                LocationPrices locationPrices = new LocationPrices();
                locationPrices.Location = locationListings.Key;

                foreach (IGrouping<DateTime, MarketListing> timeListing in timeListings)
                {
                    TimePrices timePrices = new TimePrices
                    {
                        Average = timeListing.Average(_ => _.Price),
                        Maximum = timeListing.Max(_ => _.Price),
                        Minimum = timeListing.Min(_ => _.Price),
                        EndTime = timeListing.Max(_ => _.Time),
                        StartTime = timeListing.Min(_ => _.Time),
                        TotalQuantity = timeListing.Sum(_ => _.Available),
                        TotalMarket = timeListing.Sum(_ => _.Available * _.Price),
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
            Console.WriteLine("Saving database to disk...");
            string json = JsonSerializer.Serialize(this);
            string databasePath = Path.Combine(DataDirectory, DATABASE_FILE_NAME);
            File.WriteAllText(databasePath, json);
        }

        public void LoadDatabaseFromDisk()
        {
            Console.WriteLine("Loading database from disk...");
            string databasePath = Path.Combine(DataDirectory, DATABASE_FILE_NAME);
            if (File.Exists(databasePath))
            {
                string json = File.ReadAllText(databasePath);
                MarketDatabase loadedDatabase = JsonSerializer.Deserialize<MarketDatabase>(json);
                this.Listings = loadedDatabase.Listings;
            }
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
