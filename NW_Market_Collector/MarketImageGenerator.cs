namespace NW_Market_Collector
{
    public interface MarketImageGenerator
    {

        /// <summary>
        /// Tries to capture an image of the market with a path of ./captures/market_{guid}.png
        /// </summary>
        /// <returns>true if it is able to capture an image of the market</returns>
        bool TryCaptureMarketImage();
    }
}
