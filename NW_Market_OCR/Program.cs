using Aspose.OCR;
using IronOcr;
using MW_Market_Model;
using NwdbInfoApi;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NW_Market_OCR
{
    class Program
    {
        private const int IN_MARKET_DELAY = 6_000;
        private const int OUT_OF_MARKET_DELAY = 15_000;
        // Set your mouse on the next page button when this is true and it will click through the pages
        private const bool AUTOMATIC_MARKET_BROWSING = false;
        private const int MIN_MATCHING_DATAPOINTS_FOR_SKIP = 30;
        private static Point NEXT_PAGE = new Point(1800, 227);
        private static Rectangle CONTENT_AREA = new Rectangle() { X = 690, Y = 340, Width = 1140, Height = 700 };

        // TODO: Create a companion app that is an "offline" market that allows better searching and give info like "cost to craft" and "exp / cost", etc

        // TODO: Change the app to collect screenshots frequently while in the market, but do the OCR in another thread (treating each screenshot as a queue of work to be completed)

        // TODO: Use the NWDB site to generate information about which recipes are profitable to craft, or provide the most XP per gold. (https://nwdb.info/db/recipe/2hgreataxe_pestilencet5.json)

        // TODO: Allow auto-collect mode to work of a prioritized list that it continues to adjust priority on (searches with no results = lower priority, same prices each search = lower priority)

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
            //    if (cleanedMarketListing.Name != null && cleanedMarketListing.Price != 0 && cleanedMarketListing.Name != "Silk")
            //    {
            //        cleanedDatabase.Listings.Add(cleanedMarketListing);
            //    }
            //}
            //cleanedDatabase.SaveDatabaseToDisk();

            while (true)
            {
                Console.WriteLine("Checking to see if you're at the market...");
                DateTime startTime = DateTime.UtcNow;

                string path = SaveImageOfPrimaryScreen();

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
            string marketHeaderText = ExtractMarketHeaderTextFromNewWorldMarketImageUsingIron(processedPath);
            return marketHeaderText.Contains("showing") || marketHeaderText.Contains("orders") || marketHeaderText.Contains("at");
        }

        private static string ExtractMarketHeaderTextFromNewWorldMarketImageUsingIron(string processedPath)
        {
            IronTesseract Ocr = new();
            Ocr.Configuration.EngineMode = TesseractEngineMode.TesseractOnly;
            Ocr.Configuration.BlackListCharacters = "`";
            using (var Input = new OcrInput())
            {
                var ContentArea = new Rectangle() { X = 1350, Y = 135, Width = 150, Height = 30 };
                Input.AddImage(processedPath, ContentArea);

                OcrResult Result = Ocr.Read(Input);

                Console.WriteLine($"Found market header text '{Result.Text}'.");

                return Result.Text.ToLowerInvariant();
            }
        }

        private static Dictionary<int, List<OcrTextArea>> ExtractMarketListingTextFromNewWorldMarketImageUsingIron(string processedPath)
        {
            Dictionary<int, List<OcrTextArea>> wordBucketsByHeight = new();

            IronTesseract Ocr = new();
            Ocr.Configuration.EngineMode = TesseractEngineMode.TesseractOnly;
            Ocr.Configuration.BlackListCharacters = "`";
            using (var Input = new OcrInput())
            {
                Input.AddImage(processedPath, CONTENT_AREA);

                OcrResult Result = Ocr.Read(Input);

                foreach (var page in Result.Pages)
                {
                    foreach (var line in page.Lines)
                    {
                        foreach (var word in line.Words)
                        {
                            // Find the group within 100 of the words Y value
                            int yGroupKey = wordBucketsByHeight.Keys.FirstOrDefault(yGroup => Math.Abs(yGroup - word.Y) < 100);
                            if (wordBucketsByHeight.ContainsKey(yGroupKey))
                            {
                                wordBucketsByHeight[yGroupKey].Add(new OcrTextArea { Text = word.Text, X = word.X, Y = word.Y });
                            }
                            else
                            {
                                wordBucketsByHeight.Add(word.Y, new List<OcrTextArea> { new OcrTextArea { Text = word.Text, X = word.X, Y = word.Y } });
                            }
                        }
                    }
                }
            }

            return wordBucketsByHeight;
        }

        private static Dictionary<int, List<OcrTextArea>> ExtractTextFromNewWorldMarketImageUsingAspose(string processedPath)
        {
            Dictionary<int, List<OcrTextArea>> wordBucketsByHeight = new();

            AsposeOcr Ocr = new();

            RecognitionResult result = Ocr.RecognizeImage(processedPath, new RecognitionSettings
            {
                IgnoredCharacters = "`",
                RecognitionAreas = new List<Rectangle>
                {
                    new Rectangle() { X = 690, Y = 340, Width = 1140, Height = 660 },
                }
            });


            for (int i = 0; i < result.RecognitionLinesResult.Count; i++)
            {
                Rectangle wordRectangle = result.RecognitionLinesResult[i].Line;
                string wordText = result.RecognitionLinesResult[i].TextInLine;

                // Find the group within 100 of the words Y value
                int yGroupKey = wordBucketsByHeight.Keys.FirstOrDefault(yGroup => Math.Abs(yGroup - wordRectangle.Y) < 100);
                if (wordBucketsByHeight.ContainsKey(yGroupKey))
                {
                    wordBucketsByHeight[yGroupKey].Add(new OcrTextArea { Text = wordText, X = wordRectangle.X, Y = wordRectangle.Y }); ;
                }
                else
                {
                    wordBucketsByHeight.Add(wordRectangle.Y, new List<OcrTextArea> { new OcrTextArea { Text = wordText, X = wordRectangle.X, Y = wordRectangle.Y } });
                }
            }

            return wordBucketsByHeight;
        }

        private static string SaveImageOfPrimaryScreen()
        {
            //Create a new bitmap.
            using (var bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                                           Screen.PrimaryScreen.Bounds.Height,
                                           PixelFormat.Format32bppArgb))
            {
                // Create a graphics object from the bitmap.
                using (var gfxScreenshot = Graphics.FromImage(bmpScreenshot))
                {
                    // Take the screenshot from the upper left corner to the right bottom corner.
                    gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                                                Screen.PrimaryScreen.Bounds.Y,
                                                0,
                                                0,
                                                Screen.PrimaryScreen.Bounds.Size,
                                                CopyPixelOperation.SourceCopy);

                    // Save the screenshot to the specified path that the user has chosen.
                    bmpScreenshot.Save("Screenshot.png", ImageFormat.Png);
                }
            }

            string path = Path.Combine(Directory.GetCurrentDirectory(), "Screenshot.png");
            return path;
        }

        private static MarketListing CreateMarketListing(List<OcrTextArea> looseWordLine)
        {
            MarketListing marketListing = new MarketListing();

            foreach (OcrTextArea word in looseWordLine)
            {
                if (word.X > 1550 && word.X < 2050)
                {
                    // Sometimes the name gets split into pieces
                    marketListing.Name = marketListing.Name == null ? word.Text : marketListing.Name + " " + word.Text;
                }
                else if (word.X > 2350 && word.X < 2550)
                {
                    if (float.TryParse(word.Text, out float price))
                    {
                        marketListing.Price = price;
                    }
                }
                else if (word.X > 3450 && word.X < 3650)
                {
                    if (int.TryParse(word.Text, out int available))
                    {
                        marketListing.Available = available;
                    }
                }
                else if (word.X > 3650 && word.X < 3750)
                {
                    if (int.TryParse(word.Text, out int owned))
                    {
                        marketListing.Owned = owned;
                    }
                }
                else if (word.X > 3750 && word.X < 3900)
                {
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
                else if (word.X > 3900 && word.X < 4300)
                {
                    // Sometimes the name gets split into pieces
                    marketListing.Location = marketListing.Location == null ? word.Text : marketListing.Location + word.Text;
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

        private static List<MarketListing> previousMarketListings = new List<MarketListing>();

        private static void UpdateDatabaseWithMarketListings(MarketDatabase database, string processedPath)
        {
            Dictionary<int, List<OcrTextArea>> wordBucketsByHeight = ExtractMarketListingTextFromNewWorldMarketImageUsingIron(processedPath);

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
