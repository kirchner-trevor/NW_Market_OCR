using Amazon.S3;
using Amazon.S3.Model;
using NW_Market_Model;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace NW_Market_Tools
{
    public class ServerListActivity : IMarketTool
    {
        private readonly ConfigurationDatabase configurationDatabase;
        private readonly AmazonS3Client s3Client;
        private readonly MarketDatabase marketDatabase;

        private DateTime lastUpdateDate;

        public ServerListActivity(ConfigurationDatabase configurationDatabase, AmazonS3Client s3Client, MarketDatabase itemDatabase)
        {
            this.configurationDatabase = configurationDatabase;
            this.s3Client = s3Client;
            this.marketDatabase = itemDatabase;
        }


        public async Task Run(string server)
        {
            lastUpdateDate = configurationDatabase.Content.Updated;

            bool didUpdate = false;

            foreach (ServerListInfo serverListInfo in configurationDatabase.Content.ServerList)
            {
                marketDatabase.SetServer(serverListInfo.Id);
                marketDatabase.LoadDatabaseFromDisk();

                if (marketDatabase.Updated > lastUpdateDate)
                {
                    serverListInfo.Listings = marketDatabase.Listings.Count(_ => _.IsFresh());
                    didUpdate = true;
                }
            }

            if (didUpdate)
            {
                await SaveAndUploadServerData();
            }
        }

        private async Task SaveAndUploadServerData()
        {
            configurationDatabase.SaveDatabaseToDisk();
            Trace.WriteLine("Uploading server data...");
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = "nwmarketdata",
                Key = configurationDatabase.GetDataBasePathOnServer(),
                FilePath = configurationDatabase.GetDataBasePathOnDisk(),
            });
            Trace.WriteLine("Server data uploaded!");
        }
    }
}
