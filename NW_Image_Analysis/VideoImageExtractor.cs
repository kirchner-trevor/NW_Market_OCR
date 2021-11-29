using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace NW_Image_Analysis
{
    public class VideoImageExtractor
    {
        public string[] Extract(string videoPath, DateTime videoCaptureTime, string outputDirectory, string filePrefix = "")
        {
            Directory.CreateDirectory(outputDirectory);

            List<string> extractedImagePaths = new List<string>();

            using (VideoCapture video = new VideoCapture(videoPath))
            {
                int framesPerImage = (int)(video.Fps * TimeSpan.FromSeconds(1).TotalSeconds);
                Mat videoFrame = new Mat();
                int currentFrame = 1;
                while (currentFrame <= video.FrameCount && video.Read(videoFrame))
                {
                    DateTime timeOfFrame = videoCaptureTime + TimeSpan.FromMilliseconds(video.PosMsec);
                    string outputPath = Path.Combine(outputDirectory, $"{filePrefix}{timeOfFrame.ToFileTimeUtc()}.png");

                    if (videoFrame.ImWrite(outputPath))
                    {
                        extractedImagePaths.Add(outputPath);
                    }
                    else
                    {
                        Trace.WriteLine($"Failed to write frame {video.PosFrames} to file {outputPath}.");
                    }

                    currentFrame += framesPerImage;
                    video.PosFrames = currentFrame;
                }
            }

            return extractedImagePaths.ToArray();
        }
    }
}
