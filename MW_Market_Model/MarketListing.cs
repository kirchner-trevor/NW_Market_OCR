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

        public string Name { get; set; }
        public float Price { get; set; }
        public int Available { get; set; }
        public int Owned { get; set; }
        public string Location { get; set; }
        public DateTime Time { get; set; }
        public TimeSpan TimeRemaining { get; set; }

        public override string ToString()
        {
            return $"({Time}) {Name} ${Price} x{Available} xo{Owned} r{TimeRemaining.TotalHours} @{Location}";
        }
    }
}
