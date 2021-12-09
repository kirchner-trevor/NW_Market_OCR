using NW_Image_Analysis;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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
                FileMetadata metadata = FileFormatMetadata.GetMetadataFromFile(videoPath); // TODO: Get "user" from file path as well
                DateTime videoCaptureTime = metadata.CreationTime;

                string capturesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "captures");
                IEnumerable<string> marketImagePaths = VideoImageExtractor.Extract(videoPath, videoCaptureTime, capturesDirectory, $"{metadata.ServerId ?? "unknown"}_"); // TODO: Process 5 second intervals looking for market, then 0.5 second intervals until market isn't found again

                foreach (string marketImagePath in marketImagePaths)
                {
                    if (MarketImageDetector.ImageContainsBlueBanner(marketImagePath))
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
                if (previouslyCompletedVideos.Length >= 20)
                {
                    string oldestFile = previouslyCompletedVideos.OrderBy(_ => File.GetCreationTimeUtc(_)).FirstOrDefault();
                    File.Delete(oldestFile);
                }
                File.Move(videoPath, Path.Combine(completedVideoDirectory, Path.GetFileName(videoPath)), overwrite: true);
            }

            return generatedMarketImage;
        }
    }
}
