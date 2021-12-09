using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace NW_Image_Analysis
{
    public class VideoImageExtractor
    {
        private double throughputPerSecond = 1d / 1;
        private double secondsPerIteration = 1d;

        public VideoImageExtractor()
        {
            //IntPtr zero = IntPtr.Zero;
            //OpenCvSharp.Internal.NativeMethods.redirectError(OpenCvSharp.Internal.NativeMethods.ErrorHandlerIgnorance, zero, ref zero);
        }

        public void SetTargetThroughput(double throughputPerSecond)
        {
            this.throughputPerSecond = throughputPerSecond;
        }

        public void SetSecondsPerIteration(double secondsPerIteration)
        {
            this.secondsPerIteration = secondsPerIteration;
        }

        public IEnumerable<string> Extract(string videoPath, DateTime videoCaptureTime, string outputDirectory, string filePrefix = "")
        {
            Directory.CreateDirectory(outputDirectory);

            using (VideoCapture video = new VideoCapture(videoPath))
            {
                int framesPerImage = (int)(video.Fps * TimeSpan.FromSeconds(1).TotalSeconds);
                Mat videoFrame = new Mat();
                int currentFrame = 1;

                double processingMillisecondsBehind = 0;
                DateTime processingStartTime = DateTime.UtcNow;

                while (currentFrame <= video.FrameCount && video.Read(videoFrame))
                {
                    processingMillisecondsBehind = SleepTowardTargetThroughput(processingStartTime, processingMillisecondsBehind, throughputPerSecond);
                    processingStartTime = DateTime.UtcNow;

                    DateTime timeOfFrame = videoCaptureTime + TimeSpan.FromMilliseconds(video.PosMsec);
                    string outputPath = Path.Combine(outputDirectory, $"{filePrefix}{timeOfFrame.ToFileTimeUtc()}.png");

                    if (videoFrame.ImWrite(outputPath))
                    {
                        yield return outputPath;
                    }
                    else
                    {
                        Trace.WriteLine($"Failed to write frame {video.PosFrames} to file {outputPath}.");
                    }

                    currentFrame += (int)(framesPerImage * secondsPerIteration);
                    video.PosFrames = currentFrame;
                }
            }
        }

        private static double SleepTowardTargetThroughput(DateTime processingStartTime, double processingMillisecondsBehind, double targetThroughputPerSecond)
        {
            TimeSpan timeSinceStart = DateTime.UtcNow - processingStartTime;
            TimeSpan newTimeToWait = TimeSpan.FromSeconds(1d / targetThroughputPerSecond) - timeSinceStart;
            double currentProcessingDelay = newTimeToWait.TotalMilliseconds + processingMillisecondsBehind;
            processingMillisecondsBehind = currentProcessingDelay < 0 ? currentProcessingDelay : 0;
            int msToWait = (int)Math.Max(0, currentProcessingDelay);
            Trace.TraceInformation($"[{DateTime.UtcNow}] Waiting for {msToWait} ms to acheive throughput of {targetThroughputPerSecond}. Currently {processingMillisecondsBehind} ms behind.");
            Thread.Sleep(msToWait);
            return processingMillisecondsBehind;
        }
    }
}
