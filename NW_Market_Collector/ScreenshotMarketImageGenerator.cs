using NW_Image_Analysis;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace NW_Market_Collector
{
    public class ScreenshotMarketImageGenerator : MarketImageGenerator
    {
        private readonly ApplicationConfiguration Configuration;
        private readonly ConsoleHUDWriter ConsoleHUD;
        private readonly MarketImageDetector MarketImageDetector;

        public ScreenshotMarketImageGenerator(ApplicationConfiguration configuration, ConsoleHUDWriter consoleHUDWriter, MarketImageDetector marketImageDetector)
        {
            Configuration = configuration;
            ConsoleHUD = consoleHUDWriter;
            MarketImageDetector = marketImageDetector;
        }

        public bool TryCaptureMarketImage()
        {
            Trace.WriteLine("Checking to see if you're at the market...");

            string path = SaveImageOfNewWorld();
            if (path == null)
            {
                Trace.WriteLine("Cannot find focused new world window... ");
                ConsoleHUD.CollectorStatus = "Looking for New World";
                return false;
            }

            if (MarketImageDetector.ImageContainsTradingPost(path))
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

        private string MoveNewImageToCaptures(string path)
        {
            string newPath = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "captures", $"market_{Configuration.User}_{Configuration.Server}_{Guid.NewGuid():D}.png"));
            File.Move(path, newPath, true);
            return newPath;
        }
    }
}
