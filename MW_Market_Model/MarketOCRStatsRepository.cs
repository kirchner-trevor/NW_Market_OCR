using System;
using System.Collections.Generic;
using System.IO;

namespace NW_Market_Model
{
    public class MarketOCRStatsRepository : Repository<MarketOCRStatsRepositoryContents>
    {
        public MarketOCRStatsRepository(string dataDirectory) : base(dataDirectory)
        {

        }

        protected override string GetFileName()
        {
            return Path.Combine("Stats", "marketOCRStats.json");
        }
    }

    public class MarketOCRStatsRepositoryContents : RepositoryContents
    {
        public List<MarketOCRStats> Stats { get; set; } = new List<MarketOCRStats>();
    }

    public class MarketOCRStats
    {
        public DateTime From { get; set; } = DateTime.UtcNow;
        public DateTime To { get; set; }

        public int ImagesProcessed { get; set; }
        public int ImagesSkippedForSimilarListings { get; set; }
        public int MarketListingsExtracted { get; set; }
        public int MarketListingsAdded { get; set; }
        public int MarketListingsUpdated { get; set; }
        public int MarketListingsRemovedForExpiring { get; set; }
        public int MarketListingsOmitted { get; set; }
        public int MarketListingsOmittedForName { get; set; }
        public int MarketListingsOmittedForPrice { get; set; }
        public int MarketListingsCorrectedForPrice { get; set; }
        public int MarketListingsCorrectedForName { get; set; }
        public int MarketListingsCorrectedForLocation { get; set; }
        public int MarketListingsCorrectedForTimeRemaining { get; set; }
        public int MarketListingsCorrectedForAvailable { get; set; }
    }
}
