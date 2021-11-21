using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;

namespace MW_Market_Model
{
    [Serializable]
    public class MarketListing
    {
        public MarketListing()
        {
            ListingId = Guid.NewGuid().ToString("D");
            Instances = new List<MarketListingInstance>{ new MarketListingInstance() };
        }

        public string ListingId { get; set; }

        public string Name { get; set; }
        public string OriginalName { get; set; }
        public float Price { get; set; }
        public string OriginalPrice { get; set; }
        public string Location { get; set; }
        public string OriginalLocation { get; set; }

        [JsonIgnore]
        public MarketListingInstance Latest
        {
            get
            {
                return Instances.OrderByDescending(_ => _.Time).FirstOrDefault();
            }
        }

        public List<MarketListingInstance> Instances { get; set; }

        public override string ToString()
        {
            return $"({Latest.Time}) {Name} ${Price} x{Latest.Available} r{Latest.TimeRemaining.TotalHours} @{Location}";
        }

        public MarketListing MergeIntoThis(MarketListing other)
        {
            if (Name == other.Name && Price == other.Price && Location == other.Location)
            {
                Instances.AddRange(other.Instances);
            }
            else
            {
                Trace.WriteLine($"Bad merge requested! Ignoring! This: {this}, Other: {other}");
            }
            return this;
        }

        public bool IsFresh()
        {
            return Latest.Time + Latest.TimeRemaining > DateTime.UtcNow;
        }
    }

    public class MarketListingInstance
    {
        public string BatchId { get; set; }
        public DateTime Time { get; set; }
        public int Available { get; set; }
        public string OriginalAvailable { get; set; }
        public TimeSpan TimeRemaining { get; set; }
        public string OriginalTimeRemaining { get; set; }
    }
}
