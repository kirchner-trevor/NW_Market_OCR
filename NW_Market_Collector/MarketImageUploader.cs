using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Fastenshtein;
using NW_Image_Analysis;
using NW_Market_Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NW_Market_Collector
{
    // TODO : Bundle into an archive of X files and a manifest json before uploading to reduce the number of upload requests
    public class MarketImageUploader
    {
        private int NEW_TEXT_CONTENT_THRESHOLD = 100;
        private readonly Regex whitespaceRegex = new Regex(@"\s+");
        private HashSet<string> previousTextContents;

        private readonly ApplicationConfiguration Configuration;
        private readonly ConsoleHUDWriter ConsoleHUD;
        private readonly MarketImageDetector MarketImageDetector;
        private readonly MarketImageWriteOnlyRepository MarketImageWriteOnlyRepository;

        public MarketImageUploader(ApplicationConfiguration configuration, ConsoleHUDWriter consoleHUDWriter, MarketImageDetector marketImageDetector, MarketImageWriteOnlyRepository marketImageWriteOnlyRepository)
        {
            Configuration = configuration;
            ConsoleHUD = consoleHUDWriter;
            MarketImageDetector = marketImageDetector;
            MarketImageWriteOnlyRepository = marketImageWriteOnlyRepository;
        }

        public async Task ProcessMarketImages()
        {
            string capturesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "captures");
            Directory.CreateDirectory(capturesDirectory);

            ConsoleHUD.Server = Configuration.Server;

            List<string> filesToProcess = Directory.GetFiles(capturesDirectory, $"market_*.png").ToList();
            ConsoleHUD.TotalItemsSeen += filesToProcess.Count;
            foreach (string filePath in filesToProcess)
            {
                ConsoleHUD.Progress = 0;
                await UploadNwMarketImage(filePath);
                Trace.WriteLine($"Removing market image...");
                File.Delete(filePath);
                ConsoleHUD.TotalItemsProcessed++;
            }
        }

        private async Task UploadNwMarketImage(string path)
        {
            ConsoleHUD.ProcessorStatus = "Processing Market Data";

            string processedPath = MarketImageDetector.CleanInputImage(path, Configuration.IsCustomMarketArea() ? (Rectangle)Configuration.CustomMarketArea : null);
            string textContent = CleanTextContent(MarketImageDetector.ExtractTextContent(processedPath));

            if (IsTextContentNew(textContent, NEW_TEXT_CONTENT_THRESHOLD))
            {
                ConsoleHUD.LatestTextBlob = "(New) " + textContent;

                Trace.WriteLine($"Found new market image '{processedPath}' with text '{textContent.Substring(0, Math.Min(textContent.Length, 20))}...', uploading...");
                await MarketImageWriteOnlyRepository.Add(new FileSystemMarketImage(path)
                {
                    Url = processedPath,
                }, new Dictionary<string, string>
                {
                    ["textcontent"] = textContent
                });
            }
            else
            {
                ConsoleHUD.LatestTextBlob = "(Old) " + textContent;
                Trace.WriteLine($"Skipping upload of existing text content...");
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
    }
}
