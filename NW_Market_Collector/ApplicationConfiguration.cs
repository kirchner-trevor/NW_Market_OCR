using System.Drawing;

namespace NW_Market_Collector
{
    public class ApplicationConfiguration
    {
        public string Credentials { get; set; }
        public string Server { get; set; }
        public string User { get; set; }
        public CollectorMode? Mode { get; set; } = CollectorMode.AutoScreenShot;
        public ConfigurationRectangle CustomMarketArea { get; set; }

        public string GetAccessKeyId()
        {
            string[] credentialPieces = !string.IsNullOrEmpty(Credentials) ? Credentials.Split(":") : new string[2];
            string accessKeyId = credentialPieces[0];
            return accessKeyId;
        }

        public string GetSecretAccessKey()
        {
            string[] credentialPieces = !string.IsNullOrEmpty(Credentials) ? Credentials.Split(":") : new string[2];
            string secretAccessKey = credentialPieces[1];
            return secretAccessKey;
        }

        public struct ConfigurationRectangle
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        private Rectangle DEFAULT_MARKET_AREA = new Rectangle { X = 696, Y = 326, Width = 1130, Height = 730 };
        private Point DEFAULT_SCREEN_SIZE = new Point(1920, 1080);

        public Rectangle GetMarketArea()
        {
            Rectangle marketArea = DEFAULT_MARKET_AREA;

            if (IsCustomMarketArea())
            {
                marketArea = new Rectangle
                {
                    X = CustomMarketArea.X,
                    Y = CustomMarketArea.Y,
                    Width = CustomMarketArea.Width,
                    Height = CustomMarketArea.Height,
                };
            }

            return marketArea;
        }

        public bool IsCustomMarketArea()
        {
            return CustomMarketArea.Width != 0 && CustomMarketArea.Height != 0;
        }

        public ScreenAdjustmentParameters GetScreenAdjustmentsForWindow(float width, float height)
        {
            ScreenAdjustmentParameters screenAdjustments = new ScreenAdjustmentParameters();

            float yRatio = height / DEFAULT_SCREEN_SIZE.Y;

            if ((height / width) > (DEFAULT_SCREEN_SIZE.Y / DEFAULT_SCREEN_SIZE.X))
            {
                screenAdjustments = new ScreenAdjustmentParameters
                {
                    XPadding = (width - (yRatio * DEFAULT_SCREEN_SIZE.X)) / 2,
                    Scale = yRatio,
                };
            }

            return screenAdjustments;
        }
    }

    public enum CollectorMode
    {
        AutoScreenShot = 0,
        Video = 1,
    }
}
