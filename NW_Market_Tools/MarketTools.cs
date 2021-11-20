using Amazon;
using Amazon.S3;
using MW_Market_Model;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NW_Market_Tools
{
    class MarketTools
    {
        private const string DATA_DIRECTORY = @"C:\Users\kirch\source\repos\NW_Market_OCR\Data";

        static async Task Main(string[] args)
        {
            string credentials = args[0];
            string[] credentialParts = credentials.Split(":");
            string accessKeyId = credentialParts[0];
            string secretAccessKey = credentialParts[1];

            MarketDatabase marketDatabase = new MarketDatabase(DATA_DIRECTORY);
            ItemDatabase itemDatabase = new ItemDatabase(DATA_DIRECTORY);
            AmazonS3Client s3Client = new AmazonS3Client(accessKeyId, secretAccessKey, RegionEndpoint.USEast2);
            ConfigurationDatabase configurationDatabase = new ConfigurationDatabase();

            IMarketTool[] tools = new IMarketTool[]
            {
                new ItemPriceTrendFinder(marketDatabase, s3Client, itemDatabase),
                new RecipePriceFinder(accessKeyId, secretAccessKey, itemDatabase),
            };

            DateTime startTime;
            while (true)
            {
                startTime = DateTime.UtcNow;

                foreach (string server in configurationDatabase.ServerList)
                {
                    foreach (IMarketTool tool in tools)
                    {
                        await tool.Run(server);
                    }
                }

                int sleepTimeMs = (int)Math.Max(0, TimeSpan.FromMinutes(5).TotalMilliseconds - (DateTime.UtcNow - startTime).TotalMilliseconds);
                Console.WriteLine($"Sleeping for {Math.Round(TimeSpan.FromMilliseconds(sleepTimeMs).TotalMinutes, 2)} minutes!");
                Thread.Sleep(sleepTimeMs);
            }
        }
    }
}
