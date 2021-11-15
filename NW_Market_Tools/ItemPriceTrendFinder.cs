using Amazon.S3;
using Amazon.S3.Model;
using MW_Market_Model;
using NwdbInfoApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace NW_Market_Tools
{
    public class ItemPriceTrendFinder : IMarketTool
    {
        private const string ITEM_TREND_DATA_FILE_NAME = "itemTrendData.json";

        private readonly MarketDatabase marketDatabase;
        private readonly AmazonS3Client s3Client;
        private readonly string dataDirectory;

        private DateTime lastUpdateDate;

        public ItemPriceTrendFinder(MarketDatabase marketDatabase, AmazonS3Client s3Client, string dataDirectory)
        {
            this.marketDatabase = marketDatabase;
            this.s3Client = s3Client;
            this.dataDirectory = dataDirectory;
        }

        public async Task Run()
        {
            marketDatabase.LoadDatabaseFromDisk();

            if (marketDatabase.Updated > lastUpdateDate)
            {
                lastUpdateDate = marketDatabase.Updated;

                List<ItemStats> itemStats = new List<ItemStats>();
                foreach (IGrouping<string, MarketListing> listings in marketDatabase.Listings.GroupBy(_ => _.Name))
                {
                    ItemStats itemStat = new ItemStats();
                    itemStat.Name = listings.Key;

                    DateTime toDate = DateTime.UtcNow.Date;
                    DateTime fromDate = toDate.AddDays(-1);
                    while (fromDate >= DateTime.UtcNow.Date.AddDays(-7))
                    {
                        DailyItemStats dailyItemStats = CreateDailyStats(listings, toDate, fromDate);

                        itemStat.DailyStats.Add(dailyItemStats);

                        toDate = fromDate;
                        fromDate = toDate.AddDays(-1);
                    }

                    itemStats.Add(itemStat);
                }

                await SaveAndUploadItemStats(itemStats);
            }
        }

        private async Task SaveAndUploadItemStats(List<ItemStats> itemStats)
        {
            Console.WriteLine("Writing item data to disk...");
            string path = Path.Combine(dataDirectory, ITEM_TREND_DATA_FILE_NAME);
            string json = JsonSerializer.Serialize(new ItemTrendData
            {
                Items = itemStats,
                Updated = DateTime.UtcNow,
            });
            File.WriteAllText(path, json);
            Console.WriteLine("Item data written to disk!");

            Console.WriteLine("Uploading item data...");
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = "nwmarketdata",
                Key = ITEM_TREND_DATA_FILE_NAME,
                FilePath = path,
            });
            Console.WriteLine("Item data uploaded!");
        }

        private DailyItemStats CreateDailyStats(IGrouping<string, MarketListing> listings, DateTime toDate, DateTime fromDate)
        {
            // Get the listing during the time period closest to the "to date"
            var activeDailyListings = listings.Select(listing => new
            {
                Listing = listing,
                Instance = listing.Instances
                    // This instance of the listing was active during the time period
                    .Where(instance => instance.Time <= toDate)
                    .Where(instance => instance.Time + instance.TimeRemaining > toDate)
                    // This instance was the most recent during the time period
                    .OrderByDescending(instance => instance.Time)
                    .FirstOrDefault()
            })
            .Where(_ => _.Instance != null);

            DailyItemStats dailyItemStats = new DailyItemStats();
            dailyItemStats.FromDate = fromDate;
            dailyItemStats.ToDate = toDate;
            dailyItemStats.TotalAvailable = activeDailyListings.Sum(_ => _.Instance?.Available ?? 0);
            dailyItemStats.TotalMarket = activeDailyListings.Sum(_ => (_.Instance?.Available ?? 0) * _.Listing.Price);
            dailyItemStats.AveragePrice = dailyItemStats.TotalAvailable > 0 ? dailyItemStats.TotalMarket / dailyItemStats.TotalAvailable : 0;
            dailyItemStats.TotalAvailableBelowMarketAverage = activeDailyListings.Where(_ => _.Listing.Price <= dailyItemStats.AveragePrice).Sum(_ => _.Instance?.Available ?? 0);
            dailyItemStats.MinPrice = activeDailyListings.Any() ? activeDailyListings.Min(_ => _.Listing.Price) : 0f;
            dailyItemStats.MaxPrice = activeDailyListings.Any() ? activeDailyListings.Max(_ => _.Listing.Price) : 0f;
            return dailyItemStats;
        }

        private class ItemTrendData
        {
            public List<ItemStats> Items { get; set; } = new List<ItemStats>();
            public DateTime Updated { get; set; }
        }

        private class ItemStats
        {
            public string Name { get; set; }
            public List<DailyItemStats> DailyStats { get; set; } = new List<DailyItemStats>();
        }

        private class DailyItemStats
        {
            public DateTime FromDate { get; set; }
            public DateTime ToDate { get; set; }
            public int TotalAvailable { get; set; }
            public float TotalMarket { get; set; }
            public float AveragePrice { get; set; }
            public float MinPrice { get; set; }
            public float MaxPrice { get; set; }
            public float TotalAvailableBelowMarketAverage { get; set; }
        }
    }
}
