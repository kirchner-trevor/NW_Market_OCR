using System;

namespace NW_Market_Collector
{
    public class ConsoleHUDWriter
    {
        private string collectorStatus;
        public string CollectorStatus { get { return collectorStatus; } set { collectorStatus = value; Update(); } }

        private string processorStatus;
        public string ProcessorStatus { get { return processorStatus; } set { processorStatus = value; Update(); } }

        private int progress;
        public int Progress { get { return progress; } set { progress = value; Update(); } }

        private int totalItemsSeen;
        public int TotalItemsSeen { get { return totalItemsSeen; } set { totalItemsSeen = value; Update(); } }

        private string latestTextBlob;
        public string LatestTextBlob { get { return latestTextBlob; } set { latestTextBlob = value; Update(); } }

        private int totalItemsProcessed;
        public int TotalItemsProcessed { get { return totalItemsProcessed; } set { totalItemsProcessed = value; Update(); } }


        private string server;
        public string Server { get { return server; } set { server = value; Update(); } }

        private int step = 0;

        private void Update()
        {
            if (step == 0)
            {
                Console.Clear();
            }
            else
            {
                Console.SetCursorPosition(0, 0);
            }

            Console.Write(
                $"{Rotator()} NW Market Collector - {server} {Rotator()}\n\n" +
                $"Collector Status: {collectorStatus}{new string(' ', 20)}\n" +
                $"Processor Status: {processorStatus}{new string(' ', 20)}\n" +
                $"Items: {totalItemsProcessed} / {totalItemsSeen}{new string(' ', 4)}\n" +
                $"Upload Progress: {progress}%{new string(' ', 4)}\n" +
                $"Latest Text Blob: {latestTextBlob?.Substring(0, Math.Min(latestTextBlob?.Length ?? 0, 50))}...\n"
            );
            step = DateTime.UtcNow.Second % 8;
        }

        private string Rotator()
        {
            switch (step)
            {
                case 0:
                    return "|";
                case 1:
                    return "/";
                case 2:
                    return "-";
                case 3: return "\\";
                case 4: return "|";
                case 5: return "/";
                case 6: return "-";
                case 7: return "\\";
            }
            return "X";
        }
    }
}
