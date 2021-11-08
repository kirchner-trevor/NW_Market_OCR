using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using IronOcr;
using Microsoft.Extensions.Configuration;
using MW_Market_Model;
using NwdbInfoApi;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NW_Market_OCR
{
    class MarketOCR
    {
        private const int MIN_MATCHING_DATAPOINTS_FOR_SKIP = 30;
        private const int WORD_BUCKET_Y_GROUPING_THRESHOLD = 100;
        private static Func<string, List<OcrTextArea>> OCR_ENGINE = RunIronOcr;

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
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddUserSecrets<MarketOCR>()
                .Build();

            S3Settings s3Config = new S3Settings
            {
                AccessKeyId = config["S3:AccessKeyId"],
                SecretAccessKey = config["S3:SecretAccessKey"],
            };

            Console.WriteLine($"Loading item database...");
            itemsDatabase = await new NwdbInfoApiClient(@"C:\Users\kirch\source\repos\NW_Market_OCR\Data").ListItemsAsync();
            itemsNames = itemsDatabase.Select(_ => _.Name).ToArray();

            Console.WriteLine($"Trying to extract market data from New World on your primary monitor...");

            // Assume the local copy of the database is the latest since we should be the only ones updating it
            MarketDatabase database = new MarketDatabase(@"C:\Users\kirch\source\repos\NW_Market_OCR\Data");
            database.LoadDatabaseFromDisk();

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
                Console.WriteLine($"Searching s3 for images...");
                ListObjectsResponse marketImages = await s3Client.ListObjectsAsync(new ListObjectsRequest
                {
                    BucketName = "nwmarketimages",
                });

                if (marketImages.S3Objects.Any())
                {
                    Console.WriteLine($"Extracting market data from {marketImages.S3Objects.Count} images...");
                    foreach (S3Object nwMarketImageObject in marketImages.S3Objects)
                    {
                        GetObjectResponse mwMarketImage = await s3Client.GetObjectAsync(new GetObjectRequest
                        {
                            BucketName = nwMarketImageObject.BucketName,
                            Key = nwMarketImageObject.Key,
                        });

                        string processedPath = Path.Combine(Directory.GetCurrentDirectory(), "processed.png");
                        await mwMarketImage.WriteResponseStreamToFileAsync(processedPath, false, new CancellationToken());
                        DateTime captureTime = mwMarketImage.Metadata.Keys.Contains("timestamp") ? DateTime.Parse(mwMarketImage.Metadata["timestamp"]) : DateTime.UtcNow;

                        UpdateDatabaseWithMarketListings(database, processedPath, captureTime);

                        await TryUploadDatabaseRateLimited(s3Client, database);

                        await s3Client.DeleteObjectAsync(new DeleteObjectRequest
                        {
                            BucketName = nwMarketImageObject.BucketName,
                            Key = nwMarketImageObject.Key,
                        });
                    }
                }
                else
                {
                    Console.WriteLine("Found no objects in bucket, waiting 30 seconds...");
                    Thread.Sleep(TimeSpan.FromSeconds(30));
                }
            }
        }


        private static DateTime lastDatabaseUploadTime = DateTime.MinValue;
        private static TimeSpan databaseUploadDelay = TimeSpan.FromMinutes(30);
        private static int databaseUploadItemThreshold = 18;

        private static int itemsAddedToDatabase = 0;

        public static async Task TryUploadDatabaseRateLimited(AmazonS3Client s3Client, MarketDatabase database)
        {
            // TODO : Clean database listings for sharing (remove unused data, etc)

            TimeSpan timeSinceLastUpload = DateTime.UtcNow - lastDatabaseUploadTime;
            if (itemsAddedToDatabase >= databaseUploadItemThreshold && timeSinceLastUpload > databaseUploadDelay)
            {
                PutObjectResponse putResponse = await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = "nwmarketdata",
                    Key = "database.json",
                    FilePath = database.GetDataBasePathOnDisk(),
                });
            }
            else
            {
                Console.WriteLine($"Skipping database upload, criteria not met. Items Added: {itemsAddedToDatabase} >= {databaseUploadItemThreshold} and Time Passed: {timeSinceLastUpload.TotalMinutes} minutes >= {databaseUploadDelay.TotalMinutes} minutes");
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
                int yGroupKey = wordBucketsByHeight.Keys.FirstOrDefault(yGroup => Math.Abs(yGroup - word.Y) < WORD_BUCKET_Y_GROUPING_THRESHOLD);
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

        // TesseractOnly does better with prices and detecting decimals, but Default does better detecting words
        // TODO: Try and switch to high quality raw tesseract instead of IronOcr
        private static List<OcrTextArea> RunIronOcr(string processedPath)
        {
            List<OcrTextArea> textAreas = new();

            IronTesseract Ocr = new();
            Ocr.Configuration.ReadBarCodes = false;
            Ocr.Configuration.EngineMode = TesseractEngineMode.TesseractOnly;
            Ocr.Configuration.BlackListCharacters = "`~|[]{}\\/,";
            using (var Input = new OcrInput())
            {
                Input.AddImage(processedPath);

                OcrResult Result = Ocr.Read(Input);

                foreach (OcrResult.Word word in Result.Words)
                {
                    textAreas.Add(new OcrTextArea { Text = word.Text, X = word.X, Y = word.Y });
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

            foreach (OcrTextArea word in looseWordLine)
            {
                if (ColumnMappings.NAME_TEXT_X_RANGE.Contains(word.X))
                {
                    // Sometimes the name gets split into pieces
                    marketListing.OriginalName = marketListing.OriginalName == null ? word.Text : marketListing.OriginalName + " " + word.Text;
                    marketListing.Name = marketListing.OriginalName;
                }
                else if (ColumnMappings.PRICE_TEXT_X_RANGE.Contains(word.X))
                {
                    marketListing.OriginalPrice = word.Text;
                    if (float.TryParse(word.Text, out float price))
                    {
                        marketListing.Price = price;
                    }
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
                    if(cleanedWord.EndsWith("4"))
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
                Console.WriteLine($"Updating name from '{marketListing.Name}' to '{newName}' with distance {nameDistance}...");
            }
            marketListing.Name = newName;

            (string newLocation, int locationDistance) = Autocorrect(marketListing.Location, locationNames);
            if (locationDistance > 0)
            {
                Console.WriteLine($"Updating name from '{marketListing.Location}' to '{newLocation}' with distance {locationDistance}...");
            }
            marketListing.Location = newLocation;

            if (marketListing.Latest.Available == 0)
            {
                Console.WriteLine($"Updating available from 0 to 1...");
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
                        minDistanceItemName = autocorrect.CorrectedValue;
                        Console.WriteLine($"Using manual autocorrect for '{value}'...");
                    }
                }
            }

            return (minDistanceItemName, minDistance);
        }

        private static List<MarketListing> previousMarketListings = new List<MarketListing>();

        private static MarketColumnMappings ColumnMappings = new MarketColumnMappings();

        private class MarketColumnMappings
        {
            private Point DEFAULT_SIZE = new Point(1130, 730);
            private Range DEFAULT_NAME_TEXT_X_RANGE = new Range(8, 754);
            private Range DEFAULT_PRICE_TEXT_X_RANGE = new Range(758, 1132);
            // GS 1133
            private Range DEFAULT_AVAILABLE_TEXT_X_RANGE = new Range(1927, 2074);
            private Range DEFAULT_OWNED_TEXT_X_RANGE = new Range(2075, 2236);
            private Range DEFAULT_TIME_REMAINING_TEXT_X_RANGE = new Range(2237, 2385);
            private Range DEFAULT_LOCATION_TEXT_X_RANGE = new Range(2386, 4300);

            public Range NAME_TEXT_X_RANGE { get; private set; }
            public Range PRICE_TEXT_X_RANGE { get; private set; }
            public Range AVAILABLE_TEXT_X_RANGE { get; private set; }
            public Range OWNED_TEXT_X_RANGE { get; private set; }
            public Range TIME_REMAINING_TEXT_X_RANGE { get; private set; }
            public Range LOCATION_TEXT_X_RANGE { get; private set; }

            public MarketColumnMappings()
            {
                SetSize(DEFAULT_SIZE);
            }

            public void SetSize(Point size)
            {
                float xRatio = size.X / DEFAULT_SIZE.X;

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
                    Console.WriteLine($"Omitting bad market listing {newMarketListing}");
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
                    Console.WriteLine($"Omitting bad market listing {marketListing}");
                }
            }

            foreach (MarketListing marketListing in cleanedMarketListing)
            {
                Console.WriteLine($"{marketListing}");
            }

            MergeSimilarListingsIntoDatabase(cleanedMarketListing, database);

            itemsAddedToDatabase += cleanedMarketListing.Count;

            // TODO: Purge entries older than X months

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
                            Console.WriteLine($"Corrected price for '{marketListings[i].Name}' from {marketListings[i].Price} to {next.Value}");
                            marketListings[i].Price = next.Value;
                            updatedPrice = true;
                        }
                        else if (isLast && previous.HasValue && previous.Value != 0)
                        {
                            Console.WriteLine($"Corrected price for '{marketListings[i].Name}' from {marketListings[i].Price} to {previous.Value}");
                            marketListings[i].Price = previous.Value;
                            updatedPrice = true;
                        }
                        else if (marketListings.Count >= 3 && !isFirst && !isLast && next.Value != 0 && previous.Value != 0)
                        {
                            Console.WriteLine($"Corrected price for '{marketListings[i].Name}' from {marketListings[i].Price} to {(((next.Value - previous.Value) / 2f) + previous.Value)}");
                            marketListings[i].Price = ((next.Value - previous.Value) / 2f) + previous.Value;
                            updatedPrice = true;
                        }
                    }

                    // Santity check
                    // TODO : Improve sanity check to fix more scenarios
                    //if (!IsCurrentInOrder(i, marketListings) && (IsPreviousTwoAscending(i, marketListings) && IsNextTwoAscending(i, marketListings)))
                    //{
                    //    float previousNextMidpoint = ((next.Value - previous.Value) / 2f) + previous.Value;
                    //    Console.WriteLine($"Corrected price for '{marketListings[i].Name}' from {marketListings[i].Price} to {previousNextMidpoint}");
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
                    Console.WriteLine($"Found {matchingDatapoints} matching datapoints from the previous run, skipping database update...");
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
                IEnumerable<MarketListing> newerListings = database.Listings
                    // Listing is newer than the latest
                    .Where(_ => _.Latest.Time <= newMarketListing.Latest.Time);

                IEnumerable<MarketListing> unexpiredListings = newerListings
                    // Not Expired
                    .Where(_ => _.Latest.Time + _.Latest.TimeRemaining > DateTime.UtcNow);

                IEnumerable<MarketListing> matchingFixedValueListings = unexpiredListings
                    // Same fixed values
                    .Where(_ =>
                        _.Location == newMarketListing.Location &&
                        _.Name == newMarketListing.Name &&
                        _.Price == newMarketListing.Price);

                IEnumerable<MarketListing> reasonableChangingValueListings = matchingFixedValueListings
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
    }

    public class OcrTextArea
    {
        public int X;
        public int Y;
        public string Text;
    }
}
