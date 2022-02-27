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
            string streamLinkFileName = IsInstalled() ? "streamlink" : Path.Combine("streamlink", "streamlink.bat");
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
            ProcessStartInfo processStartInfo = new ProcessStartInfo(streamLinkFileName, streamLinkArgumentsString);
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardOutput = true;
            using (Process process = Process.Start(processStartInfo))
            {
                process.ErrorDataReceived += Process_ErrorDataReceived;
                process.WaitForExit();
                process.ErrorDataReceived -= Process_ErrorDataReceived;

                if (process.ExitCode != 0)
                {
                    throw new Exception($"StreamApiClient errored when executing: '{streamLinkFileName} {streamLinkArgumentsString}'.");
                }
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Trace.TraceError($"[{DateTime.UtcNow}] {e.Data}");
        }

        private bool IsInstalled()
        {
            try
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo("streamlink");
                processStartInfo.RedirectStandardError = false;
                processStartInfo.RedirectStandardOutput = false;
                using (Process process = Process.Start(processStartInfo))
                {
                    process.WaitForExit();

                    return process.ExitCode == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
