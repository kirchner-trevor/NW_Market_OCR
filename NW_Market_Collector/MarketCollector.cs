using Amazon;
using Amazon.S3;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NW_Market_Collector
{
    class MarketCollector
    {
        private const int IN_MARKET_DELAY = 6_000;
        private const int OUT_OF_MARKET_DELAY = 15_000;
        private static Rectangle BLUE_BANNER_SAMPLE_AREA = new Rectangle { X = 950, Y = 15, Width = 50, Height = 50 };
        private static Color BLUE_BANNER_COLOR = Color.FromArgb(23, 51, 73);
        private const double BLUE_BANNER_AVERAGE_DIFFERENCE_LIMIT = 20;

        static async Task Main(string[] args)
        {
            string credentials = args.Length == 1 ? args[0] : null;

            if (credentials == null)
            {
                Console.WriteLine("Please pass in credentials in the form of 'fdsfdsfds:fdsfdsfs'");
                return;
            }

            string accessKeyId = credentials.Split(":")[0];
            string secretAccessKey = credentials.Split(":")[1];

            while (true)
            {
                Console.WriteLine("Checking to see if you're at the market...");
                DateTime startTime = DateTime.UtcNow;

                string path = SaveImageOfNewWorld();
                if (path == null)
                {
                    int timePassed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                    int timeToWait = Math.Clamp(OUT_OF_MARKET_DELAY - timePassed, 0, OUT_OF_MARKET_DELAY);
                    Console.WriteLine($"\tWaiting for {timeToWait}ms...");
                    Thread.Sleep(timeToWait);
                    continue;
                }

                if (ImageContainsBlueBanner(path))
                {
                    Console.Write("Found market interface capturing market listings... ");

                    AmazonS3Client s3Client = new AmazonS3Client(accessKeyId, secretAccessKey, RegionEndpoint.USEast2);
                    await s3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
                    {
                        FilePath = path,
                        BucketName = "nwmarketimages",
                        Key = Guid.NewGuid().ToString("D"),
                    });

                    int timePassed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                    int timeToWait = Math.Clamp(IN_MARKET_DELAY - timePassed, 0, IN_MARKET_DELAY);
                    Console.WriteLine($"\tWaiting for {timeToWait}ms...");
                    Thread.Sleep(timeToWait);
                }
                else
                {
                    Console.Write("No market interface... ");

                    int timePassed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                    int timeToWait = Math.Clamp(OUT_OF_MARKET_DELAY - timePassed, 0, OUT_OF_MARKET_DELAY);
                    Console.WriteLine($"\tWaiting for {timeToWait}ms...");
                    Thread.Sleep(timeToWait);
                }
            }
        }

        private static IntPtr GetHandleOfNewWorldWindow()
        {
            Process process = Process
                .GetProcesses()
                .SingleOrDefault(x => x.MainWindowTitle.Contains("New World"));

            return process != null ? process.MainWindowHandle : IntPtr.Zero;
        }

        private static string SaveImageOfNewWorld()
        {
            IntPtr newWorldWindow = GetHandleOfNewWorldWindow();
            if (newWorldWindow != IntPtr.Zero)
            {
                using (Bitmap bmpScreenshot = WindowPrinterV2.PrintWindow(newWorldWindow))
                {
                    bmpScreenshot.Save("Screenshot.png", ImageFormat.Png);
                }

                string path = Path.Combine(Directory.GetCurrentDirectory(), "Screenshot.png");
                return path;
            }
            else
            {
                Console.WriteLine("Cannot find window 'New World'...");
                return null;
            }
        }

        public static bool ImageContainsBlueBanner(string path)
        {
            Console.WriteLine($"Checking image for blue banner at '{path}'...");

            using (Bitmap myBitmap = new Bitmap(path))
            {
                double totalDifference = 0f;
                for (int x = 0; x < BLUE_BANNER_SAMPLE_AREA.Width; x++)
                {
                    for (int y = 0; y < BLUE_BANNER_SAMPLE_AREA.Height; y++)
                    {
                        Color color = myBitmap.GetPixel(BLUE_BANNER_SAMPLE_AREA.X + x, BLUE_BANNER_SAMPLE_AREA.Y + y);
                        totalDifference += Math.Sqrt(Math.Pow(color.R - BLUE_BANNER_COLOR.R, 2) + Math.Pow(color.G - BLUE_BANNER_COLOR.G, 2) + Math.Pow(color.B - BLUE_BANNER_COLOR.B, 2));
                    }
                }

                double averageDifference = totalDifference / (BLUE_BANNER_SAMPLE_AREA.Width * BLUE_BANNER_SAMPLE_AREA.Height);

                if (averageDifference < BLUE_BANNER_AVERAGE_DIFFERENCE_LIMIT)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
