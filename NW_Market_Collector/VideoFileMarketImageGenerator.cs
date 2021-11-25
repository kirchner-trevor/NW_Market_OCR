using OpenCvSharp;
using System;
using System.Diagnostics;
using System.IO;

namespace NW_Market_Collector
{
    public class VideoFileMarketImageGenerator : MarketImageGenerator
    {
        private readonly MarketImageDetector MarketImageDetector;

        public VideoFileMarketImageGenerator(MarketImageDetector marketImageDetector)
        {
            MarketImageDetector = marketImageDetector;
        }

        public bool TryCaptureMarketImage()
        {
            bool generatedMarketImage = false;
            string videoDirectory = Path.Combine(Directory.GetCurrentDirectory(), "videos");
            string completedVideoDirectory = Path.Combine(Directory.GetCurrentDirectory(), "completedvideos");
            Directory.CreateDirectory(completedVideoDirectory);
            string[] videoPaths = Directory.GetFiles(videoDirectory, "*.mp4");
            foreach (string videoPath in videoPaths)
            {
                // TODO: Replace with the time when the video was created
                DateTime videoCaptureTime = DateTime.UtcNow;

                using (VideoCapture video = new VideoCapture(videoPath))
                {
                    Mat videoFrame = new Mat();
                    while (video.Read(videoFrame))
                    {

                        string path = Path.Combine(Directory.GetCurrentDirectory(), "captures");
                        Directory.CreateDirectory(path);

                        path = Path.Combine(path, $"new.png");

                        if (videoFrame.ImWrite(path))
                        {
                            if (MarketImageDetector.ImageContainsBlueBanner(path))
                            {
                                DateTime timeOfFrame = videoCaptureTime + TimeSpan.FromMilliseconds(video.PosMsec);
                                string capturePath = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "captures", $"market_{timeOfFrame.ToFileTimeUtc()}.png"));
                                File.Move(path, capturePath, true);
                                generatedMarketImage = true;
                            }
                        }
                        else
                        {
                            Trace.WriteLine($"Failed to write frame {video.PosFrames} to file {path}.");
                        }

                        video.PosMsec += (int)TimeSpan.FromSeconds(1).TotalMilliseconds;
                    }
                }

                File.Move(videoPath, Path.Combine(completedVideoDirectory, Path.GetFileName(videoPath)));
            }

            return generatedMarketImage;
        }
    }
}
