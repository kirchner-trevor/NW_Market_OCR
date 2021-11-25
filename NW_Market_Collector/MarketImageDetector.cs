using System;
using System.Diagnostics;
using System.Drawing;

namespace NW_Market_Collector
{
    public class MarketImageDetector
    {
        private Rectangle DEFAULT_BLUE_BANNER_SAMPLE_AREA = new Rectangle { X = 950, Y = 15, Width = 50, Height = 50 };
        private Color BLUE_BANNER_COLOR = Color.FromArgb(23, 51, 73);
        private const double BLUE_BANNER_AVERAGE_DIFFERENCE_LIMIT = 20;

        private readonly ApplicationConfiguration Configuration;

        public MarketImageDetector(ApplicationConfiguration configuration)
        {
            Configuration = configuration;
        }

        public bool ImageContainsBlueBanner(string path)
        {
            bool containsBlueBanner = false;

            Trace.WriteLine($"Checking image for blue banner at '{path}'...");

            using (Bitmap myBitmap = new Bitmap(path))
            {
                containsBlueBanner = ImageContainsBlueBanner(myBitmap);
            }
            return containsBlueBanner;
        }

        public bool ImageContainsBlueBanner(Bitmap myBitmap)
        {
            bool containsBlueBanner = false;

            ScreenAdjustmentParameters screenAdjustments = Configuration.GetScreenAdjustmentsForWindow(myBitmap.Width, myBitmap.Height);
            Rectangle blueBannerSampleArea = screenAdjustments.Adjust(DEFAULT_BLUE_BANNER_SAMPLE_AREA);

            double totalDifference = 0f;
            for (int x = 0; x < blueBannerSampleArea.Width; x++)
            {
                for (int y = 0; y < blueBannerSampleArea.Height; y++)
                {
                    Color color = myBitmap.GetPixel(blueBannerSampleArea.X + x, blueBannerSampleArea.Y + y);
                    totalDifference += Math.Sqrt(Math.Pow(color.R - BLUE_BANNER_COLOR.R, 2) + Math.Pow(color.G - BLUE_BANNER_COLOR.G, 2) + Math.Pow(color.B - BLUE_BANNER_COLOR.B, 2));
                }
            }

            double averageDifference = totalDifference / (blueBannerSampleArea.Width * blueBannerSampleArea.Height);

            if (averageDifference < BLUE_BANNER_AVERAGE_DIFFERENCE_LIMIT)
            {
                containsBlueBanner = true;
            }

            return containsBlueBanner;
        }
    }
}
