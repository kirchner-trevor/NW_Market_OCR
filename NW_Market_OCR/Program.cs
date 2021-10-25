using IronOcr;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NW_Market_OCR
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"Trying to extract market data from New World on your primary monitor...");

            string input = "";
            while (input != "q")
            {
                Console.WriteLine("Waiting for next input... (c = capture, q = quit)");
                input = Console.ReadKey().KeyChar.ToString();

                string path = SaveImageOfPrimaryScreen();

                string processedPath = CleanInputImage(path);

                Dictionary<int, List<OcrResult.Word>> wordBucketsByHeight = ExtractTextFromNewWorldMarketImage(processedPath);

                List<MarketListing> marketListings = new List<MarketListing>();
                foreach (KeyValuePair<int, List<OcrResult.Word>> wordBucket in wordBucketsByHeight)
                {
                    marketListings.Add(CreateMarketListing(wordBucket.Value));

                    Console.Write($"Bucket @ Y={wordBucket.Key}\n");

                    foreach (OcrResult.Word word in wordBucket.Value)
                    {
                        Console.Write($"\t{word.X}, {word.Y} - {word.Text}\n");
                    }

                    Console.Write("\n\n");
                }

                foreach (MarketListing marketListing in marketListings)
                {
                    Console.Write($"{marketListing}\n");
                }
            }
        }

        private static Dictionary<int, List<OcrResult.Word>> ExtractTextFromNewWorldMarketImage(string processedPath)
        {
            Dictionary<int, List<OcrResult.Word>> wordBucketsByHeight = new();

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
                    Console.Write($"Page @ {page.X},{page.Y}\n");

                    foreach (var line in page.Lines)
                    {
                        Console.Write($"Line @ {line.X},{line.Y}\n");

                        foreach (var word in line.Words)
                        {
                            Console.Write($"\t{word.X}, {word.Y} - {word.Text}\n");

                            // Find the group within 100 of the words Y value
                            int yGroupKey = wordBucketsByHeight.Keys.FirstOrDefault(yGroup => Math.Abs(yGroup - word.Y) < 100);
                            if (wordBucketsByHeight.ContainsKey(yGroupKey))
                            {
                                wordBucketsByHeight[yGroupKey].Add(word);
                            }
                            else
                            {
                                wordBucketsByHeight.Add(word.Y, new List<OcrResult.Word> { word });
                            }
                        }

                        Console.Write("\n\n");
                    }

                    Console.Write("\n\n");
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

        private static MarketListing CreateMarketListing(List<OcrResult.Word> looseWordLine)
        {
            MarketListing marketListing = new MarketListing();

            foreach (OcrResult.Word word in looseWordLine)
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

    public class MarketListing
    {
        public string Name;
        public float Price;
        public int Available;
        public int Owned;
        public string Location;

        public override string ToString()
        {
            return $"{Name} ${Price} x{Available} xo{Owned} @{Location}";
        }
    }
}
