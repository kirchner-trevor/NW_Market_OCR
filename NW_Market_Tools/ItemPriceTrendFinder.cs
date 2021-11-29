using Amazon.S3;
using Amazon.S3.Model;
using NW_Market_Model;
using NwdbInfoApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly ItemDatabase itemDatabase;

        private DateTime lastUpdateDate;

        public ItemPriceTrendFinder(MarketDatabase marketDatabase, AmazonS3Client s3Client, ItemDatabase itemDatabase)
        {
            this.marketDatabase = marketDatabase;
            this.s3Client = s3Client;
            this.itemDatabase = itemDatabase;
        }

        public async Task Run(string server)
        {
            marketDatabase.SetServer(server);
            marketDatabase.LoadDatabaseFromDisk();

            if (marketDatabase.Updated > lastUpdateDate)
            {
                lastUpdateDate = marketDatabase.Updated;

                List<ItemStats> itemStats = new List<ItemStats>();
                foreach (IGrouping<string, MarketListing> listings in marketDatabase.Listings.GroupBy(_ => _.Name))
                {
                    ItemStats itemStat = new ItemStats();
                    itemStat.Name = listings.Key;

                    DateTime toDate = DateTime.UtcNow.Date.AddDays(1);
                    DateTime fromDate = toDate.AddDays(-1);
                    while (fromDate >= DateTime.UtcNow.Date.AddDays(-7))
                    {
                        PeriodicItemStats dailyItemStats = CreateDailyStats(listings, toDate, fromDate);

                        itemStat.DailyStats.Add(dailyItemStats);

                        toDate = fromDate;
                        fromDate = toDate.AddDays(-1);
                    }

                    itemStats.Add(itemStat);
                }

                await SaveAndUploadItemStats(itemStats, server);
            }
        }

        private async Task SaveAndUploadItemStats(List<ItemStats> itemStats, string server)
        {
            itemDatabase.Contents = new ItemDatabaseContents
            {
                Items = itemStats,
                Updated = DateTime.UtcNow,
            };
            itemDatabase.SetServer(server);
            itemDatabase.SaveDatabaseToDisk();

            Trace.WriteLine($"Uploading item data for server {server}...");
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = "nwmarketdata",
                Key = server + "/" + ITEM_TREND_DATA_FILE_NAME,
                FilePath = itemDatabase.GetDataBasePathOnDisk(),
            });
            Trace.WriteLine($"Item data uploaded for server {server}!");
        }

        private PeriodicItemStats CreateDailyStats(IGrouping<string, MarketListing> listings, DateTime toDate, DateTime fromDate)
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

            PeriodicItemStats dailyItemStats = new PeriodicItemStats();
            dailyItemStats.FromDate = fromDate;
            dailyItemStats.ToDate = toDate;
            dailyItemStats.TotalAvailable = activeDailyListings.Sum(_ => _.Instance?.Available ?? 0);
            dailyItemStats.TotalMarket = activeDailyListings.Sum(_ => (_.Instance?.Available ?? 0) * _.Listing.Price);
            dailyItemStats.AveragePrice = dailyItemStats.TotalAvailable > 0 ? dailyItemStats.TotalMarket / dailyItemStats.TotalAvailable : 0;
            dailyItemStats.TotalAvailableBelowMarketAverage = activeDailyListings.Where(_ => _.Listing.Price <= dailyItemStats.AveragePrice).Sum(_ => _.Instance?.Available ?? 0);
            dailyItemStats.MinPrice = activeDailyListings.Any() ? activeDailyListings.Min(_ => _.Listing.Price) : 0f;
            dailyItemStats.MaxPrice = activeDailyListings.Any() ? activeDailyListings.Max(_ => _.Listing.Price) : 0f;
            dailyItemStats.NumListings = activeDailyListings.Count();
            return dailyItemStats;
        }
    }
}
