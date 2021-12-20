using System;
using System.Collections.Generic;
using System.IO;

namespace NW_Market_Model
{
    public class StreamCollectorStatsRepository : Repository<StreamCollectorStatsRepositoryContents>
    {
        public StreamCollectorStatsRepository(string dataDirectory) : base(dataDirectory)
        {

        }

        protected override string GetFileName()
        {
            return Path.Combine("Stats", "streamCollectorStats.json");
        }
    }

    public class StreamCollectorStatsRepositoryContents : RepositoryContents
    {
        public List<StreamCollectorStats> Stats { get; set; } = new List<StreamCollectorStats>();
    }

    public class StreamCollectorStats
    {
        public DateTime From { get; set; } = DateTime.UtcNow;
        public DateTime To { get; set; }

        public int VideosFound { get; set; }
        public int VideosRecentlyCreated { get; set; }
        public int VideosNewToProcess { get; set; }
        public int VideosWithServerInfo { get; set; }
        public int VideosWithServerInfoInTitle { get; set; }
        public int VideosWithServerInfoOnRecord { get; set; }
        public int VideosWithHighResolution { get; set; }
        public int VideosShowingMarket { get; set; }
        public int VideoMinutesSearched { get; set; }
        public int VideoSegmentsShowingMarket { get; set; }
        public int VideoMinutesShowingMarket { get; set; }
    }
}
