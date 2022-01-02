using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using NW_Market_Model;
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
    public class MarketOCR
    {
        public const string OCR_KIND_DECIMALS = "decimals";
        public const string OCR_KIND_LETTERS = "letters";

        private const int MIN_MATCHING_DATAPOINTS_FOR_SKIP = 30;
        private const int MIN_LISTINGS_FOR_AVERAGE_PRICE_ADJUSTMENT = 9;
        private const int MAX_AUTOCORRECT_DISTANCE = 7;
        private const string DATA_DIRECTORY = @"C:\Users\kirch\source\repos\NW_Market_OCR\Data";
        private const string IMAGES_DIRECTORY = @"C:\Users\kirch\source\repos\NW_Market_OCR\Data\Images";
        private static Func<string, List<OcrTextArea>> OCR_ENGINE = RunTesseractOcr; //RunIronOcr;

        private static List<ItemsPageData> itemsDatabase = new List<ItemsPageData>();
        private static string[] itemsNames = new string[0];
        private static Dictionary<string, string> itemNameIds = new Dictionary<string, string>();
        private static string[] locationNames = new string[0];
        private static List<ManualAutocorrect> manualAutocorrects = new List<ManualAutocorrect>
        {
            new ManualAutocorrect("Wyrdwood", 3, new[] { "Wymv", "WMwood" }),
        };
        private static MarketListingBuilder marketListingBuilder = new MarketListingBuilder();
        private static TerritoryDatabase territoryDatabase;
        private static MarketOCRStatsRepository marketOCRStatsRepository = new MarketOCRStatsRepository(DATA_DIRECTORY);
        private static MarketOCRStats marketOCRStats;

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
            NwdbInfoApiClient nwdbInfoApiClient = new NwdbInfoApiClient(DATA_DIRECTORY, config["nwdbinfo:UserAgent"]);
            itemsDatabase = await nwdbInfoApiClient.ListItemsAsync();
            itemsNames = itemsDatabase.Select(_ => _.Name).ToArray();
            itemNameIds = itemsDatabase.GroupBy(_ => _.Name).Select(_ => _.OrderBy(item => item.Id).First()).ToDictionary(_ => _.Name, _ => _.Id);

            territoryDatabase = new TerritoryDatabase();
            locationNames = territoryDatabase.List().Select(_ => _.Names[0]).ToArray();

            // Assume the local copy of the database is the latest since we should be the only ones updating it
            MarketDatabase database = new MarketDatabase(DATA_DIRECTORY);

            ConfigurationDatabase configurationDatabase = new ConfigurationDatabase(DATA_DIRECTORY);
            marketOCRStatsRepository.LoadDatabaseFromDisk();

            AmazonS3Client s3Client = new AmazonS3Client(s3Config.AccessKeyId, s3Config.SecretAccessKey, RegionEndpoint.USEast2);
            MarketImageRepository localMarketImageRepository = new FileSystemMarketImageRepository(IMAGES_DIRECTORY);
            MarketImageRepository remoteMarketImageRepository = new S3MarketImageRepository(s3Client);

            while (true)
            {
                marketOCRStats = new MarketOCRStats();
                marketOCRStatsRepository.Contents.Stats.Add(marketOCRStats);

                await ProcessImages(database, s3Client, localMarketImageRepository);
                await ProcessImages(database, s3Client, remoteMarketImageRepository);

                Trace.WriteLine($"[{DateTime.UtcNow}] Sleeping for 15 minutes!");
                Thread.Sleep(TimeSpan.FromMinutes(15));
            }
        }

        private static async Task ProcessImages(MarketDatabase database, AmazonS3Client s3Client, MarketImageRepository marketImageRepository)
        {
            List<MarketImage> allMarketImages = await marketImageRepository.List();

            if (allMarketImages.Any())
            {
                foreach (IGrouping<string, MarketImage> serverMarketImages in allMarketImages.GroupBy(_ => _.Server))
                {
                    string server = serverMarketImages.Key;
                    database.SetServer(server);
                    database.LoadDatabaseFromDisk();

                    if (!itemsAddedToDatabase.ContainsKey(server))
                    {
                        itemsAddedToDatabase.Add(server, 0);
                        lastDatabaseUploadTime.Add(server, database.Updated);
                    }

                    Trace.WriteLine($"Extracting {serverMarketImages.Key} market data from {serverMarketImages.Count()} images...");
                    foreach (MarketImage nwMarketImage in serverMarketImages)
                    {
                        string processedPath = Path.Combine(Directory.GetCurrentDirectory(), "processed.png");
                        await nwMarketImage.SaveTo(processedPath);
                        Trace.WriteLine($"Processing image '{nwMarketImage.Server}/{nwMarketImage.Id}' from '{nwMarketImage.Metadata.User}' at {nwMarketImage.Metadata.Timestamp}.");
                        marketOCRStats.ImagesProcessed += 1;

                        UpdateDatabaseWithMarketListings(database, processedPath, nwMarketImage.Metadata.Timestamp);

                        await TryUploadDatabaseRateLimited(s3Client, database, server);

                        BackupImageLocally(nwMarketImage, processedPath);

                        await nwMarketImage.Delete();
                        marketOCRStats.To = DateTime.UtcNow;
                        marketOCRStatsRepository.SaveDatabaseToDisk();
                    }

                    if (itemsAddedToDatabase[server] > 0)
                    {
                        Trace.WriteLine($"Found un-uploaded items after processing all images, forcing upload.");
                        await TryUploadDatabaseRateLimited(s3Client, database, server, force: true);
                    }
                }
            }
            else
            {
                Trace.WriteLine($"[{DateTime.UtcNow}] No objects when searching {marketImageRepository}...");
            }
        }

        private static void BackupImageLocally(MarketImage nwMarketImage, string processedPath)
        {
            if (File.Exists(processedPath))
            {
                Trace.WriteLine($"Backing up image {nwMarketImage} to processed folder.");
                string backupDestination = Path.Combine(Directory.GetCurrentDirectory(), "processed", nwMarketImage.Server, nwMarketImage.Id + ".png");
                string processedDirectory = Path.GetDirectoryName(backupDestination);
                Directory.CreateDirectory(processedDirectory);
                string[] previouslyProcessedFiles = Directory.GetFiles(processedDirectory, "*", SearchOption.AllDirectories);
                if (previouslyProcessedFiles.Length > 100)
                {
                    string oldestFile = previouslyProcessedFiles.OrderBy(_ => File.GetCreationTimeUtc(_)).FirstOrDefault();
                    File.Delete(oldestFile);
                }
                File.Move(processedPath, backupDestination, overwrite: true);
            }
        }

        private static TimeSpan databaseUploadDelay = TimeSpan.FromSeconds(30);
        private static TimeSpan maxDatabaseUploadDelay = TimeSpan.FromMinutes(30);
        private static int databaseUploadItemThreshold = 18;

        private static Dictionary<string, DateTime> lastDatabaseUploadTime = new Dictionary<string, DateTime>();
        private static Dictionary<string, int> itemsAddedToDatabase = new Dictionary<string, int>();

        public static async Task TryUploadDatabaseRateLimited(AmazonS3Client s3Client, MarketDatabase database, string server, bool force = false)
        {
            TimeSpan timeSinceLastUpload = DateTime.UtcNow - lastDatabaseUploadTime[server];
            if (force || (itemsAddedToDatabase[server] >= databaseUploadItemThreshold && timeSinceLastUpload > databaseUploadDelay) || (itemsAddedToDatabase[server] > 0 && timeSinceLastUpload > maxDatabaseUploadDelay))
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
                Trace.WriteLine($"Skipping database upload for {server}. Items Added: {itemsAddedToDatabase[server]} >= {databaseUploadItemThreshold} and Time Passed: {Math.Round(timeSinceLastUpload.TotalMinutes, 2)} minutes >= {databaseUploadDelay.TotalMinutes} minutes");
            }
        }

        private class S3Settings
        {
            public string AccessKeyId { get; set; }
            public string SecretAccessKey { get; set; }
        }

        private static TesseractEngine tesseractEngineForDecimals = new TesseractEngine(Path.Combine(Directory.GetCurrentDirectory(), "tessdata/normal"), "eng", EngineMode.TesseractOnly);
        private static TesseractEngine tesseractOcrForLetters = new TesseractEngine(Path.Combine(Directory.GetCurrentDirectory(), "tessdata/best"), "eng", EngineMode.Default);

        private static List<OcrTextArea> RunTesseractOcr(string processedPath)
        {
            tesseractEngineForDecimals.SetVariable("whitelist", ".,0123456789");
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

        private static MarketListing ValidateAndFixMarketListing(MarketListing marketListing)
        {
            (string newName, int nameDistance) = Autocorrect(marketListing.Name, itemsNames);
            if (nameDistance < 0)
            {
                marketListing.Name = marketListing.OriginalName?.Substring(0, marketListing.OriginalName.Length / 2);
                Trace.WriteLine($"Couldn't find correction for name, halving name to '{marketListing.Name}' and trying again...");
                (newName, nameDistance) = Autocorrect(marketListing.Name, itemsNames);
            }
            if (nameDistance > 0)
            {
                Trace.WriteLine($"Updating name from '{marketListing.Name}' to '{newName}' with distance {nameDistance}...");
                marketOCRStats.MarketListingsCorrectedForName += 1;
            }
            marketListing.Name = newName;
            if (marketListing.Name != null)
            {
                marketListing.NameId = itemNameIds.GetValueOrDefault(marketListing.Name, null);
            }

            (string newLocation, int locationDistance) = Autocorrect(marketListing.Location, locationNames);
            if (locationDistance < 0)
            {
                marketListing.Location = marketListing.OriginalLocation?.Substring(0, marketListing.OriginalLocation.Length / 2);
                Trace.WriteLine($"Couldn't find correction for location name, halving name to '{marketListing.Location}' and trying again...");
                (newLocation, locationDistance) = Autocorrect(marketListing.Location, locationNames);
            }
            if (locationDistance > 0)
            {
                Trace.WriteLine($"Updating name from '{marketListing.Location}' to '{newLocation}' with distance {locationDistance}...");
                marketOCRStats.MarketListingsCorrectedForLocation += 1;
            }
            marketListing.Location = newLocation;
            if (marketListing.Location != null)
            {
                marketListing.LocationId = territoryDatabase.Lookup(marketListing.Location)?.TerritoryId;
            }

            if (marketListing.Latest.Available == 0)
            {
                Trace.WriteLine($"Updating available from 0 to 1...");
                marketListing.Latest.Available = 1;
                marketOCRStats.MarketListingsCorrectedForAvailable += 1;
            }

            if (marketListing.Latest.TimeRemaining == TimeSpan.Zero)
            {
                Trace.WriteLine($"Updating time remaining from 0 to 14 days...");
                marketListing.Latest.TimeRemaining = TimeSpan.FromDays(14);
                marketOCRStats.MarketListingsCorrectedForTimeRemaining += 1;
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
            if (value == null || value.Length < 3)
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
            if (minDistance > MAX_AUTOCORRECT_DISTANCE || minDistance > (value.Length / 2f))
            {
                return (null, -1);
            }

            return (minDistanceItemName, minDistance);
        }

        private static List<MarketListing> previousMarketListings = new List<MarketListing>();

        public static void UpdateDatabaseWithMarketListings(MarketDatabase database, string processedPath, DateTime captureTime)
        {
            Point imageSize;
            using (Image image = Image.FromFile(processedPath))
            {
                imageSize = new Point(image.Width, image.Height);
            }

            List<MarketListing> rawMarketListings = marketListingBuilder.BuildFromMany(OCR_ENGINE(processedPath), imageSize.X, imageSize.Y);

            string batchId = Guid.NewGuid().ToString("D");

            List<MarketListing> marketListings = new List<MarketListing>();
            foreach (MarketListing rawMarketListing in rawMarketListings)
            {
                marketOCRStats.MarketListingsExtracted += 1;

                MarketListing newMarketListing = ValidateAndFixMarketListing(rawMarketListing);
                newMarketListing.Latest.BatchId = batchId;
                newMarketListing.Latest.Time = captureTime;

                if (!string.IsNullOrWhiteSpace(newMarketListing.Name) && newMarketListing.Latest.TimeRemaining != default)
                {
                    marketListings.Add(newMarketListing);
                }
                else
                {
                    Trace.WriteLine($"Omitting bad market listing {newMarketListing.ToOriginalString()}");
                    marketOCRStats.MarketListingsOmitted += 1;
                    marketOCRStats.MarketListingsOmittedForName += 1;
                }
            }

            CorrectMarketListingPricesAscending(marketListings);

            if (IsSimilarSetOfMarketListings(marketListings))
            {
                marketOCRStats.ImagesSkippedForSimilarListings += 1;
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
                    Trace.WriteLine($"Omitting bad market listing {marketListing.ToOriginalString()}");
                    marketOCRStats.MarketListingsOmitted += 1;
                    marketOCRStats.MarketListingsOmittedForPrice += 1;
                }
            }

            foreach (MarketListing marketListing in cleanedMarketListing)
            {
                Trace.WriteLine($"Adding market listing {marketListing}");
            }

            MergeSimilarListingsIntoDatabase(cleanedMarketListing, database);

            if (itemsAddedToDatabase.ContainsKey(database.GetServer()))
            {
                itemsAddedToDatabase[database.GetServer()] += cleanedMarketListing.Count;
            }
            else
            {
                itemsAddedToDatabase.Add(database.GetServer(), cleanedMarketListing.Count);
            }

            // Delete all entries that expired more than 8 days ago
            int listingsRemoved = database.Listings.RemoveAll(_ => _.Latest.Time + _.Latest.TimeRemaining < DateTime.UtcNow.AddDays(-8));
            marketOCRStats.MarketListingsRemovedForExpiring += listingsRemoved;

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
                            marketOCRStats.MarketListingsCorrectedForPrice += 1;
                        }
                        else if (isLast && previous.HasValue && previous.Value != 0)
                        {
                            Trace.WriteLine($"Corrected price for '{marketListings[i].Name}' from {marketListings[i].Price} to {previous.Value}");
                            marketListings[i].Price = previous.Value;
                            updatedPrice = true;
                            marketOCRStats.MarketListingsCorrectedForPrice += 1;
                        }
                        else if (marketListings.Count >= 3 && !isFirst && !isLast && next.Value != 0 && previous.Value != 0)
                        {
                            Trace.WriteLine($"Corrected price for '{marketListings[i].Name}' from {marketListings[i].Price} to {(((next.Value - previous.Value) / 2f) + previous.Value)}");
                            marketListings[i].Price = ((next.Value - previous.Value) / 2f) + previous.Value;
                            updatedPrice = true;
                            marketOCRStats.MarketListingsCorrectedForPrice += 1;
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
                    marketOCRStats.MarketListingsAdded += 1;
                }
                else
                {
                    bestMatch.MergeIntoThis(newMarketListing);
                    matchedMarketListings.Add(bestMatch);
                    marketOCRStats.MarketListingsUpdated += 1;
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
