using Amazon.S3;
using Amazon.S3.Model;
using NW_Market_Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NW_Market_OCR
{
    public class S3MarketImageRepository : MarketImageRepository
    {
        private readonly AmazonS3Client s3Client;

        public S3MarketImageRepository(AmazonS3Client s3Client)
        {
            this.s3Client = s3Client;
        }

        public async Task<List<MarketImage>> List()
        {
            ListObjectsResponse allMarketImages = await s3Client.ListObjectsAsync(new ListObjectsRequest
            {
                BucketName = "nwmarketimages"
            });

            return allMarketImages.S3Objects.Select(s3Object => new S3MarketImage(s3Client, s3Object)).Cast<MarketImage>().ToList();
        }
    }

    public class S3MarketImage : MarketImage
    {
        private readonly AmazonS3Client s3Client;

        public S3MarketImage(AmazonS3Client s3Client, S3Object s3Object)
        {
            this.s3Client = s3Client;

            Url = $"https://nwmarketimages.s3-us-east-2.amazonaws.com/{s3Object.Key}";
            Server = s3Object.Key.Split("/")[0];
            Id = s3Object.Key.Split("/")[1];
        }

        public override async Task Delete()
        {
            await s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = "nwmarketimages",
                Key = Server + "/" + Id,
            });
        }

        public override async Task SaveTo(string pathOnDisk)
        {
            GetObjectResponse objectResponse = await s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = "nwmarketimages",
                Key = Server + "/" + Id,
            });

            DateTime captureTime = objectResponse.Metadata.Keys.Contains("x-amz-meta-timestamp") ? DateTime.Parse(objectResponse.Metadata["x-amz-meta-timestamp"]) : DateTime.UtcNow;
            string captureUser = objectResponse.Metadata.Keys.Contains("x-amz-meta-user") ? objectResponse.Metadata["x-amz-meta-user"] : null;

            Metadata = new MarketImageMetadata
            {
                Timestamp = captureTime,
                User = captureUser
            };

            await objectResponse.WriteResponseStreamToFileAsync(pathOnDisk, false, new CancellationToken());
        }

        public override string ToString()
        {
            return nameof(S3MarketImageRepository);
        }
    }
}
