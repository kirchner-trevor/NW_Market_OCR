using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace NW_Market_Collector
{
    public class ScreenshotMarketImageGenerator
    {
        private Rectangle DEFAULT_BLUE_BANNER_SAMPLE_AREA = new Rectangle { X = 950, Y = 15, Width = 50, Height = 50 };
        private Color BLUE_BANNER_COLOR = Color.FromArgb(23, 51, 73);
        private const double BLUE_BANNER_AVERAGE_DIFFERENCE_LIMIT = 20;
        private ScreenAdjustmentParameters screenAdjustments = new ScreenAdjustmentParameters();

        private readonly ApplicationConfiguration Configuration;
        private readonly ConsoleHUDWriter ConsoleHUD;

        public ScreenshotMarketImageGenerator(ApplicationConfiguration configuration, ConsoleHUDWriter consoleHUDWriter)
        {
            Configuration = configuration;
            ConsoleHUD = consoleHUDWriter;
        }

        public bool CaptureMarketImage()
        {
            Trace.WriteLine("Checking to see if you're at the market...");

            string path = SaveImageOfNewWorld();
            if (path == null)
            {
                Trace.WriteLine("Cannot find focused new world window... ");
                ConsoleHUD.CollectorStatus = "Looking for New World";
                return false;
            }

            if (ImageContainsBlueBanner(path))
            {
                Trace.WriteLine("Found market interface capturing market listings... ");
                ConsoleHUD.CollectorStatus = "Capturing Market Data";

                MoveNewImageToCaptures(path);

                return true;
            }
            else
            {
                Trace.WriteLine("No market interface... ");
                ConsoleHUD.CollectorStatus = "Looking for Market";

                return false;
            }
        }

        private string SaveImageOfNewWorld()
        {
            IntPtr newWorldWindow = WindowPrinterV2.GetHandleOfFocusedWindowWithName("New World");
            if (newWorldWindow != IntPtr.Zero)
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), "captures");
                Directory.CreateDirectory(path);

                path = Path.Combine(path, $"new.png");

                using (Image bmpScreenshot = WindowPrinterV2.PrintWindow(newWorldWindow))
                {
                    screenAdjustments = Configuration.GetScreenAdjustmentsForWindow(bmpScreenshot.Width, bmpScreenshot.Height);

                    bmpScreenshot.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }

                return path;
            }
            else
            {
                Trace.WriteLine("Cannot find window 'New World'...");
                return null;
            }
        }

        private bool ImageContainsBlueBanner(string path)
        {
            Trace.WriteLine($"Checking image for blue banner at '{path}'...");

            Rectangle blueBannerSampleArea = screenAdjustments.Adjust(DEFAULT_BLUE_BANNER_SAMPLE_AREA);

            using (Bitmap myBitmap = new Bitmap(path))
            {
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
                    return true;
                }
            }
            return false;
        }

        private string MoveNewImageToCaptures(string newPath)
        {
            string capturePath = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "captures", $"market_{Guid.NewGuid():D}.png"));
            File.Move(newPath, capturePath, true);
            return capturePath;
        }
    }
}
