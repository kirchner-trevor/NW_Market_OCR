using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NW_Market_Model
{
    public interface MarketImageRepository
    {
        public Task<List<MarketImage>> List();
    }

    public abstract class MarketImage
    {
        public string Server { get; set; }
        public string Id { get; set; }
        public string Url { get; set; }
        public MarketImageMetadata Metadata { get; set; }
        public abstract Task SaveTo(string pathOnDisk);
        public abstract Task Delete();

        public override string ToString()
        {
            return $"{Server}/{Id}";
        }
    }

    public class MarketImageMetadata
    {
        public string User { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
