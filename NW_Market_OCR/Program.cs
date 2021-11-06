using Amazon;
using Amazon.S3;
using IronOcr;
using MW_Market_Model;
using NwdbInfoApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NW_Market_OCR
{
    class Program
    {
        private const int IN_MARKET_DELAY = 6_000;
        private const int OUT_OF_MARKET_DELAY = 15_000;
        // Set your mouse on the next page button when this is true and it will click through the pages
        private const bool AUTOMATIC_MARKET_BROWSING = false;
        private const int MIN_MATCHING_DATAPOINTS_FOR_SKIP = 30;
        private const int WORD_BUCKET_Y_GROUPING_THRESHOLD = 100;
        private static Point NEXT_PAGE = new Point(1800, 227);
        private static Rectangle CONTENT_AREA = new Rectangle() { X = 690, Y = 340, Width = 1140, Height = 700 };
        private static Rectangle MARKET_HEADER_CONTENT_AREA = new Rectangle() { X = 1350, Y = 135, Width = 150, Height = 30 };
        private static Rectangle BASE_IMAGE_SIZE = new Rectangle() { X = 0, Y = 0, Width = 1920, Height = 1080 };
        private static Range NAME_TEXT_X_RANGE = new Range(1550, 2050);
        private static Range PRICE_TEXT_X_RANGE = new Range(2350, 2550);
        private static Range AVAILABLE_TEXT_X_RANGE = new Range(3550, 3650);
        private static Range OWNED_TEXT_X_RANGE = new Range(3650, 3750);
        private static Range TIME_REMAINING_TEXT_X_RANGE = new Range(3750, 3900);
        private static Range LOCATION_TEXT_X_RANGE = new Range(3900, 4300);
        private static Func<string, Rectangle, List<OcrTextArea>> OCR_ENGINE = RunIronOcr;

        private static Rectangle BLUE_BANNER_SAMPLE_AREA = new Rectangle { X = 950, Y = 15, Width = 50, Height = 50 };
        private static Color BLUE_BANNER_COLOR = Color.FromArgb(23, 51, 73);
        private const double BLUE_BANNER_AVERAGE_DIFFERENCE_LIMIT = 20;

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
            bool isBottomOfMarketPage = true;

            Console.WriteLine($"Loading item database...");
            itemsDatabase = await new NwdbInfoApiClient(@"C:\Users\kirch\source\repos\NW_Market_OCR\Data").ListItemsAsync();
            itemsNames = itemsDatabase.Select(_ => _.Name).ToArray();

            Console.WriteLine($"Trying to extract market data from New World on your primary monitor...");

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

                ImageContainsBlueBanner(path);

                string processedPath = CleanInputImage(path);

                if (IsImageShowingMarket(processedPath))
                {
                    Console.Write("Found market interface, processing market listings... ");

                    UpdateDatabaseWithMarketListings(database, processedPath);

                    if (AUTOMATIC_MARKET_BROWSING)
                    {
                        // TODO : Choose a single settlement to browse (as you can only load 500 listings at once)

                        // TODO : Extract and save number of listings at each settlement

                        if (isBottomOfMarketPage)
                        {
                            // TODO : Move mouse to next page button

                            AutoInput.Click();
                            AutoInput.MouseEntropy(NEXT_PAGE);
                            //isBottomOfMarketPage = false;
                        }
                        else
                        {
                            // TODO : Move mouse to scroll view

                            //AutoInput.ScrollDown(13);
                            //isBottomOfMarketPage = true;
                        }
                    }

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

        private static bool IsImageShowingMarket(string processedPath)
        {
            string marketHeaderText = ExtractMarketHeaderTextFromNewWorldMarketImage(processedPath, OCR_ENGINE);
            return marketHeaderText.Contains("showing") || marketHeaderText.Contains("orders") || marketHeaderText.Contains("at");
        }

        private static string ExtractMarketHeaderTextFromNewWorldMarketImage(string processedPath, Func<string, Rectangle, List<OcrTextArea>> runOcr)
        {
            List<OcrTextArea> words = runOcr(processedPath, MARKET_HEADER_CONTENT_AREA);
            string text = words.Aggregate("", (a, b) => a + b.Text);

            Console.WriteLine($"Found market header text '{text}'.");

            return text.ToLowerInvariant();
        }

        private static Dictionary<int, List<OcrTextArea>> ExtractMarketListingTextFromNewWorldMarketImage(string processedPath, Func<string, Rectangle, List<OcrTextArea>> runOcr)
        {
            List<OcrTextArea> words = runOcr(processedPath, CONTENT_AREA);
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

        private static List<OcrTextArea> RunIronOcr(string processedPath, Rectangle contentArea)
        {
            List<OcrTextArea> textAreas = new();

            IronTesseract Ocr = new();
            Ocr.Configuration.ReadBarCodes = false;
            Ocr.Configuration.EngineMode = TesseractEngineMode.TesseractAndLstm;
            Ocr.Configuration.BlackListCharacters = "`";
            using (var Input = new OcrInput())
            {
                Input.AddImage(processedPath, contentArea);

                OcrResult Result = Ocr.Read(Input);

                foreach (OcrResult.Word word in Result.Words)
                {
                    textAreas.Add(new OcrTextArea { Text = word.Text, X = word.X, Y = word.Y });
                }
            }

            return textAreas;
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
                return Start < value && value < End;
            }
        }

        private static MarketListing CreateMarketListing(List<OcrTextArea> looseWordLine)
        {
            MarketListing marketListing = new MarketListing();

            foreach (OcrTextArea word in looseWordLine)
            {
                if (NAME_TEXT_X_RANGE.Contains(word.X))
                {
                    // Sometimes the name gets split into pieces
                    marketListing.OriginalName = marketListing.OriginalName == null ? word.Text : marketListing.OriginalName + " " + word.Text;
                    marketListing.Name = marketListing.OriginalName;
                }
                else if (PRICE_TEXT_X_RANGE.Contains(word.X))
                {
                    marketListing.OriginalPrice = word.Text;
                    if (float.TryParse(word.Text, out float price))
                    {
                        marketListing.Price = price;
                    }
                }
                else if (AVAILABLE_TEXT_X_RANGE.Contains(word.X))
                {
                    marketListing.OriginalAvailable = word.Text;
                    if (int.TryParse(word.Text, out int available))
                    {
                        marketListing.Available = available;
                    }
                }
                else if (OWNED_TEXT_X_RANGE.Contains(word.X))
                {
                    marketListing.OriginalOwned = word.Text;
                    if (int.TryParse(word.Text, out int owned))
                    {
                        marketListing.Owned = owned;
                    }
                }
                else if (TIME_REMAINING_TEXT_X_RANGE.Contains(word.X))
                {
                    marketListing.OriginalTimeRemaining = word.Text;
                    string cleanedWord = word.Text.Replace(" ", "");
                    if (cleanedWord.EndsWith("h"))
                    {
                        string amountText = cleanedWord.Replace("h", "");
                        if (int.TryParse(amountText, out int timeAmount))
                        {
                            marketListing.TimeRemaining = TimeSpan.FromHours(timeAmount);
                        }
                    }
                    else if (cleanedWord.EndsWith("d"))
                    {
                        string amountText = cleanedWord.Replace("d", "");
                        if (int.TryParse(amountText, out int timeAmount))
                        {
                            marketListing.TimeRemaining = TimeSpan.FromDays(timeAmount);
                        }
                    }
                }
                else if (LOCATION_TEXT_X_RANGE.Contains(word.X))
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

            if (marketListing.Available == 0)
            {
                Console.WriteLine($"Updating available from 0 to 1...");
                marketListing.Available = 1;
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
                return (value, -1);
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

        private static string CleanInputImage(string path)
        {
            string fileDirectory = Path.GetDirectoryName(path);
            string processedPath = Path.Combine(fileDirectory, "processed.png");

            Console.WriteLine($"Cleaning image at '{path}'...");

            using (Bitmap myBitmap = new Bitmap(path))
            {
                const float limit = 0.2f;
                for (int i = 0; i < myBitmap.Width; i++)
                {
                    for (int j = 0; j < myBitmap.Height; j++)
                    {
                        Color c = myBitmap.GetPixel(i, j);
                        if (c.GetBrightness() > limit)
                        {
                            myBitmap.SetPixel(i, j, Color.Black);
                        }
                        else
                        {
                            myBitmap.SetPixel(i, j, Color.White);
                        }
                    }
                }

                myBitmap.Save(processedPath);
            }

            Console.WriteLine($"Cleaned image saved to '{processedPath}'");
            return processedPath;
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

        private static List<MarketListing> previousMarketListings = new List<MarketListing>();

        private static void UpdateDatabaseWithMarketListings(MarketDatabase database, string processedPath)
        {
            Dictionary<int, List<OcrTextArea>> wordBucketsByHeight = ExtractMarketListingTextFromNewWorldMarketImage(processedPath, OCR_ENGINE);

            List<MarketListing> marketListings = new List<MarketListing>();
            foreach (KeyValuePair<int, List<OcrTextArea>> wordBucket in wordBucketsByHeight.OrderBy(_ => _.Key))
            {
                MarketListing newMarketListing = ValidateAndFixMarketListing(CreateMarketListing(wordBucket.Value));

                if (newMarketListing.Name != null)
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

            database.Listings.AddRange(cleanedMarketListing);

            // TODO: Purge entries older than 1 week

            database.SaveDatabaseToDisk();

            // TODO: Upload database somewhere to share
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
                    if (!IsCurrentInOrder(i, marketListings) && (IsPreviousTwoAscending(i, marketListings) && IsNextTwoAscending(i, marketListings)))
                    {
                        float previousNextMidpoint = ((next.Value - previous.Value) / 2f) + previous.Value;
                        Console.WriteLine($"Corrected price for '{marketListings[i].Name}' from {marketListings[i].Price} to {previousNextMidpoint}");
                        marketListings[i].Price = previousNextMidpoint;
                        updatedPrice = true;
                    }
                }
            } while (updatedPrice);
        }

        private static bool IsPreviousTwoAscending(int index, List<MarketListing> marketListings)
        {
            // Start of array
            if (index - 2 < 0)
            {
                return true;
            }

            return marketListings[index - 2].Price <= marketListings[index - 1].Price && marketListings[index - 1].Price <= marketListings[index].Price;
        }

        private static bool IsNextTwoAscending(int index, List<MarketListing> marketListings)
        {
            // End of array
            if (index + 2 >= marketListings.Count)
            {
                return true;
            }

            return marketListings[index].Price <= marketListings[index + 1].Price && marketListings[index + 1].Price <= marketListings[index + 2].Price;
        }

        private static bool IsCurrentInOrder(int index, List<MarketListing> marketListings)
        {
            // Start or end of array
            if (index <= 0 || index + 1 >= marketListings.Count)
            {
                return true;
            }

            return marketListings[index - 1].Price <= marketListings[index].Price && marketListings[index].Price <= marketListings[index + 1].Price;
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

                    matchingDatapoints += previous.Available == current.Available ? 1 : 0;
                    matchingDatapoints += previous.Location == current.Location ? 1 : 0;
                    matchingDatapoints += previous.Name == current.Name ? 1 : 0;
                    matchingDatapoints += previous.Owned == current.Owned ? 1 : 0;
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
    }

    public class OcrTextArea
    {
        public int X;
        public int Y;
        public string Text;
    }
}
