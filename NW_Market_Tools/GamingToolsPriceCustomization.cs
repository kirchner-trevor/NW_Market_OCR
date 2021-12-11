using Amazon.S3;
using Amazon.S3.Model;
using NW_Market_Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NW_Market_Tools
{
    public class GamingToolsPriceCustomization : IMarketTool
    {
        private const string DATA_FILE_NAME = "gamingToolsPrices.json";

        private readonly MarketDatabase marketDatabase;
        private readonly AmazonS3Client s3Client;
        private readonly string dataDirectory;

        private Dictionary<string, DateTime> lastUpdateDate = new Dictionary<string, DateTime>();
        private string localFilePath;

        public GamingToolsPriceCustomization(MarketDatabase marketDatabase, AmazonS3Client s3Client, string dataDirectory)
        {
            this.marketDatabase = marketDatabase;
            this.s3Client = s3Client;
            this.dataDirectory = dataDirectory;
        }

        public async Task Run(string server)
        {
            lastUpdateDate[server] = DateTime.MinValue;
            localFilePath = Path.Combine(dataDirectory, server, DATA_FILE_NAME);

            if (File.Exists(localFilePath))
            {
                lastUpdateDate[server] = File.GetLastWriteTimeUtc(localFilePath);
            }

            marketDatabase.SetServer(server);
            marketDatabase.LoadDatabaseFromDisk();

            if (marketDatabase.Updated > lastUpdateDate[server])
            {
                lastUpdateDate[server] = marketDatabase.Updated;

                List<PriceData> priceDatas = new List<PriceData>();

                foreach (MarketListing marketListing in marketDatabase.Listings)
                {
                    PriceData priceData = new PriceData
                    {
                        Availability = marketListing.Latest.Available,
                        GearScore = null,
                        ItemId = marketListing.NameId,
                        ItemName = marketListing.Name,
                        Location = marketListing.Location,
                        LocationId = marketListing.LocationId,
                        Price = (decimal)marketListing.Price,
                        Tier = null,
                        TimeCreatedUtc = marketListing.Latest.Time.ToUniversalTime(),
                    };

                    priceDatas.Add(priceData);
                }

                await SaveAndUpload(server, priceDatas);
            }
        }

        private async Task SaveAndUpload(string server, List<PriceData> priceDatas)
        {
            Trace.WriteLine($"Writing gaming tools data for server {server} to disk...");
            File.WriteAllText(localFilePath, JsonSerializer.Serialize(priceDatas, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }));
            Trace.WriteLine($"Gaming tools data for server {server} written to disk!");

            Trace.WriteLine($"Uploading gaming tools data for server {server}...");
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = "nwmarketdata",
                Key = server + "/" + DATA_FILE_NAME,
                FilePath = localFilePath,
            });
            Trace.WriteLine($"Gaming tools data uploaded for server {server}!");
        }

        private class PriceData
        {
            public string ItemId { get; set; }
            public string ItemName { get; set; }
            public int? Tier { get; set; }
            public decimal Price { get; set; }
            public int Availability { get; set; }
            public int? GearScore { get; set; }
            public string LocationId { get; set; }
            public string Location { get; set; }
            public DateTime TimeCreatedUtc { get; set; }
        }
    }
}
