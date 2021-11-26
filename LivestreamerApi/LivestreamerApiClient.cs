using System;
using System.Diagnostics;
using System.IO;

namespace LivestreamerApi
{
    public class LivestreamerApiClient
    {
        public void Test()
        {
            using (Process process = Process.Start(Path.Combine("Livestreamer", "livestreamer.exe"), ""))
            {
                process.WaitForExit();

                string result = process.StandardOutput.ReadToEnd();
                Console.WriteLine(result);
            }
        }
    }
}
