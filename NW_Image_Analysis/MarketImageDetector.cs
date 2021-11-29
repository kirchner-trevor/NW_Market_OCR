using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Tesseract;

namespace NW_Image_Analysis
{
    public class MarketImageDetector : IDisposable
    {
        private Rectangle DEFAULT_MARKET_AREA = new Rectangle { X = 696, Y = 326, Width = 1130, Height = 730 };
        private Point DEFAULT_SCREEN_SIZE = new Point(1920, 1080);
        private Rectangle DEFAULT_BLUE_BANNER_SAMPLE_AREA = new Rectangle { X = 950, Y = 15, Width = 50, Height = 50 };
        private Color BLUE_BANNER_COLOR = Color.FromArgb(23, 51, 73);
        private const double BLUE_BANNER_AVERAGE_DIFFERENCE_LIMIT = 20;

        private TesseractEngine tesseractEngine = new TesseractEngine(Path.Combine(Directory.GetCurrentDirectory(), "tessdata"), "eng", EngineMode.Default);

        public bool ImageContainsBlueBanner(string path)
        {
            bool containsBlueBanner = false;

            Trace.WriteLine($"Checking image for blue banner at '{path}'...");

            if (File.Exists(path))
            {
                try
                {
                    using (Bitmap myBitmap = new Bitmap(path))
                    {
                        containsBlueBanner = ImageContainsBlueBanner(myBitmap);
                    }
                }
                catch (Exception)
                {
                    Trace.TraceWarning($"Failed to check image for blue banner for '{path}', assuming its not there.");
                }
            }

            return containsBlueBanner;
        }

        public bool ImageContainsBlueBanner(Bitmap myBitmap)
        {
            bool containsBlueBanner = false;

            ScreenAdjustmentParameters screenAdjustments = GetScreenAdjustmentsForWindow(myBitmap.Width, myBitmap.Height);
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

        public ScreenAdjustmentParameters GetScreenAdjustmentsForWindow(float width, float height)
        {
            ScreenAdjustmentParameters screenAdjustments;

            float yRatio = height / DEFAULT_SCREEN_SIZE.Y;

            // Scale linearly with padding
            if ((height / width) > (DEFAULT_SCREEN_SIZE.Y / DEFAULT_SCREEN_SIZE.X))
            {
                screenAdjustments = new ScreenAdjustmentParameters
                {
                    XPadding = (width - (yRatio * DEFAULT_SCREEN_SIZE.X)) / 2,
                    Scale = yRatio,
                };
            }
            // Scale linearly without padding (probably?)
            else
            {
                screenAdjustments = new ScreenAdjustmentParameters
                {
                    XPadding = 0,
                    Scale = yRatio,
                };
            }

            return screenAdjustments;
        }

        public string CleanInputImage(string path, Rectangle? customMarketArea = null)
        {
            string fileDirectory = Path.GetDirectoryName(path);
            string processedPath = Path.Combine(fileDirectory, "processed.png");

            Trace.WriteLine($"Cleaning image at '{path}'...");

            using (Bitmap original = new Bitmap(path))
            {
                Rectangle marketArea;
                if (customMarketArea.HasValue)
                {
                    marketArea = customMarketArea.Value;
                }
                else
                {
                    marketArea = GetScreenAdjustmentsForWindow(original.Width, original.Height).Adjust(DEFAULT_MARKET_AREA);
                }

                using (Bitmap cropped = original.Clone(marketArea, PixelFormat.Format32bppArgb))
                {
                    const float limit = 0.2f;
                    for (int i = 0; i < cropped.Width; i++)
                    {
                        for (int j = 0; j < cropped.Height; j++)
                        {
                            Color c = cropped.GetPixel(i, j);
                            if (c.GetBrightness() > limit)
                            {
                                cropped.SetPixel(i, j, Color.Black);
                            }
                            else
                            {
                                cropped.SetPixel(i, j, Color.White);
                            }
                        }
                    }

                    cropped.Save(processedPath);
                }
            }

            Trace.WriteLine($"Cleaned image saved to '{processedPath}'");
            return processedPath;
        }

        public string ExtractTextContent(string processedPath)
        {
            using (Pix image = Pix.LoadFromFile(processedPath))
            {
                using (Page page = tesseractEngine.Process(image))
                {
                    return page.GetText();
                }
            }
        }

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    tesseractEngine?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~MarketCollector()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
