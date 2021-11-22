using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Fastenshtein;
using MW_Market_Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;

namespace NW_Market_Collector
{
    public class MarketCollector : IDisposable
    {
        private const int IN_MARKET_DELAY = 1_000;
        private const int OUT_OF_MARKET_DELAY = 5_000;
        private const int FILE_PROCESSING_DELAY = 30_000;
        private static int NEW_TEXT_CONTENT_THRESHOLD = 100;
        private static Rectangle DEFAULT_BLUE_BANNER_SAMPLE_AREA = new Rectangle { X = 950, Y = 15, Width = 50, Height = 50 };
        private static Color BLUE_BANNER_COLOR = Color.FromArgb(23, 51, 73);
        private const double BLUE_BANNER_AVERAGE_DIFFERENCE_LIMIT = 20;
        private const string IMAGE_TEXT_CACHE_FILE_NAME = "imageTextCache.json";
        private const string LOG_FILE_NAME = "log.txt";
        private static Rectangle DEFAULT_MARKET_AREA = new Rectangle { X = 696, Y = 326, Width = 1130, Height = 730 };
        private static Point DEFAULT_SCREEN_SIZE = new Point(1920, 1080);
        private static Rectangle MarketArea = DEFAULT_MARKET_AREA;
        private static ScreenAdjustmentParameters ScreenAdjustments = new ScreenAdjustmentParameters
        {
            Scale = 1f,
            XPadding = 0
        };

        private static volatile bool isRunning = true;

        private class ConsoleHUDWriter
        {
            private string collectorStatus;
            public string CollectorStatus { get { return collectorStatus; } set { collectorStatus = value; Update(); } }

            private string processorStatus;
            public string ProcessorStatus { get { return processorStatus; } set { processorStatus = value; Update(); } }

            private int progress;
            public int Progress { get { return progress; } set { progress = value; Update(); } }

            private int totalItemsSeen;
            public int TotalItemsSeen { get { return totalItemsSeen; } set { totalItemsSeen = value; Update(); } }

            private string latestTextBlob;
            public string LatestTextBlob { get { return latestTextBlob; } set { latestTextBlob = value; Update(); } }

            private int totalItemsProcessed;
            public int TotalItemsProcessed { get { return totalItemsProcessed; } set { totalItemsProcessed = value; Update(); } }


            private string server;
            public string Server { get { return server; } set { server = value; Update(); } }

            private int step = 0;

            private void Update()
            {
                if (step == 0)
                {
                    Console.Clear();
                }
                else
                {
                    Console.SetCursorPosition(0, 0);
                }

                Console.Write(
                    $"{Rotator()} NW Market Collector - {server} {Rotator()}\n\n" +
                    $"Collector Status: {collectorStatus}{new string(' ', 20)}\n" +
                    $"Processor Status: {processorStatus}{new string(' ', 20)}\n" +
                    $"Items: {totalItemsProcessed} / {totalItemsSeen}{new string(' ', 4)}\n" +
                    $"Upload Progress: {progress}%{new string(' ', 4)}\n" +
                    $"Latest Text Blob: {latestTextBlob?.Substring(0, Math.Min(latestTextBlob?.Length ?? 0, 50))}...\n"
                );
                step = DateTime.UtcNow.Second % 8;
            }

            private string Rotator()
            {
                switch (step)
                {
                    case 0:
                        return "|";
                    case 1:
                        return "/";
                    case 2:
                        return "-";
                    case 3: return "\\";
                    case 4: return "|";
                    case 5: return "/";
                    case 6: return "-";
                    case 7: return "\\";
                }
                return "X";
            }
        }

        private static volatile ConsoleHUDWriter ConsoleHUD = new ConsoleHUDWriter
        {
            TotalItemsSeen = 0,
            TotalItemsProcessed = 0,
            Progress = 0,
            CollectorStatus = "Starting Up",
            ProcessorStatus = "Starting Up",
            LatestTextBlob = "",
        };

        public class ApplicationConfiguration
        {
            public string Credentials { get; set; }
            public string Server { get; set; }
            public string User { get; set; }
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
        }

        static async Task Main(string[] args)
        {
            ConfigurationDatabase configurationDatabase = new ConfigurationDatabase();

            Trace.Listeners.Add(new TextWriterTraceListener(LOG_FILE_NAME));

            ApplicationConfiguration configuration = new ApplicationConfiguration();

            configuration.Credentials = args.Length >= 1 ? args[0] : null;
            configuration.Server = args.Length >= 2 ? args[1] : null;
            configuration.User = args.Length >= 3 ? args[2] : null;

            Console.WriteLine("Welcome to the NW Market Collector!\n");

            if (configuration.Credentials == null)
            {
                if (File.Exists("config.json"))
                {
                    configuration = JsonSerializer.Deserialize<ApplicationConfiguration>(File.ReadAllText("config.json"));
                }

                if (string.IsNullOrWhiteSpace(configuration?.Credentials))
                {
                    Console.Write("Credentials (e.g. xxxxx:yyyyyy): ");
                    configuration.Credentials = Console.ReadLine();
                }

                if (string.IsNullOrWhiteSpace(configuration?.Server))
                {
                    Console.Write("Server (Required): ");
                    configuration.Server = Console.ReadLine();
                }

                if (string.IsNullOrWhiteSpace(configuration?.User))
                {
                    Console.Write("Username (Optional): ");
                    configuration.User = Console.ReadLine();
                }
            }

            configuration.Server = configuration.Server?.ToLowerInvariant();

            if (configuration.GetAccessKeyId() == null || configuration.GetSecretAccessKey() == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Credentials not found! You must provide credentials to use this application.");
                Console.ResetColor();
                Console.Write("Press enter to exit");
                Console.ReadLine();
                return;
            }

            if (string.IsNullOrWhiteSpace(configuration.Server) || !configurationDatabase.Content.ServerList.Select(_ => _.Id).Contains(configuration.Server))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Server '{configuration.Server}' not found! You must provide a valid server to use this application.");
                Console.ResetColor();
                Console.Write("Press enter to exit");
                Console.ReadLine();
                return;
            }

            File.WriteAllText("config.json", JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));

            Start(configuration);
        }

        public static void Start(ApplicationConfiguration configuration)
        {
            Thread processThread = new Thread(async () => await ProcessMarketImagesForever(configuration.GetAccessKeyId(), configuration.GetSecretAccessKey(), configuration));

            try
            {
                processThread.Start();

                while (isRunning)
                {
                    CaptureMarketImage(configuration);
                }
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"NW Market Collector encountered an error and is shutting down. See '{LOG_FILE_NAME}' for details.");
                File.AppendAllText(LOG_FILE_NAME, e.Message);
                Console.ResetColor();

                isRunning = false;
            }
            finally
            {
                processThread.Join();
            }
            Console.Write("Press enter to exit");
            Console.ReadLine();
        }

        private static void CaptureMarketImage(ApplicationConfiguration configuration)
        {
            Trace.WriteLine("Checking to see if you're at the market...");
            DateTime startTime = DateTime.UtcNow;

            string path = SaveImageOfNewWorld(configuration);
            if (path == null)
            {
                ConsoleHUD.CollectorStatus = "Looking for New World";
                int timePassed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                int timeToWait = Math.Clamp(OUT_OF_MARKET_DELAY - timePassed, 0, OUT_OF_MARKET_DELAY);
                Trace.WriteLine($"\tWaiting for {timeToWait}ms...");
                Thread.Sleep(timeToWait);
                return;
            }


            if (ImageContainsBlueBanner(path))
            {
                Trace.WriteLine("Found market interface capturing market listings... ");

                path = MoveNewImageToCaptures(path);

                ConsoleHUD.CollectorStatus = "Capturing Market Data";
                WaitForTotalTimeToPass(startTime, IN_MARKET_DELAY);
            }
            else
            {
                ConsoleHUD.CollectorStatus = "Looking for Market";
                Trace.WriteLine("No market interface... ");
                WaitForTotalTimeToPass(startTime, OUT_OF_MARKET_DELAY);
            }
        }

        private static async Task ProcessMarketImagesForever(string accessKeyId, string secretAccessKey, ApplicationConfiguration configuration)
        {
            AmazonS3Client s3Client = new AmazonS3Client(accessKeyId, secretAccessKey, RegionEndpoint.USEast2);

            string capturesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "captures");
            Directory.CreateDirectory(capturesDirectory);

            ConsoleHUD.Server = configuration.Server;

            while (isRunning)
            {
                DateTime startTime = DateTime.UtcNow;

                List<string> filesToProcess = Directory.GetFiles(capturesDirectory, $"market_*.png").ToList();
                ConsoleHUD.TotalItemsSeen += filesToProcess.Count;
                foreach (string filePath in filesToProcess)
                {
                    ConsoleHUD.Progress = 0;
                    await UploadNwMarketImage(filePath, s3Client, configuration);
                    Trace.WriteLine($"Removing market image...");
                    File.Delete(filePath);
                    ConsoleHUD.TotalItemsProcessed++;
                }

                ConsoleHUD.ProcessorStatus = "Waiting For More Files";
                Trace.WriteLine("Waiting for more files... ");
                WaitForTotalTimeToPass(startTime, FILE_PROCESSING_DELAY);
            }
        }

        private static async Task UploadNwMarketImage(string path, AmazonS3Client s3Client, ApplicationConfiguration configuration)
        {
            ConsoleHUD.ProcessorStatus = "Processing Market Data";

            DateTime fileCreationTime = File.GetCreationTimeUtc(path);
            string processedPath = CleanInputImage(path);
            string textContent = CleanTextContent(ExtractTextContent(processedPath));

            if (IsTextContentNew(textContent, NEW_TEXT_CONTENT_THRESHOLD))
            {
                ConsoleHUD.LatestTextBlob = "(New) " + textContent;
                PutObjectRequest putRequest = new PutObjectRequest
                {
                    FilePath = processedPath,
                    BucketName = "nwmarketimages",
                    Key = configuration.Server + "/" + Guid.NewGuid().ToString("D"),
                };
                putRequest.StreamTransferProgress += new EventHandler<StreamTransferProgressArgs>(UpdateProgress);
                putRequest.Metadata.Add("timestamp", fileCreationTime.ToString("o"));
                putRequest.Metadata.Add("textcontent", textContent);
                putRequest.Metadata.Add("user", configuration.User);

                Trace.WriteLine($"Found new market image '{processedPath}' with text '{textContent.Substring(0, Math.Min(textContent.Length, 20))}...', uploading...");
                await s3Client.PutObjectAsync(putRequest);
            }
            else
            {
                ConsoleHUD.LatestTextBlob = "(Old) " + textContent;
                Trace.WriteLine($"Skipping upload of existing text content...");
            }
        }

        private static void UpdateProgress(object sender, StreamTransferProgressArgs e)
        {
            ConsoleHUD.Progress = e.PercentDone;
        }

        private static readonly Regex whitespaceRegex = new Regex(@"\s+");

        private static string CleanTextContent(string textContent)
        {
            if (textContent == null)
            {
                return null;
            }

            string textNoWhitespace = whitespaceRegex.Replace(textContent ?? "", "");
            byte[] encodedBytes = Encoding.UTF8.GetBytes(textNoWhitespace);
            byte[] convertedBytes = Encoding.Convert(Encoding.UTF8, Encoding.ASCII, encodedBytes);

            return Encoding.ASCII.GetString(convertedBytes);
        }

        private static HashSet<string> previousTextContents;

        private static bool IsTextContentNew(string textContent, int minDistanceForNew)
        {
            if (previousTextContents == null)
            {
                //if (File.Exists(IMAGE_TEXT_CACHE_FILE_NAME))
                //{
                //    string readJson = File.ReadAllText(IMAGE_TEXT_CACHE_FILE_NAME);
                //    previousTextContents = JsonSerializer.Deserialize<HashSet<string>>(readJson);
                //}
                //else
                //{
                previousTextContents = new HashSet<string>();
                //}
            }

            if (previousTextContents.Contains(textContent))
            {
                return false;
            }

            Levenshtein nameLevenshtein = new Levenshtein(textContent);

            foreach (string previousTextContent in previousTextContents)
            {
                int distance = nameLevenshtein.DistanceFrom(previousTextContent);
                if (distance < minDistanceForNew)
                {
                    return false;
                }
            }

            previousTextContents.Add(textContent);

            //string writeJson = JsonSerializer.Serialize(previousTextContents);
            //File.WriteAllText(IMAGE_TEXT_CACHE_FILE_NAME, writeJson);

            return true;
        }

        private static TesseractEngine tesseractEngine = new TesseractEngine(Path.Combine(Directory.GetCurrentDirectory(), "tessdata"), "eng", EngineMode.Default);
        private bool disposedValue;

        private static string ExtractTextContent(string processedPath)
        {
            using (Pix image = Pix.LoadFromFile(processedPath))
            {
                using (Page page = tesseractEngine.Process(image))
                {
                    return page.GetText();
                }
            }
        }

        private static void WaitForTotalTimeToPass(DateTime startTime, int totalTimeToPass)
        {
            int timePassed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            int timeToWait = Math.Clamp(totalTimeToPass - timePassed, 0, totalTimeToPass);
            Trace.WriteLine($"\tWaiting for {timeToWait}ms...");
            Thread.Sleep(timeToWait);
        }

        private static string SaveImageOfNewWorld(ApplicationConfiguration configuration)
        {
            IntPtr newWorldWindow = WindowPrinterV2.GetHandleOfFocusedWindowWithName("New World");
            if (newWorldWindow != IntPtr.Zero)
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), "captures");
                Directory.CreateDirectory(path);

                path = Path.Combine(path, $"new.png");

                using (Image bmpScreenshot = WindowPrinterV2.PrintWindow(newWorldWindow))
                {
                    UpdateMarketAreaUsingWindowSize(bmpScreenshot.Width, bmpScreenshot.Height, configuration);

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

        private static void UpdateMarketAreaUsingWindowSize(float width, float height, ApplicationConfiguration configuration)
        {
            float yRatio = height / DEFAULT_SCREEN_SIZE.Y;

            if (configuration.CustomMarketArea.Width != 0 && configuration.CustomMarketArea.Height != 0)
            {
                MarketArea = new Rectangle
                {
                    X = configuration.CustomMarketArea.X,
                    Y = configuration.CustomMarketArea.Y,
                    Width = configuration.CustomMarketArea.Width,
                    Height = configuration.CustomMarketArea.Height,
                };
            }
            else if ((height / width) > (DEFAULT_SCREEN_SIZE.Y / DEFAULT_SCREEN_SIZE.X))
            {
                ScreenAdjustments = new ScreenAdjustmentParameters
                {
                    XPadding = (width - (yRatio * DEFAULT_SCREEN_SIZE.X)) / 2,
                    Scale = yRatio,
                };
                MarketArea = ScreenAdjustments.Adjust(DEFAULT_MARKET_AREA);
            }
            else
            {
                MarketArea = DEFAULT_MARKET_AREA;
            }
        }

        private class ScreenAdjustmentParameters
        {
            public float XPadding;
            public float Scale;

            public Rectangle Adjust(Rectangle rect)
            {
                return new Rectangle
                {
                    X = (int)Math.Round(XPadding + (Scale * rect.X)),
                    Y = (int)Math.Round(Scale * rect.Y),
                    Width = (int)Math.Round(Scale * rect.Width),
                    Height = (int)Math.Round(Scale * rect.Height),
                };
            }
        }

        private static string MoveNewImageToCaptures(string newPath)
        {
            string capturePath = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "captures", $"market_{Guid.NewGuid():D}.png"));
            File.Move(newPath, capturePath, true);
            return capturePath;
        }

        public static bool ImageContainsBlueBanner(string path)
        {
            Trace.WriteLine($"Checking image for blue banner at '{path}'...");

            Rectangle blueBannerSampleArea = ScreenAdjustments.Adjust(DEFAULT_BLUE_BANNER_SAMPLE_AREA);

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

        private static string CleanInputImage(string path)
        {
            string fileDirectory = Path.GetDirectoryName(path);
            string processedPath = Path.Combine(fileDirectory, "processed.png");

            Trace.WriteLine($"Cleaning image at '{path}'...");

            using (Bitmap original = new Bitmap(path))
            using (Bitmap cropped = original.Clone(MarketArea, PixelFormat.Format32bppArgb))
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

            Trace.WriteLine($"Cleaned image saved to '{processedPath}'");
            return processedPath;
        }

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
