using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Fastenshtein;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;

namespace NW_Market_Collector
{
    public class MarketImageUploader : IDisposable
    {
        private int NEW_TEXT_CONTENT_THRESHOLD = 100;

        private readonly Regex whitespaceRegex = new Regex(@"\s+");
        private TesseractEngine tesseractEngine = new TesseractEngine(Path.Combine(Directory.GetCurrentDirectory(), "tessdata"), "eng", EngineMode.Default);
        private HashSet<string> previousTextContents;

        private readonly ApplicationConfiguration Configuration;
        private readonly ConsoleHUDWriter ConsoleHUD;

        public MarketImageUploader(ApplicationConfiguration configuration, ConsoleHUDWriter consoleHUDWriter)
        {
            Configuration = configuration;
            ConsoleHUD = consoleHUDWriter;
        }

        public async Task ProcessMarketImages()
        {
            AmazonS3Client s3Client = new AmazonS3Client(Configuration.GetAccessKeyId(), Configuration.GetSecretAccessKey(), RegionEndpoint.USEast2);

            string capturesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "captures");
            Directory.CreateDirectory(capturesDirectory);

            ConsoleHUD.Server = Configuration.Server;

            List<string> filesToProcess = Directory.GetFiles(capturesDirectory, $"market_*.png").ToList();
            ConsoleHUD.TotalItemsSeen += filesToProcess.Count;
            foreach (string filePath in filesToProcess)
            {
                ConsoleHUD.Progress = 0;
                await UploadNwMarketImage(filePath, s3Client);
                Trace.WriteLine($"Removing market image...");
                File.Delete(filePath);
                ConsoleHUD.TotalItemsProcessed++;
            }
        }

        private async Task UploadNwMarketImage(string path, AmazonS3Client s3Client)
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
                    Key = Configuration.Server + "/" + Guid.NewGuid().ToString("D"),
                };
                putRequest.StreamTransferProgress += new EventHandler<StreamTransferProgressArgs>(UpdateProgress);
                putRequest.Metadata.Add("timestamp", fileCreationTime.ToString("o"));
                putRequest.Metadata.Add("textcontent", textContent);
                putRequest.Metadata.Add("user", Configuration.User);

                Trace.WriteLine($"Found new market image '{processedPath}' with text '{textContent.Substring(0, Math.Min(textContent.Length, 20))}...', uploading...");
                await s3Client.PutObjectAsync(putRequest);
            }
            else
            {
                ConsoleHUD.LatestTextBlob = "(Old) " + textContent;
                Trace.WriteLine($"Skipping upload of existing text content...");
            }
        }

        private string CleanInputImage(string path)
        {
            string fileDirectory = Path.GetDirectoryName(path);
            string processedPath = Path.Combine(fileDirectory, "processed.png");

            Trace.WriteLine($"Cleaning image at '{path}'...");

            using (Bitmap original = new Bitmap(path))
            {
                Rectangle marketArea = Configuration.GetMarketArea();
                if (!Configuration.IsCustomMarketArea())
                {
                    marketArea = Configuration.GetScreenAdjustmentsForWindow(original.Width, original.Height).Adjust(marketArea);
                }

                using (Bitmap cropped = original.Clone(marketArea, PixelFormat.Format32bppArgb))
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
            }

            Trace.WriteLine($"Cleaned image saved to '{processedPath}'");
            return processedPath;
        }

        private string ExtractTextContent(string processedPath)
        {
            using (Pix image = Pix.LoadFromFile(processedPath))
            {
                using (Page page = tesseractEngine.Process(image))
                {
                    return page.GetText();
                }
            }
        }

        private string CleanTextContent(string textContent)
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

        private void UpdateProgress(object sender, StreamTransferProgressArgs e)
        {
            ConsoleHUD.Progress = e.PercentDone;
        }

        private bool IsTextContentNew(string textContent, int minDistanceForNew)
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

        private bool disposedValue;

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
