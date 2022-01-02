using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NW_Market_Model
{
    public class FileSystemMarketImageRepository : MarketImageRepository
    {
        private readonly string imagesDirectory;

        public FileSystemMarketImageRepository(string imagesDirectory)
        {
            this.imagesDirectory = imagesDirectory;
        }

        public Task<List<MarketImage>> List()
        {
            string[] allMarketImages = Directory.GetFiles(imagesDirectory, "*.png", SearchOption.AllDirectories);

            return Task.FromResult(allMarketImages.Select(path => new FileSystemMarketImage(path)).Cast<MarketImage>().ToList());
        }
    }

    public class FileSystemMarketImage : MarketImage
    {
        public FileSystemMarketImage(string path)
        {
            Url = path;

            FileMetadata fileMetadata = FileFormatMetadata.GetMetadataFromFile(path);
            Server = fileMetadata.ServerId;
            Id = Path.GetFileNameWithoutExtension(path);

            Metadata = new MarketImageMetadata
            {
                Timestamp = fileMetadata.CreationTime,
                User = fileMetadata.User,
            };
        }

        public override Task Delete()
        {
            if (File.Exists(Url))
            {
                File.Delete(Url);
            }

            return Task.CompletedTask;
        }

        public override Task SaveTo(string pathOnDisk)
        {
            if (File.Exists(Url))
            {
                File.Move(Url, pathOnDisk, overwrite: true);
                Url = pathOnDisk;
            }

            return Task.CompletedTask;
        }

        public override string ToString()
        {
            return nameof(FileSystemMarketImageRepository);
        }
    }
}
