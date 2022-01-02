using NW_Image_Analysis;
using NW_Market_Model;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace NW_Market_Collector
{
    public class VideoFileMarketImageGenerator : MarketImageGenerator
    {
        private readonly MarketImageDetector MarketImageDetector;
        private readonly VideoImageExtractor VideoImageExtractor;

        public VideoFileMarketImageGenerator(MarketImageDetector marketImageDetector, VideoImageExtractor videoImageExtractor)
        {
            MarketImageDetector = marketImageDetector;
            VideoImageExtractor = videoImageExtractor;
        }

        public bool TryCaptureMarketImage()
        {
            bool generatedMarketImage = false;
            string videoDirectory = Path.Combine(Directory.GetCurrentDirectory(), "videos");
            Directory.CreateDirectory(videoDirectory);
            string completedVideoDirectory = Path.Combine(Directory.GetCurrentDirectory(), "completedvideos");
            Directory.CreateDirectory(completedVideoDirectory);
            string[] videoPaths = Directory.GetFiles(videoDirectory, "*.mp4", SearchOption.AllDirectories);
            foreach (string videoPath in videoPaths)
            {
                WaitWhileFileIsLocked(videoPath, TimeSpan.FromSeconds(30));

                FileMetadata metadata = FileFormatMetadata.GetMetadataFromFile(videoPath);
                DateTime videoCaptureTime = metadata.CreationTime;

                string capturesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "captures");
                IEnumerable<string> marketImagePaths = VideoImageExtractor.Extract(videoPath, videoCaptureTime, capturesDirectory, $"{metadata.User ?? "unknown"}_{metadata.ServerId ?? "unknown"}_"); // TODO: Process 5 second intervals looking for market, then 0.5 second intervals until market isn't found again

                foreach (string marketImagePath in marketImagePaths)
                {
                    if (MarketImageDetector.ImageContainsTradingPost(marketImagePath))
                    {
                        string fileName = Path.GetFileName(marketImagePath);
                        string capturePath = Path.Combine(capturesDirectory, $"market_{fileName}");
                        File.Move(marketImagePath, capturePath, true);
                        generatedMarketImage = true;

                        VideoImageExtractor.SetSecondsPerIteration(1f / 2);
                    }
                    else
                    {
                        try
                        {
                            if (File.Exists(marketImagePath))
                            {
                                File.Delete(marketImagePath);
                            }
                        }
                        catch (Exception)
                        {
                            Trace.TraceError($"Failed to delete image not showing market at '{marketImagePath}.");
                        }

                        VideoImageExtractor.SetSecondsPerIteration(5f);
                    }
                }

                string[] previouslyCompletedVideos = Directory.GetFiles(completedVideoDirectory, "*", SearchOption.AllDirectories);
                if (previouslyCompletedVideos.Length >= 1)
                {
                    string oldestFile = previouslyCompletedVideos.OrderBy(_ => File.GetCreationTimeUtc(_)).FirstOrDefault();
                    File.Delete(oldestFile);
                }
                File.Move(videoPath, Path.Combine(completedVideoDirectory, Path.GetFileName(videoPath)), overwrite: true);
            }

            return generatedMarketImage;
        }

        private void WaitWhileFileIsLocked(string path, TimeSpan maximumWait)
        {
            DateTime startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalSeconds < maximumWait.TotalSeconds && IsFileLocked(path))
            {
                Trace.TraceInformation($"[{DateTime.UtcNow}] Waiting 1 second for locked file {path}.");
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        private bool IsFileLocked(string path)
        {
            try
            {
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }
        }
    }
}
