using Amazon.S3;
using Amazon.S3.Model;
using MW_Market_Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NW_Market_Tools
{
    public class ServerListActivity : IMarketTool
    {
        private readonly ConfigurationDatabase configurationDatabase;
        private readonly AmazonS3Client s3Client;
        private readonly ItemDatabase itemDatabase;

        private DateTime lastUpdateDate;

        public ServerListActivity(ConfigurationDatabase configurationDatabase, AmazonS3Client s3Client, ItemDatabase itemDatabase)
        {
            this.configurationDatabase = configurationDatabase;
            this.s3Client = s3Client;
            this.itemDatabase = itemDatabase;
        }


        public async Task Run(string server)
        {
            lastUpdateDate = configurationDatabase.Content.Updated;

            bool didUpdate = false;

            foreach (ServerListInfo serverListInfo in configurationDatabase.Content.ServerList)
            {
                itemDatabase.SetServer(serverListInfo.Id);
                itemDatabase.LoadDatabaseFromDisk();

                if (itemDatabase.Contents != null && itemDatabase.Contents.Updated > lastUpdateDate)
                {
                    serverListInfo.Listings = itemDatabase.Contents.Items.Sum(_ => _.DailyStats[0].NumListings);
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
