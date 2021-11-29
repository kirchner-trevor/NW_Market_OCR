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

            public static implicit operator ConfigurationRectangle(Rectangle r) => new ConfigurationRectangle { X = r.X, Y = r.Y, Height = r.Height, Width = r.Width };
            public static explicit operator Rectangle(ConfigurationRectangle r) => new Rectangle { X = r.X, Y = r.Y, Height = r.Height, Width = r.Width };
        }

        public bool IsCustomMarketArea()
        {
            return CustomMarketArea.Width != 0 && CustomMarketArea.Height != 0;
        }
    }

    public enum CollectorMode
    {
        AutoScreenShot = 0,
        Video = 1,
    }
}
