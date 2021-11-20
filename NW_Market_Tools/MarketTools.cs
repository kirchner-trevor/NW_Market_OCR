using Amazon;
using Amazon.S3;
using MW_Market_Model;
using System;
using System.Linq;
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
            ConfigurationDatabase configurationDatabase = new ConfigurationDatabase(DATA_DIRECTORY);

            IMarketTool[] perServerTools = new IMarketTool[]
            {
                new ItemPriceTrendFinder(marketDatabase, s3Client, itemDatabase),
                new RecipePriceFinder(accessKeyId, secretAccessKey, itemDatabase),
            };

            IMarketTool[] globalTools = new IMarketTool[]
            {
                new ServerListActivity(configurationDatabase, s3Client, itemDatabase),
            };

            DateTime startTime;
            while (true)
            {
                startTime = DateTime.UtcNow;

                foreach (ServerListInfo server in configurationDatabase.Content.ServerList)
                {
                    foreach (IMarketTool tool in perServerTools)
                    {
                        await tool.Run(server.Id);
                    }
                }

                foreach (IMarketTool tool in globalTools)
                {
                    await tool.Run(null);
                }

                int sleepTimeMs = (int)Math.Max(0, TimeSpan.FromMinutes(5).TotalMilliseconds - (DateTime.UtcNow - startTime).TotalMilliseconds);
                Console.WriteLine($"Sleeping for {Math.Round(TimeSpan.FromMilliseconds(sleepTimeMs).TotalMinutes, 2)} minutes!");
                Thread.Sleep(sleepTimeMs);
            }
        }
    }
}
