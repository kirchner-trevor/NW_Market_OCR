using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using NW_Market_Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NW_Market_Collector
{
    public interface MarketImageWriteOnlyRepository
    {
        public Task Add(MarketImage marketImage, Dictionary<string, string> additionalMetadata);
    }

    public class S3MarketImageWriteOnlyRepository : MarketImageWriteOnlyRepository
    {
        private readonly AmazonS3Client s3Client;
        private readonly EventHandler<StreamTransferProgressArgs> streamTransferProcessHandler;

        public S3MarketImageWriteOnlyRepository(AmazonS3Client s3Client, EventHandler<StreamTransferProgressArgs> streamTransferProcessHandler)
        {
            this.s3Client = s3Client;
            this.streamTransferProcessHandler = streamTransferProcessHandler;
        }

        public async Task Add(MarketImage marketImage, Dictionary<string, string> additionalMetadata)
        {
            PutObjectRequest putRequest = new PutObjectRequest
            {
                FilePath = marketImage.Url,
                BucketName = "nwmarketimages",
                Key = marketImage.Server + "/" + Guid.NewGuid().ToString("D"),
            };
            putRequest.StreamTransferProgress += streamTransferProcessHandler;
            putRequest.Metadata.Add("timestamp", marketImage.Metadata.Timestamp.ToString("o"));
            putRequest.Metadata.Add("user", marketImage.Metadata.User);
            foreach (KeyValuePair<string, string> metadata in additionalMetadata)
            {
                putRequest.Metadata.Add(metadata.Key, metadata.Value);
            }

            await s3Client.PutObjectAsync(putRequest);
        }
    }

    public class FileSystemMarketImageWriteOnlyRepository : MarketImageWriteOnlyRepository
    {
        private readonly string imagesDirectory;

        public FileSystemMarketImageWriteOnlyRepository(string imagesDirectory)
        {
            this.imagesDirectory = imagesDirectory;
        }

        public async Task Add(MarketImage marketImage, Dictionary<string, string> additionalMetadata)
        {
            string savePathDirectory = Path.Combine(imagesDirectory, marketImage.Server);
            Directory.CreateDirectory(savePathDirectory);
            string savePathOnDisk = Path.Combine(savePathDirectory, marketImage.Id + ".png");
            await marketImage.SaveTo(savePathOnDisk);
        }
    }
}
