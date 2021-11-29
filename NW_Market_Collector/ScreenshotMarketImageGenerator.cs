using NW_Image_Analysis;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace NW_Market_Collector
{
    public class ScreenshotMarketImageGenerator : MarketImageGenerator
    {
        private readonly ConsoleHUDWriter ConsoleHUD;
        private readonly MarketImageDetector MarketImageDetector;

        public ScreenshotMarketImageGenerator(ConsoleHUDWriter consoleHUDWriter, MarketImageDetector marketImageDetector)
        {
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

            if (MarketImageDetector.ImageContainsBlueBanner(path))
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

        private string MoveNewImageToCaptures(string newPath)
        {
            string capturePath = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "captures", $"market_{Guid.NewGuid():D}.png"));
            File.Move(newPath, capturePath, true);
            return capturePath;
        }
    }
}
