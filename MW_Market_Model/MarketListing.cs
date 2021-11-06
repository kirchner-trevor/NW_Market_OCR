using System;

namespace MW_Market_Model
{
    [Serializable]
    public class MarketListing
    {
        public MarketListing()
        {
            Time = DateTime.UtcNow;
        }

        public DateTime Time { get; set; }
        public string Name { get; set; }
        public string OriginalName { get; set; }
        public float Price { get; set; }
        public string OriginalPrice { get; set; }
        public int Available { get; set; }
        public string OriginalAvailable { get; set; }
        public int Owned { get; set; }
        public string OriginalOwned { get; set; }
        public string Location { get; set; }
        public string OriginalLocation { get; set; }
        public TimeSpan TimeRemaining { get; set; }
        public string OriginalTimeRemaining { get; set; }

        public override string ToString()
        {
            return $"({Time}) {Name} ${Price} x{Available} xo{Owned} r{TimeRemaining.TotalHours} @{Location}";
        }
    }
}
