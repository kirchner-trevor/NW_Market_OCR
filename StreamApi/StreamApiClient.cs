using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace StreamApi
{
    public class StreamApiClient
    {
        public void Download(string url, string stream, string outputFileName, TimeSpan? startOffset = null, TimeSpan? duration = null, bool force = true)
        {
            string streamLinkFileName = Path.Combine("streamlink", "streamlink.bat");
            List<string> streamLinkArguments = new List<string> { url, stream };
            if (force)
            {
                streamLinkArguments.Add("-f");
            }
            if (outputFileName != null)
            {
                streamLinkArguments.Add($"-o {outputFileName}");
            }
            if (startOffset != null)
            {
                streamLinkArguments.Add($"--hls-start-offset {startOffset.Value.Hours.ToString().PadLeft(2, '0')}:{startOffset.Value.Minutes.ToString().PadLeft(2, '0')}:{startOffset.Value.Seconds.ToString().PadLeft(2, '0')}");
            }
            if (duration != null)
            {
                streamLinkArguments.Add($"--hls-duration {duration.Value.Hours.ToString().PadLeft(2, '0')}:{duration.Value.Minutes.ToString().PadLeft(2, '0')}:{duration.Value.Seconds.ToString().PadLeft(2, '0')}");
            }
            string streamLinkArgumentsString = string.Join(" ", streamLinkArguments);
            using (Process process = Process.Start(streamLinkFileName, streamLinkArgumentsString))
            {
                process.WaitForExit();

                //string result = process.StandardOutput.ReadToEnd();
                //Console.WriteLine(result);
            }
        }
    }
}
