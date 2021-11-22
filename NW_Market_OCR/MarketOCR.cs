using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using MW_Market_Model;
using NwdbInfoApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;

namespace NW_Market_OCR
{
    class MarketOCR
    {
        private const int MIN_MATCHING_DATAPOINTS_FOR_SKIP = 30;
        private const int MIN_LISTINGS_FOR_AVERAGE_PRICE_ADJUSTMENT = 9;
        private const int MAX_AUTOCORRECT_DISTANCE = 5;
        private const string OCR_KIND_DECIMALS = "decimals";
        private const string OCR_KIND_LETTERS = "letters";
        private const string DATA_DIRECTORY = @"C:\Users\kirch\source\repos\NW_Market_OCR\Data";
        private static Func<string, List<OcrTextArea>> OCR_ENGINE = RunTesseractOcr; //RunIronOcr;

        // TODO: Create a companion app that is an "offline" market that allows better searching and give info like "cost to craft" and "exp / cost", etc

        // TODO: Change the app to collect screenshots frequently while in the market, but do the OCR in another thread (treating each screenshot as a queue of work to be completed)

        // TODO: Generate "trade-routes" where it generates a trip plan where you buy/sell items at each stop in an efficient way (e.g. In Windsward Sell Ironhide & Buy Silkweed, Then in Monarch's Bluff Sell Silkweek & Buy Iron Ore, Then in Everfall...)

        private static List<ItemsPageData> itemsDatabase = new List<ItemsPageData>();
        private static string[] itemsNames = new string[0];
        private static string[] locationNames = new[] { "Mountainhome", "Mountainrise", "Last Stand", "Cleave's Point", "Eastburn", "Valor Hold", "Mourningdale", "Brightwood", "Weaver's Fen", "Ebonscale Reach", "Everfall", "Restless Shore", "Monarch's Bluff", "Reekwater", "Windsward", "Cutlass Keys", "First Light" };
        private static List<ManualAutocorrect> manualAutocorrects = new List<ManualAutocorrect>
        {
            new ManualAutocorrect("Wyrdwood", 3, new[] { "Wymv", "WMwood" }),
        };

        [STAThread]
        static async Task Main(string[] args)
        {
            Trace.AutoFlush = true;
            Trace.Listeners.Add(new TextWriterTraceListener("log.txt"));
            Trace.Listeners.Add(new ConsoleTraceListener());

            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddUserSecrets<MarketOCR>()
                .Build();

            S3Settings s3Config = new S3Settings
            {
                AccessKeyId = config["S3:AccessKeyId"],
                SecretAccessKey = config["S3:SecretAccessKey"],
            };

            Trace.WriteLine($"Loading item database...");
            itemsDatabase = await new NwdbInfoApiClient(DATA_DIRECTORY).ListItemsAsync();
            itemsNames = itemsDatabase.Select(_ => _.Name).ToArray();

            Trace.WriteLine($"Trying to extract market data from New World on your primary monitor...");

            // Assume the local copy of the database is the latest since we should be the only ones updating it
            MarketDatabase database = new MarketDatabase(DATA_DIRECTORY);

            ConfigurationDatabase configurationDatabase = new ConfigurationDatabase(DATA_DIRECTORY);

            //MarketDatabase cleanedDatabase = new MarketDatabase(@"C:\Users\kirch\source\repos\NW_Market_OCR\Data");
            //foreach (MarketListing marketListing in database.Listings)
            //{
            //    MarketListing cleanedMarketListing = ValidateAndFixMarketListing(marketListing);
            //    if (cleanedMarketListing.Name != null && cleanedMarketListing.Price != 0 && cleanedMarketListing.Available != 0)
            //    {
            //        cleanedDatabase.Listings.Add(cleanedMarketListing);
            //    }
            //}
            //cleanedDatabase.SaveDatabaseToDisk();
            //database = cleanedDatabase;

            AmazonS3Client s3Client = new AmazonS3Client(s3Config.AccessKeyId, s3Config.SecretAccessKey, RegionEndpoint.USEast2);

            while (true)
            {
                ListObjectsResponse allMarketImages = await s3Client.ListObjectsAsync(new ListObjectsRequest
                {
                    BucketName = "nwmarketimages",
                    MaxKeys = 1,
                });

                if (allMarketImages.S3Objects.Any())
                {
                    foreach (ServerListInfo server in configurationDatabase.Content.ServerList)
                    {
                        database.SetServer(server.Id);
                        database.LoadDatabaseFromDisk();

                        if (!itemsAddedToDatabase.ContainsKey(server.Id))
                        {
                            itemsAddedToDatabase.Add(server.Id, 0);
                            lastDatabaseUploadTime.Add(server.Id, database.Updated);
                        }

                        ListObjectsResponse marketImages = await s3Client.ListObjectsAsync(new ListObjectsRequest
                        {
                            BucketName = "nwmarketimages",
                            Prefix = server.Id + "/",
                        });

                        if (marketImages.S3Objects.Any())
                        {
                            Trace.WriteLine($"Extracting market data from {marketImages.S3Objects.Count} images...");
                            foreach (S3Object nwMarketImageObject in marketImages.S3Objects)
                            {
                                GetObjectResponse nwMarketImage = await s3Client.GetObjectAsync(new GetObjectRequest
                                {
                                    BucketName = nwMarketImageObject.BucketName,
                                    Key = nwMarketImageObject.Key,
                                });

                                string processedPath = Path.Combine(Directory.GetCurrentDirectory(), "processed.png");
                                await nwMarketImage.WriteResponseStreamToFileAsync(processedPath, false, new CancellationToken());
                                DateTime captureTime = nwMarketImage.Metadata.Keys.Contains("x-amz-meta-timestamp") ? DateTime.Parse(nwMarketImage.Metadata["x-amz-meta-timestamp"]) : DateTime.UtcNow;
                                string captureUser = nwMarketImage.Metadata.Keys.Contains("x-amz-meta-user") ? nwMarketImage.Metadata["x-amz-meta-user"] : null;
                                Trace.WriteLine($"Processing image '{nwMarketImage.Key}' from '{captureUser}' at {captureTime}.");

                                UpdateDatabaseWithMarketListings(database, processedPath, captureTime);

                                await TryUploadDatabaseRateLimited(s3Client, database, server.Id);

                                BackupImageLocally(nwMarketImage, processedPath);

                                await s3Client.DeleteObjectAsync(new DeleteObjectRequest
                                {
                                    BucketName = nwMarketImageObject.BucketName,
                                    Key = nwMarketImageObject.Key,
                                });
                            }
                        }
                        else
                        {
                            await TryUploadDatabaseRateLimited(s3Client, database, server.Id);
                        }
                    }
                }
                else
                {
                    Trace.WriteLine($"[{DateTime.UtcNow}] No objects in any S3 bucket...");
                }

                Trace.WriteLine($"[{DateTime.UtcNow}] Sleeping for 15 minutes!");
                Thread.Sleep(TimeSpan.FromMinutes(15));
            }
        }

        private static void BackupImageLocally(GetObjectResponse nwMarketImage, string processedPath)
        {
            if (File.Exists(processedPath))
            {
                Trace.WriteLine($"Backing up image {nwMarketImage.Key} to processed folder.");
                string backupDestination = Path.Combine(Directory.GetCurrentDirectory(), "processed", nwMarketImage.Key + ".png");
                string processedDirectory = Path.GetDirectoryName(backupDestination);
                Directory.CreateDirectory(processedDirectory);
                string[] previouslyProcessedFiles = Directory.GetFiles(processedDirectory, "*", SearchOption.AllDirectories);
                if (previouslyProcessedFiles.Length > 100)
                {
                    string oldestFile = previouslyProcessedFiles.OrderBy(_ => File.GetCreationTimeUtc(_)).FirstOrDefault();
                    File.Delete(oldestFile);
                }
                File.Move(processedPath, backupDestination);
            }
        }

        private static TimeSpan databaseUploadDelay = TimeSpan.FromSeconds(30);
        private static TimeSpan maxDatabaseUploadDelay = TimeSpan.FromMinutes(30);
        private static int databaseUploadItemThreshold = 18;

        private static Dictionary<string, DateTime> lastDatabaseUploadTime = new Dictionary<string, DateTime>();
        private static Dictionary<string, int> itemsAddedToDatabase = new Dictionary<string, int>();

        public static async Task TryUploadDatabaseRateLimited(AmazonS3Client s3Client, MarketDatabase database, string server)
        {
            TimeSpan timeSinceLastUpload = DateTime.UtcNow - lastDatabaseUploadTime[server];
            if ((itemsAddedToDatabase[server] >= databaseUploadItemThreshold && timeSinceLastUpload > databaseUploadDelay) || (itemsAddedToDatabase[server] > 0 && timeSinceLastUpload > maxDatabaseUploadDelay))
            {
                PutObjectResponse putResponse = await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = "nwmarketdata",
                    Key = server + "/database.json",
                    FilePath = database.GetDatabasePathOnDisk(),
                });
                lastDatabaseUploadTime[server] = DateTime.UtcNow;
                itemsAddedToDatabase[server] = 0;
            }
            else
            {
                Trace.WriteLine($"Skipping database upload for {server}. Items Added: {itemsAddedToDatabase[server]} >= {databaseUploadItemThreshold} and Time Passed: {(int)timeSinceLastUpload.TotalMinutes} minutes >= {databaseUploadDelay.TotalMinutes} minutes");
            }
        }

        private class S3Settings
        {
            public string AccessKeyId { get; set; }
            public string SecretAccessKey { get; set; }
        }

        private static Dictionary<int, List<OcrTextArea>> ExtractMarketListingTextFromNewWorldMarketImage(string processedPath, Func<string, List<OcrTextArea>> runOcr)
        {
            List<OcrTextArea> words = runOcr(processedPath);
            Dictionary<int, List<OcrTextArea>> wordBucketsByHeight = new();

            foreach (OcrTextArea word in words)
            {
                // Find the group within 100 of the words Y value
                int yGroupKey = wordBucketsByHeight.Keys.FirstOrDefault(yGroup => Math.Abs(yGroup - word.Y) < MarketColumnMappings.WORD_BUCKET_Y_GROUPING_THRESHOLD);
                if (wordBucketsByHeight.ContainsKey(yGroupKey))
                {
                    wordBucketsByHeight[yGroupKey].Add(word);
                }
                else
                {
                    wordBucketsByHeight.Add(word.Y, new List<OcrTextArea> { word });
                }
            }

            return wordBucketsByHeight;
        }

        private static TesseractEngine tesseractEngineForDecimals = new TesseractEngine(Path.Combine(Directory.GetCurrentDirectory(), "tessdata/normal"), "eng", EngineMode.TesseractOnly);
        private static TesseractEngine tesseractOcrForLetters = new TesseractEngine(Path.Combine(Directory.GetCurrentDirectory(), "tessdata/best"), "eng", EngineMode.Default);

        private static List<OcrTextArea> RunTesseractOcr(string processedPath)
        {
            List<OcrTextArea> textAreas = new List<OcrTextArea>();
            textAreas.AddRange(RunTesseractOcr(processedPath, OCR_KIND_DECIMALS, tesseractEngineForDecimals));
            textAreas.AddRange(RunTesseractOcr(processedPath, OCR_KIND_LETTERS, tesseractOcrForLetters));
            return textAreas;
        }

        private static List<OcrTextArea> RunTesseractOcr(string processedPath, string source, TesseractEngine tesseractEngine)
        {
            List<OcrTextArea> textAreas = new();

            using (Pix image = Pix.LoadFromFile(processedPath))
            {
                using (Page page = tesseractEngine.Process(image))
                {
                    using (ResultIterator resultIterator = page.GetIterator())
                    {
                        do
                        {
                            if (resultIterator.TryGetBoundingBox(PageIteratorLevel.Word, out Rect bounds))
                            {
                                textAreas.Add(new OcrTextArea
                                {
                                    Text = resultIterator.GetText(PageIteratorLevel.Word),
                                    X = bounds.X1,
                                    Y = bounds.Y1,
                                    Kind = source,
                                });
                            }
                        } while (resultIterator.Next(PageIteratorLevel.Word));
                    }
                }
            }

            return textAreas;
        }

        private class Range
        {
            public Range(int start, int end)
            {
                Start = start;
                End = end;
            }

            public int Start;
            public int End;

            public bool Contains(int value)
            {
                return Start <= value && value < End;
            }

            public static Range operator *(Range range, float scale)
            {
                return new Range((int)Math.Round(range.Start * scale), (int)Math.Round(range.End * scale));
            }
        }

        private static MarketListing CreateMarketListing(List<OcrTextArea> looseWordLine)
        {
            MarketListing marketListing = new MarketListing();

            foreach (OcrTextArea word in looseWordLine.Where(_ => _.Kind == OCR_KIND_DECIMALS))
            {
                if (ColumnMappings.PRICE_TEXT_X_RANGE.Contains(word.X))
                {
                    marketListing.OriginalPrice = word.Text;
                    if (float.TryParse(word.Text, out float price))
                    {
                        marketListing.Price = price;
                    }
                }
            }

            foreach (OcrTextArea word in looseWordLine.Where(_ => _.Kind == OCR_KIND_LETTERS))
            {
                if (ColumnMappings.NAME_TEXT_X_RANGE.Contains(word.X))
                {
                    // Sometimes the name gets split into pieces
                    marketListing.OriginalName = marketListing.OriginalName == null ? word.Text : marketListing.OriginalName + " " + word.Text;
                    marketListing.Name = marketListing.OriginalName;
                }
                else if (ColumnMappings.AVAILABLE_TEXT_X_RANGE.Contains(word.X))
                {
                    marketListing.Latest.OriginalAvailable = word.Text;
                    if (int.TryParse(word.Text, out int available))
                    {
                        marketListing.Latest.Available = available;
                    }
                }
                else if (ColumnMappings.TIME_REMAINING_TEXT_X_RANGE.Contains(word.X))
                {
                    marketListing.Latest.OriginalTimeRemaining = word.Text;
                    string cleanedWord = word.Text.Replace(" ", "");

                    // Manual corrections
                    if (cleanedWord.EndsWith("4") || cleanedWord.EndsWith("n"))
                    {
                        cleanedWord = cleanedWord.Substring(0, cleanedWord.Length - 1) + "h";
                    }
                    if (cleanedWord.Length == 2 && (cleanedWord.StartsWith("a") || cleanedWord.StartsWith("g")))
                    {
                        cleanedWord = "6" + cleanedWord.Substring(1, cleanedWord.Length - 1);
                    }
                    if (cleanedWord.Length == 2 && cleanedWord.StartsWith("S"))
                    {
                        cleanedWord = "9" + cleanedWord.Substring(1, cleanedWord.Length - 1);
                    }

                    if (cleanedWord.EndsWith("h"))
                    {
                        string amountText = cleanedWord.Replace("h", "");
                        if (int.TryParse(amountText, out int timeAmount))
                        {
                            marketListing.Latest.TimeRemaining = TimeSpan.FromHours(timeAmount);
                        }
                    }
                    else if (cleanedWord.EndsWith("d"))
                    {
                        string amountText = cleanedWord.Replace("d", "");
                        if (int.TryParse(amountText, out int timeAmount))
                        {
                            marketListing.Latest.TimeRemaining = TimeSpan.FromDays(timeAmount);
                        }
                    }
                }
                else if (ColumnMappings.LOCATION_TEXT_X_RANGE.Contains(word.X))
                {
                    marketListing.OriginalLocation = marketListing.OriginalLocation == null ? word.Text : marketListing.OriginalLocation + word.Text;
                    marketListing.Location = marketListing.OriginalLocation;
                }
            }

            return marketListing;
        }

        private static MarketListing ValidateAndFixMarketListing(MarketListing marketListing)
        {
            (string newName, int nameDistance) = Autocorrect(marketListing.Name, itemsNames);
            if (nameDistance > 0)
            {
                Trace.WriteLine($"Updating name from '{marketListing.Name}' to '{newName}' with distance {nameDistance}...");
            }
            marketListing.Name = newName;

            (string newLocation, int locationDistance) = Autocorrect(marketListing.Location, locationNames);
            if (locationDistance > 0)
            {
                Trace.WriteLine($"Updating name from '{marketListing.Location}' to '{newLocation}' with distance {locationDistance}...");
            }
            marketListing.Location = newLocation;

            if (marketListing.Latest.Available == 0)
            {
                Trace.WriteLine($"Updating available from 0 to 1...");
                marketListing.Latest.Available = 1;
            }

            return marketListing;
        }

        private class ManualAutocorrect
        {
            public ManualAutocorrect(string correctedValue, int minLevenshteinDistanceToCorrect, string[] originalContainsValues)
            {
                CorrectedValue = correctedValue;
                MinLevenshteinDistanceToCorrect = minLevenshteinDistanceToCorrect;
                OriginalContainsValues = originalContainsValues;
            }

            public int MinLevenshteinDistanceToCorrect;
            public string[] OriginalContainsValues;
            public string CorrectedValue;
        }

        private static (string Value, int Distance) Autocorrect(string value, string[] potentialValues)
        {
            if (value == null || value.Length <= 3)
            {
                return (null, -1);
            }

            string simplifyString(string complexString) => complexString?.Replace(" ", "").Replace("'", "").ToLowerInvariant() ?? string.Empty;

            Fastenshtein.Levenshtein nameLevenshtein = new Fastenshtein.Levenshtein(simplifyString(value));

            int minDistance = int.MaxValue;
            string minDistanceItemName = null;
            foreach (var potentialValue in potentialValues)
            {
                int levenshteinDistance = nameLevenshtein.DistanceFrom(simplifyString(potentialValue));

                if (levenshteinDistance < minDistance)
                {
                    minDistance = levenshteinDistance;
                    minDistanceItemName = potentialValue;
                }

                if (levenshteinDistance == 0)
                {
                    break;
                }
            }

            foreach (ManualAutocorrect autocorrect in manualAutocorrects)
            {
                if (minDistance >= autocorrect.MinLevenshteinDistanceToCorrect)
                {
                    if (autocorrect.OriginalContainsValues.Any(_ => value.Contains(_)))
                    {
                        minDistance = 0;
                        minDistanceItemName = autocorrect.CorrectedValue;
                        Trace.WriteLine($"Using manual autocorrect for '{value}'...");
                    }
                }
            }

            // Distance is too far to use correction
            if (minDistance > MAX_AUTOCORRECT_DISTANCE)
            {
                return (null, -1);
            }

            return (minDistanceItemName, minDistance);
        }

        private static List<MarketListing> previousMarketListings = new List<MarketListing>();

        private static MarketColumnMappings ColumnMappings = new MarketColumnMappings();

        private class MarketColumnMappings
        {
            // Ocr Units / Ocr Units
            private static Point IRON_OCR_SIZE = new Point(2648, 1711);
            private static Point TESSERACT_OCR_SIZE = new Point(1130, 730);
            private static float X_OCR_RATIO = TESSERACT_OCR_SIZE.X / (IRON_OCR_SIZE.X * 1f);

            // Pixels
            private static Point DEFAULT_SIZE = new Point(1130, 730);

            // Iron Ocr Coordinates
            private Range DEFAULT_NAME_TEXT_X_RANGE = new Range(8, 754);
            private Range DEFAULT_PRICE_TEXT_X_RANGE = new Range(758, 1132);
            // GS 1133
            private Range DEFAULT_AVAILABLE_TEXT_X_RANGE = new Range(1927, 2074);
            private Range DEFAULT_OWNED_TEXT_X_RANGE = new Range(2075, 2236);
            private Range DEFAULT_TIME_REMAINING_TEXT_X_RANGE = new Range(2237, 2385);
            private Range DEFAULT_LOCATION_TEXT_X_RANGE = new Range(2386, 2648);

            public Range NAME_TEXT_X_RANGE { get; private set; }
            public Range PRICE_TEXT_X_RANGE { get; private set; }
            public Range AVAILABLE_TEXT_X_RANGE { get; private set; }
            public Range OWNED_TEXT_X_RANGE { get; private set; }
            public Range TIME_REMAINING_TEXT_X_RANGE { get; private set; }
            public Range LOCATION_TEXT_X_RANGE { get; private set; }

            private static int DEFAULT_WORD_BUCKET_Y_GROUPING_THRESHOLD = 100;
            public static int WORD_BUCKET_Y_GROUPING_THRESHOLD = (int)Math.Round(DEFAULT_WORD_BUCKET_Y_GROUPING_THRESHOLD * X_OCR_RATIO);

            public MarketColumnMappings()
            {
                SetSize(DEFAULT_SIZE);
            }

            public void SetSize(Point size)
            {
                // None * Pixels / Ocr Units
                float xRatio = (X_OCR_RATIO * size.X) / DEFAULT_SIZE.X;

                NAME_TEXT_X_RANGE = DEFAULT_NAME_TEXT_X_RANGE * xRatio;
                PRICE_TEXT_X_RANGE = DEFAULT_PRICE_TEXT_X_RANGE * xRatio;
                AVAILABLE_TEXT_X_RANGE = DEFAULT_AVAILABLE_TEXT_X_RANGE * xRatio;
                OWNED_TEXT_X_RANGE = DEFAULT_OWNED_TEXT_X_RANGE * xRatio;
                TIME_REMAINING_TEXT_X_RANGE = DEFAULT_TIME_REMAINING_TEXT_X_RANGE * xRatio;
                LOCATION_TEXT_X_RANGE = DEFAULT_LOCATION_TEXT_X_RANGE * xRatio;
            }
        }

        private static void UpdateDatabaseWithMarketListings(MarketDatabase database, string processedPath, DateTime captureTime)
        {
            Dictionary<int, List<OcrTextArea>> wordBucketsByHeight = ExtractMarketListingTextFromNewWorldMarketImage(processedPath, OCR_ENGINE);

            using (Image image = Image.FromFile(processedPath))
            {
                ColumnMappings.SetSize(new Point(image.Width, image.Height));
            }

            string batchId = Guid.NewGuid().ToString("D");

            List<MarketListing> marketListings = new List<MarketListing>();
            foreach (KeyValuePair<int, List<OcrTextArea>> wordBucket in wordBucketsByHeight.OrderBy(_ => _.Key))
            {
                MarketListing newMarketListing = ValidateAndFixMarketListing(CreateMarketListing(wordBucket.Value));
                newMarketListing.Latest.BatchId = batchId;
                newMarketListing.Latest.Time = captureTime;

                if (newMarketListing.Name != null && newMarketListing.Latest.TimeRemaining != default)
                {
                    marketListings.Add(newMarketListing);
                }
                else
                {
                    Trace.WriteLine($"Omitting bad market listing {newMarketListing}");
                }
            }

            CorrectMarketListingPricesAscending(marketListings);

            if (IsSimilarSetOfMarketListings(marketListings))
            {
                return;
            }

            previousMarketListings = marketListings;

            List<MarketListing> cleanedMarketListing = new List<MarketListing>();
            foreach (MarketListing marketListing in marketListings)
            {
                if (marketListing.Price != 0)
                {
                    cleanedMarketListing.Add(marketListing);
                }
                else
                {
                    Trace.WriteLine($"Omitting bad market listing {marketListing}");
                }
            }

            foreach (MarketListing marketListing in cleanedMarketListing)
            {
                Trace.WriteLine($"{marketListing}");
            }

            MergeSimilarListingsIntoDatabase(cleanedMarketListing, database);

            if (itemsAddedToDatabase.ContainsKey(database.GetServer()))
            {
                itemsAddedToDatabase[database.GetServer()] += cleanedMarketListing.Count;
            }
            else
            {
                itemsAddedToDatabase.Add(database.GetServer(), 0);
            }

            // Delete all entries that expired more than 8 days ago
            database.Listings.RemoveAll(_ => _.Latest.Time + _.Latest.TimeRemaining < DateTime.UtcNow.AddDays(-8));

            database.SaveDatabaseToDisk();
        }

        private static void CorrectMarketListingPricesAscending(List<MarketListing> marketListings)
        {
            bool updatedPrice;
            do
            {
                updatedPrice = false;

                for (int i = 0; i < marketListings.Count; i++)
                {
                    float current = marketListings[i].Price;

                    bool isFirst = i == 0;
                    bool isLast = i == marketListings.Count - 1;

                    float? previous = i > 0 ? marketListings[i - 1].Price : null;
                    float? next = i < marketListings.Count - 1 ? marketListings[i + 1].Price : null;

                    // First fill any gaps using neighboring listings
                    if (current == 0)
                    {
                        if (isFirst && next.HasValue && next.Value != 0)
                        {
                            Trace.WriteLine($"Corrected price for '{marketListings[i].Name}' from {marketListings[i].Price} to {next.Value}");
                            marketListings[i].Price = next.Value;
                            updatedPrice = true;
                        }
                        else if (isLast && previous.HasValue && previous.Value != 0)
                        {
                            Trace.WriteLine($"Corrected price for '{marketListings[i].Name}' from {marketListings[i].Price} to {previous.Value}");
                            marketListings[i].Price = previous.Value;
                            updatedPrice = true;
                        }
                        else if (marketListings.Count >= 3 && !isFirst && !isLast && next.Value != 0 && previous.Value != 0)
                        {
                            Trace.WriteLine($"Corrected price for '{marketListings[i].Name}' from {marketListings[i].Price} to {(((next.Value - previous.Value) / 2f) + previous.Value)}");
                            marketListings[i].Price = ((next.Value - previous.Value) / 2f) + previous.Value;
                            updatedPrice = true;
                        }
                    }

                    // Santity check
                    // TODO : Improve sanity check to fix more scenarios
                    //if (!IsCurrentInOrder(i, marketListings) && (IsPreviousTwoAscending(i, marketListings) && IsNextTwoAscending(i, marketListings)))
                    //{
                    //    float previousNextMidpoint = ((next.Value - previous.Value) / 2f) + previous.Value;
                    //    Trace.WriteLine($"Corrected price for '{marketListings[i].Name}' from {marketListings[i].Price} to {previousNextMidpoint}");
                    //    marketListings[i].Price = previousNextMidpoint;
                    //    updatedPrice = true;
                    //}
                }
            } while (updatedPrice);
        }

        private static bool IsSimilarSetOfMarketListings(List<MarketListing> marketListings)
        {
            bool foundDuplicateListings = false;
            if (previousMarketListings.Count == marketListings.Count)
            {
                int matchingDatapoints = 0;

                for (int i = 0; i < marketListings.Count; i++)
                {
                    MarketListing previous = previousMarketListings[i];
                    MarketListing current = marketListings[i];

                    matchingDatapoints += previous.Latest.Available == current.Latest.Available ? 1 : 0;
                    matchingDatapoints += previous.Location == current.Location ? 1 : 0;
                    matchingDatapoints += previous.Name == current.Name ? 1 : 0;
                    matchingDatapoints += previous.Price == current.Price ? 1 : 0;
                }

                // If enough of the data is the same from the previous run, exit without updating the database
                if (matchingDatapoints >= MIN_MATCHING_DATAPOINTS_FOR_SKIP)
                {
                    Trace.WriteLine($"Found {matchingDatapoints} matching datapoints from the previous run, skipping database update...");
                    foundDuplicateListings = true;
                }
            }

            return foundDuplicateListings;
        }

        // Only supports merging newer listings, could make this work for merging any order, but would be more complicated
        private static void MergeSimilarListingsIntoDatabase(List<MarketListing> newMarketListings, MarketDatabase database)
        {
            List<MarketListing> matchedMarketListings = new List<MarketListing>();

            foreach (MarketListing newMarketListing in newMarketListings)
            {
                IEnumerable<MarketListing> unexpiredListings = database.Listings
                    // Not Expired
                    .Where(_ => _.Latest.Time + _.Latest.TimeRemaining > newMarketListing.Latest.Time);

                IEnumerable<MarketListing> matchingNameListings = unexpiredListings
                    // Same name
                    .Where(_ =>
                        _.Name == newMarketListing.Name);

                IEnumerable<MarketListing> matchingFixedValuesListings = matchingNameListings
                    // Same fixed values
                    .Where(_ =>
                        _.Location == newMarketListing.Location &&
                        _.Price == newMarketListing.Price);

                IEnumerable<MarketListing> newerListings = matchingFixedValuesListings
                    // Listing is newer than the latest
                    .Where(_ => _.Latest.Time <= newMarketListing.Latest.Time);

                IEnumerable<MarketListing> reasonableChangingValueListings = newerListings
                    // Reasonable changing values
                    .Where(_ =>
                        _.Latest.Available >= newMarketListing.Latest.Available &&
                        _.Latest.TimeRemaining >= newMarketListing.Latest.TimeRemaining);

                IEnumerable<MarketListing> unmatchedListings = reasonableChangingValueListings
                    // The existing market listing hasn't already been matched with one of the other new listings
                    .Where(_ =>
                        !matchedMarketListings.Contains(_));

                IEnumerable<MarketListing> differentBatchListings = unmatchedListings
                    // Didn't come from the same batch of market listings
                    .Where(_ =>
                        _.Latest.BatchId != newMarketListing.Latest.BatchId);

                IEnumerable<MarketListing> existingListingsByPreference = differentBatchListings
                    // Prefer similar expiration times
                    .OrderBy(_ => Math.Abs(((_.Latest.Time + _.Latest.TimeRemaining) - (newMarketListing.Latest.Time + newMarketListing.Latest.TimeRemaining)).TotalHours))
                    // Prefer similar available amounts
                    .OrderBy(_ => Math.Abs(_.Latest.Available - newMarketListing.Latest.Available));

                MarketListing bestMatch = existingListingsByPreference.FirstOrDefault();

                if (bestMatch == null)
                {
                    database.Listings.Add(newMarketListing);
                }
                else
                {
                    bestMatch.MergeIntoThis(newMarketListing);
                    matchedMarketListings.Add(bestMatch);
                }
            }
        }

        // This doesn't work becaus there will always be market outliers that don't fit close to the average
        //private static void TryAdjustListingPriceBasedOnAveragePriceOfItem(MarketListing newMarketListing, IEnumerable<MarketListing> matchingNameListings)
        //{
        //    if (matchingNameListings.Count() > MIN_LISTINGS_FOR_AVERAGE_PRICE_ADJUSTMENT)
        //    {
        //        float averagePrice = matchingNameListings.Average(_ => _.Price);
        //        float[] priceOptions = new[]
        //        {
        //                newMarketListing.Price / 100f,
        //                newMarketListing.Price / 10f,
        //                newMarketListing.Price * 10f,
        //                newMarketListing.Price * 100f
        //            };

        //        float minDifference = Math.Abs(newMarketListing.Price - averagePrice);
        //        float priceChoice = newMarketListing.Price;
        //        bool foundBetterPrice = false;
        //        foreach (float priceOption in priceOptions)
        //        {
        //            float difference = Math.Abs(priceOption - averagePrice);
        //            if (difference < minDifference)
        //            {
        //                minDifference = difference;
        //                priceChoice = priceOption;
        //                foundBetterPrice = true;
        //            }
        //        }

        //        if (foundBetterPrice)
        //        {
        //            Trace.WriteLine($"Corrected price for '{newMarketListing.Name}' from {newMarketListing.Price} to {priceChoice} using closest to average");
        //            newMarketListing.Price = priceChoice;
        //        }
        //    }
        //}
    }

    public class OcrTextArea
    {
        public int X;
        public int Y;
        public string Text;
        public string Kind;
    }
}
