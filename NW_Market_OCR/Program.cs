using Aspose.OCR;
using IronOcr;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace NW_Market_OCR
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"Trying to extract market data from New World on your primary monitor...");

            MarketDatabase database = LoadDatabaseFromDisk();

            while (true)
            {
                Console.WriteLine("Waiting for next input... (c = capture, q = quit)");
                string input = Console.ReadKey().KeyChar.ToString();

                if (input == "q")
                {
                    break;
                }

                string path = SaveImageOfPrimaryScreen();

                string processedPath = CleanInputImage(path);

                Dictionary<int, List<OcrTextArea>> wordBucketsByHeight = ExtractTextFromNewWorldMarketImageUsingIron(processedPath);

                List<MarketListing> marketListings = new List<MarketListing>();
                foreach (KeyValuePair<int, List<OcrTextArea>> wordBucket in wordBucketsByHeight)
                {
                    marketListings.Add(ValidateAndFixMarketListing(CreateMarketListing(wordBucket.Value)));

                    Console.Write($"Bucket @ Y={wordBucket.Key}\n");

                    foreach (OcrTextArea word in wordBucket.Value)
                    {
                        Console.Write($"\t{word.X}, {word.Y} - {word.Text}\n");
                    }

                    Console.Write("\n\n");
                }

                foreach (MarketListing marketListing in marketListings)
                {
                    Console.Write($"{marketListing}\n");
                }

                database.Listings.AddRange(marketListings);

                SaveDatabaseToDisk(database);
            }
        }

        private static void SaveDatabaseToDisk(MarketDatabase database)
        {
            Console.WriteLine("Saving database to disk...");
            string json = JsonSerializer.Serialize(database);
            string databasePath = Path.Combine(Directory.GetCurrentDirectory(), "database.json");
            File.WriteAllText(databasePath, json);
        }

        private static MarketDatabase LoadDatabaseFromDisk()
        {
            Console.WriteLine("Loading database from disk...");
            string databasePath = Path.Combine(Directory.GetCurrentDirectory(), "database.json");
            if (File.Exists(databasePath))
            {
                string json = File.ReadAllText(databasePath);
                return JsonSerializer.Deserialize<MarketDatabase>(json);
            }
            else
            {
                return new MarketDatabase();
            }
        }

        private static Dictionary<int, List<OcrTextArea>> ExtractTextFromNewWorldMarketImageUsingIron(string processedPath)
        {
            Dictionary<int, List<OcrTextArea>> wordBucketsByHeight = new();

            IronTesseract Ocr = new();
            Ocr.Configuration.EngineMode = TesseractEngineMode.TesseractOnly;
            Ocr.Configuration.BlackListCharacters = "`";
            using (var Input = new OcrInput())
            {
                var ContentArea = new Rectangle() { X = 690, Y = 340, Width = 1140, Height = 660 };
                Input.AddImage(processedPath, ContentArea);

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


            for(int i = 0; i < result.RecognitionLinesResult.Count; i++)
            {
                Rectangle wordRectangle = result.RecognitionLinesResult[i].Line;
                string wordText = result.RecognitionLinesResult[i].TextInLine;

                // Find the group within 100 of the words Y value
                int yGroupKey = wordBucketsByHeight.Keys.FirstOrDefault(yGroup => Math.Abs(yGroup - wordRectangle.Y) < 100);
                if (wordBucketsByHeight.ContainsKey(yGroupKey))
                {
                    wordBucketsByHeight[yGroupKey].Add(new OcrTextArea { Text = wordText, X = wordRectangle.X, Y = wordRectangle.Y });;
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
            // TODO: Find the nearest item name and the nearest town name from the database of items and towns and fix them

            // TODO: We know that market listings are sorted by price, so we can do a sanity check on the prices and try and correct them
            return marketListing;
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
    }

    public class OcrTextArea
    {
        public int X;
        public int Y;
        public string Text;
    }

    [Serializable]
    public class MarketListing
    {
        public MarketListing()
        {
            Time = DateTime.UtcNow;
        }

        public string Name { get; set; }
        public float Price { get; set; }
        public int Available { get; set; }
        public int Owned { get; set; }
        public string Location { get; set; }
        public DateTime Time { get; set; }

        public override string ToString()
        {
            return $"({Time}) {Name} ${Price} x{Available} xo{Owned} @{Location}";
        }
    }

    [Serializable]
    public class MarketDatabase
    {
        public MarketDatabase()
        {
            Listings = new List<MarketListing>();
        }

        public List<MarketListing> Listings { get; set; }
    }
}
