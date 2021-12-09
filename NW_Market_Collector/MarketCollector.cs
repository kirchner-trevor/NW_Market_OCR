using NW_Image_Analysis;
using NW_Market_Model;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NW_Market_Collector
{
    public class MarketCollector
    {
        private const int IN_MARKET_DELAY = 1_000;
        private const int OUT_OF_MARKET_DELAY = 5_000;
        private const int FILE_PROCESSING_DELAY = 30_000;
        private const string LOG_FILE_NAME = "log.txt";

        private static volatile bool isRunning = true;

        private static volatile ConsoleHUDWriter ConsoleHUD = new ConsoleHUDWriter
        {
            TotalItemsSeen = 0,
            TotalItemsProcessed = 0,
            Progress = 0,
            CollectorStatus = "Starting Up",
            ProcessorStatus = "Starting Up",
            LatestTextBlob = "",
        };

        static async Task Main(string[] args)
        {
            ConfigurationDatabase configurationDatabase = new ConfigurationDatabase();

            Trace.Listeners.Add(new TextWriterTraceListener(LOG_FILE_NAME));

            ApplicationConfiguration configuration = new ApplicationConfiguration();

            configuration.Credentials = args.Length >= 1 ? args[0] : null;
            configuration.Server = args.Length >= 2 ? args[1] : null;
            configuration.User = args.Length >= 3 ? args[2] : null;
            configuration.Mode = args.Length >= 4 ? GetMode(args[3]) : null;

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

                if (configuration?.Mode == null)
                {
                    Console.Write("Mode (0 = AutoScreenShot, 1 = Video): ");
                    configuration.Mode = GetMode(Console.ReadLine());
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

        private static CollectorMode GetMode(string mode)
        {
            return mode != null && Enum.TryParse(mode, out CollectorMode collectorMode) ? collectorMode : CollectorMode.AutoScreenShot;
        }

        public static void Start(ApplicationConfiguration configuration)
        {
            MarketImageDetector marketImageDetector = new MarketImageDetector();

            Thread processThread = new Thread(async () =>
            {
                MarketImageUploader marketImageUploader = new MarketImageUploader(configuration, ConsoleHUD, marketImageDetector);
                while (isRunning)
                {
                    DateTime startTime = DateTime.UtcNow;

                    await marketImageUploader.ProcessMarketImages();

                    ConsoleHUD.ProcessorStatus = "Waiting For More Files";
                    Trace.WriteLine("Waiting for more files... ");
                    WaitForTotalTimeToPass(startTime, FILE_PROCESSING_DELAY);
                }
            });

            Thread autoScreenshotThread = new Thread(() =>
            {
                MarketImageGenerator marketImageGenerator = new ScreenshotMarketImageGenerator(ConsoleHUD, marketImageDetector);

                while (isRunning)
                {
                    DateTime startTime = DateTime.UtcNow;

                    bool capturedMarketImage = marketImageGenerator.TryCaptureMarketImage();

                    if (capturedMarketImage)
                    {
                        WaitForTotalTimeToPass(startTime, IN_MARKET_DELAY);
                    }
                    else
                    {
                        WaitForTotalTimeToPass(startTime, OUT_OF_MARKET_DELAY);
                    }
                }
            });

            try
            {
                processThread.Start();

                if (configuration.Mode == CollectorMode.AutoScreenShot)
                {
                    autoScreenshotThread.Start();
                    autoScreenshotThread.Join();
                }
                else if (configuration.Mode == CollectorMode.Video)
                {
                    MarketImageGenerator marketImageGenerator = new VideoFileMarketImageGenerator(marketImageDetector, new VideoImageExtractor());

                    while (isRunning)
                    {
                        DateTime startTime = DateTime.UtcNow;

                        marketImageGenerator.TryCaptureMarketImage();

                        ConsoleHUD.ProcessorStatus = "Waiting For More Files";
                        Trace.WriteLine("Waiting for more files... ");
                        WaitForTotalTimeToPass(startTime, FILE_PROCESSING_DELAY);
                    }
                }
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"NW Market Collector encountered an error and is shutting down. See '{LOG_FILE_NAME}' for details.");
                Trace.Close();
                File.AppendAllText(LOG_FILE_NAME, e.Message);
                Console.ResetColor();

                isRunning = false;
            }
            finally
            {
                marketImageDetector.Dispose();
                processThread.Join();
            }
            Console.Write("Press enter to exit");
            Console.ReadLine();
        }

        private static void WaitForTotalTimeToPass(DateTime startTime, int totalTimeToPass)
        {
            int timePassed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            int timeToWait = Math.Clamp(totalTimeToPass - timePassed, 0, totalTimeToPass);
            Trace.WriteLine($"\tWaiting for {timeToWait}ms...");
            Thread.Sleep(timeToWait);
        }
    }
}
