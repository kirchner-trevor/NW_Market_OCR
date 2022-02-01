using NW_Image_Analysis;
using NW_Market_Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StreamApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Api.Helix.Models.Videos.GetVideos;

namespace NW_Stream_Collector
{
    public class Program
    {
        public const string DATA_DIRECTORY = @"C:\Users\kirch\source\repos\NW_Market_OCR\Data";

        static async Task Main(string[] args)
        {
            string clientId = args.Length > 0 ? args[0] : null;
            string secret = args.Length > 1 ? args[1] : null;
            Trace.Listeners.Add(new ConsoleTraceListener());

            TwitchAPI twitchApi = new TwitchAPI();
            twitchApi.Settings.ClientId = clientId;
            twitchApi.Settings.Secret = secret;

            StreamApiClient streamApiClient = new StreamApiClient();
            ConfigurationDatabase configurationDatabase = new ConfigurationDatabase(DATA_DIRECTORY);
            OcrEngine ocrEngine = new TesseractOcrEngine();
            MarketImageDetector marketImageDetector = new MarketImageDetector(ocrEngine);
            VideoImageExtractor videoImageExtractor = new VideoImageExtractor();
            StreamCollectorStatsRepository streamCollectorStatsRepository = new StreamCollectorStatsRepository(DATA_DIRECTORY);
            TwitchStreamMetadata twitchStreamMetadata = new TwitchStreamMetadata(configurationDatabase);
            StreamCollector streamCollector = new StreamCollector(twitchApi, streamApiClient, configurationDatabase, marketImageDetector, videoImageExtractor, streamCollectorStatsRepository, twitchStreamMetadata);

            try
            {
                await streamCollector.Run();
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
            }
        }
    }

    public class StreamCollector
    {
        private const string NEW_WORLD_GAME_ID = "493597";
        private const string IMAGE_OUTPUT_DIRECTORY = "images";
        private const string VIDEO_OUTPUT_DIRECTORY = "videos";
        private const string FULL_MARKET_VIDEO_OUTPUT_DIRECTORY = @"C:\Users\kirch\source\repos\NW_Market_OCR\NW_Market_Collector\bin\Debug\net5.0\videos";
        private static readonly string PROCESSED_VIDEOS_PATH = Path.Combine(Program.DATA_DIRECTORY, "processedVideos.json");
        private static readonly string AUTHOR_INFO_PATH = Path.Combine(Program.DATA_DIRECTORY, "authorInfo.json");
        private static readonly string MARKET_SEGMENTS_PATH = Path.Combine(Program.DATA_DIRECTORY, "testMarketSegments.json");
        private static readonly string TWITCH_USERS_TO_FOLLOW_PATH = Path.Combine(Program.DATA_DIRECTORY, "followedTwitchUsers.json");
        private const int VIDEO_GET_FAILURES_MAX = 5;
        private const int VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS = 5;
        private const int MINIMUM_RESOLUTION_FOR_PROCESSING = 1080;
        private const long DAILY_DOWNLOAD_BYTE_LIMIT = 20_000_000_000; // 20gb
        private static readonly TimeSpan OLDEST_PROCESSING_DATE = TimeSpan.FromDays(-8);
        private static readonly TimeSpan OLDEST_AUTHORINFO_DATE = TimeSpan.FromDays(-30);
        private readonly TwitchAPI twitchApi;
        private readonly StreamApiClient livestreamerApiClient;
        private readonly ConfigurationDatabase configurationDatabase;
        private readonly MarketImageDetector marketImageDetector;
        private readonly VideoImageExtractor videoImageExtractor;
        private readonly StreamCollectorStatsRepository streamCollectorStatsRepository;
        private readonly TwitchStreamMetadata twitchStreamMetadata;

        public StreamCollector(TwitchAPI twitchApi, StreamApiClient livestreamerApiClient, ConfigurationDatabase configurationDatabase, MarketImageDetector marketImageDetector, VideoImageExtractor videoImageExtractor, StreamCollectorStatsRepository streamCollectorStatsRepository, TwitchStreamMetadata twitchStreamMetadata)
        {
            this.twitchApi = twitchApi;
            this.livestreamerApiClient = livestreamerApiClient;
            this.configurationDatabase = configurationDatabase;
            this.marketImageDetector = marketImageDetector;
            this.videoImageExtractor = videoImageExtractor;
            this.streamCollectorStatsRepository = streamCollectorStatsRepository;
            this.twitchStreamMetadata = twitchStreamMetadata;
        }

        public async Task Run()
        {
            List<string> twitchUsersToFollow = new List<string>();
            if (File.Exists(TWITCH_USERS_TO_FOLLOW_PATH))
            {
                twitchUsersToFollow = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(TWITCH_USERS_TO_FOLLOW_PATH));
            }

            List<string> processedVideos = new List<string>();
            if (File.Exists(PROCESSED_VIDEOS_PATH))
            {
                processedVideos = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(PROCESSED_VIDEOS_PATH));
            }

            Dictionary<string, AuthorInfo> authorInfos = new Dictionary<string, AuthorInfo>();
            if (File.Exists(PROCESSED_VIDEOS_PATH))
            {
                authorInfos = JsonSerializer.Deserialize<Dictionary<string, AuthorInfo>>(File.ReadAllText(AUTHOR_INFO_PATH));
            }

            List<TimeSegment> segmentsContainingMarket = new List<TimeSegment>();
            if (File.Exists(MARKET_SEGMENTS_PATH))
            {
                segmentsContainingMarket = JsonSerializer.Deserialize<List<TimeSegment>>(File.ReadAllText(MARKET_SEGMENTS_PATH));
            }

            streamCollectorStatsRepository.LoadDatabaseFromDisk();

            while (true)
            {
                StreamCollectorStats streamCollectorStats = new StreamCollectorStats();
                streamCollectorStatsRepository.Contents.Stats.Add(streamCollectorStats);

                Trace.TraceInformation($"[{DateTime.UtcNow}] Fetching next set of videos.");

                List<Video> twitchVideos = new List<Video>();
                GetUsersResponse getUsersResponse = await twitchApi.Helix.Users.GetUsersAsync(logins: twitchUsersToFollow);
                foreach (string userId in getUsersResponse.Users.Select(_ => _.Id))
                {
                    List<Video> newUserVideos = await GetAllVideos(cursor => twitchApi.Helix.Videos.GetVideoAsync(userId: userId, after: cursor, language: "en", period: Period.Day, sort: VideoSort.Views, type: VideoType.Archive));
                    twitchVideos.AddRange(newUserVideos);
                }

                List<Video> newGlobalVideos = await GetAllVideos(cursor => twitchApi.Helix.Videos.GetVideoAsync(gameId: NEW_WORLD_GAME_ID, after: cursor, language: "en", period: Period.Day, sort: VideoSort.Views, type: VideoType.Archive));
                twitchVideos.AddRange(newGlobalVideos);

                streamCollectorStats.VideosFound += twitchVideos.Count;

                foreach (Video video in twitchVideos)
                {
                    WaitIfTotalBytesDownloadedIsAboveThreshold();
                    WaitIfNonActivePeriod();
                    FindMarketSegments(video, processedVideos, authorInfos, segmentsContainingMarket, streamCollectorStats);
                }

                Trace.TraceInformation($"Sleeping for 1 hour before processing again...");
                Thread.Sleep(TimeSpan.FromHours(1));
            }
        }

        private void WaitIfTotalBytesDownloadedIsAboveThreshold()
        {
            long totalBytesDownloadedInLastDay;
            do
            {
                totalBytesDownloadedInLastDay = GetTotalBytesDownloadedInLastDay();
                Trace.TraceInformation($"StreamCollector has downloaded {totalBytesDownloadedInLastDay / 1_000_000_000f} Gb in the last 24 hours!");
                if (totalBytesDownloadedInLastDay >= DAILY_DOWNLOAD_BYTE_LIMIT)
                {
                    Trace.TraceInformation($"Passed daily download limit of {DAILY_DOWNLOAD_BYTE_LIMIT / 1_000_000_000f} Gb. Sleeping for 1 hours.");
                    Thread.Sleep(TimeSpan.FromHours(1));
                }
            } while (totalBytesDownloadedInLastDay >= DAILY_DOWNLOAD_BYTE_LIMIT);
        }

        private void WaitIfNonActivePeriod()
        {
            const int MAX_HOUR = 12;
            do
            {
                if (DateTime.Now.Hour > MAX_HOUR)
                {
                    Trace.WriteLine($"Current hour {DateTime.Now.Hour} is not within the active time window of 0 - {MAX_HOUR}, sleeping for 1 hr!");
                    Thread.Sleep(TimeSpan.FromHours(1));
                }
            } while (DateTime.Now.Hour > MAX_HOUR);
        }

        private long GetTotalBytesDownloadedInLastDay()
        {
            long totalBytes = 0;
            double totalHoursSummed = 0;
            DateTime oneDayAgo = DateTime.UtcNow.AddDays(-1);
            foreach(StreamCollectorStats stats in streamCollectorStatsRepository.Contents.Stats.Where(_ => _.To >= oneDayAgo).OrderByDescending(_ => _.To))
            {
                TimeSpan duration = stats.To - stats.From;

                if (duration.TotalHours == 0)
                {
                    continue;
                }

                if (totalHoursSummed + duration.TotalHours >= 24f)
                {
                    double bytesPerHour = stats.VideoBytesDownloaded / duration.TotalHours;
                    double applicableDurationHours = totalHoursSummed + duration.TotalHours - 24f;
                    totalHoursSummed += applicableDurationHours; 
                    totalBytes += (long)(applicableDurationHours * bytesPerHour);
                    break;
                }
                else
                {
                    totalHoursSummed += duration.TotalHours;
                    totalBytes += stats.VideoBytesDownloaded;
                }
            }
            return totalBytes;
        }

        // For each video get the duration and then
        // Download 1 second snippets every 5 minutes
        private void FindMarketSegments(Video video, List<string> processedVideos, Dictionary<string, AuthorInfo> authorInfos, List<TimeSegment> segmentsContainingMarket, StreamCollectorStats streamCollectorStats)
        {
            Trace.TraceInformation($"Started processing video {video.Id}");
            streamCollectorStats.VideosThatStartedProcessing += 1;

            if (DateTime.Parse(video.CreatedAt).ToUniversalTime() < (DateTime.UtcNow + OLDEST_PROCESSING_DATE))
            {
                Trace.TraceInformation($"Too old video {video.Id} was created on {DateTime.Parse(video.CreatedAt).ToUniversalTime()}. Skipping.");
                return;
            }
            streamCollectorStats.VideosRecentlyCreated += 1;

            // We've already seen this video
            if (processedVideos.Contains(video.Id))
            {
                Trace.TraceInformation($"Already processed video {video.Id}. Skipping.");
                return;
            }
            streamCollectorStats.VideosNewToProcess += 1;

            // Filter videos to only those with server names in them
            string serverId = twitchStreamMetadata.TryExtractServer(video.Title + " " + video.Description, out string server) ? server : null;
            AuthorInfo authorInfo = null;
            if (authorInfos.TryGetValue($"twitch|{video.UserId}", out authorInfo) && authorInfo.LastUpdated < (DateTime.UtcNow + OLDEST_AUTHORINFO_DATE))
            {
                if (serverId == null)
                {
                    serverId = authorInfo.ServerId;
                }
                else if (serverId != authorInfo.ServerId)
                {
                    Trace.TraceWarning($"Author {authorInfo.Id} has videos on multiple servers: {serverId}, {authorInfo.ServerId}. Preferring server in video title.");
                    authorInfo.ServerId = serverId;
                    authorInfo.LastUpdated = DateTime.Parse(video.CreatedAt).ToUniversalTime();
                    File.WriteAllText(AUTHOR_INFO_PATH, JsonSerializer.Serialize(authorInfos));

                    streamCollectorStats.VideosWithServerInfoInTitle += 1;
                }
                streamCollectorStats.VideosWithServerInfoOnRecord += 1;
            }
            else if (serverId != null)
            {
                string authorId = $"twitch|{video.UserId}";
                if (authorInfo == null)
                {
                    authorInfo = new AuthorInfo
                    {
                        Id = authorId,
                        ServerId = serverId,
                        LastUpdated = DateTime.Parse(video.CreatedAt).ToUniversalTime(),
                    };
                    authorInfos.Add(authorId, authorInfo);
                }
                else
                {
                    Trace.TraceWarning($"Author {authorInfo.Id} with expired entry updated with new server: {serverId} was {authorInfo.ServerId}.");
                    authorInfo.ServerId = serverId;
                    authorInfo.LastUpdated = DateTime.Parse(video.CreatedAt).ToUniversalTime();
                }
                File.WriteAllText(AUTHOR_INFO_PATH, JsonSerializer.Serialize(authorInfos));
                streamCollectorStats.VideosWithServerInfoInTitle += 1;
            }
            else
            {
                Trace.TraceInformation($"Could not find server for video '{video.Id}' in '{video.Title}{video.Description}', and there is no recent author information for 'twitch|{video.UserId}'. Skipping.");
                return;
                //serverId = "unknown";
                //Trace.TraceInformation($"Could not find server for video '{video.Id}' in '{video.Title}{video.Description}', and there is no author information for 'twitch|{video.UserId}'. Processing as '{serverId}' server.");
            }
            streamCollectorStats.VideosWithServerInfo += 1;

            TimeSegment previousMarketSegment = null;
            TimeSegment currentMarketSegment = null;

            int videoGetFailures = 0;
            int durationMinutes = (int)ParseDuration(video.Duration).TotalMinutes;
            streamCollectorStats.VideoMinutesSearched += durationMinutes;
            streamCollectorStats.VideosWithHighResolution += 1;
            DateTime videoStartTime = DateTime.Parse(video.CreatedAt).ToUniversalTime();
            for (int startTimeMinutes = 0; startTimeMinutes <= durationMinutes; startTimeMinutes += VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS)
            {
                DateTime snippetStartTime = videoStartTime + TimeSpan.FromMinutes(startTimeMinutes);
                long fileTime = snippetStartTime.ToFileTimeUtc();

                string videoDirectory = Path.Combine(VIDEO_OUTPUT_DIRECTORY, serverId);
                Directory.CreateDirectory(videoDirectory);

                string imageDirectory = Path.Combine(IMAGE_OUTPUT_DIRECTORY, serverId);
                Directory.CreateDirectory(imageDirectory);
                string videoFileNamePrefix = GetVideoPrefix(video.UserLogin, video.Id, serverId);
                string videoPath = Path.Combine(videoDirectory, GetVideoFileName(videoFileNamePrefix, fileTime));
                bool downloadedVideo = false;
                try
                {
                    // TODO: Lower the quality of the snippet downloads since its just for detecting the blue banner (might need to adjust banner detection logic to support this)
                    Trace.TraceInformation($"Downloading {TimeSpan.FromSeconds(1)} from {video.Id} at {TimeSpan.FromMinutes(startTimeMinutes)}.");
                    livestreamerApiClient.Download($"twitch.tv/videos/{video.Id}", "best", videoPath, TimeSpan.FromMinutes(startTimeMinutes), TimeSpan.FromSeconds(1));
                    downloadedVideo = true;
                    videoGetFailures = 0;
                    streamCollectorStats.VideoBytesDownloaded += new FileInfo(videoPath).Length;
                }
                catch (Exception)
                {
                    videoGetFailures++;

                    if (videoGetFailures < VIDEO_GET_FAILURES_MAX)
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }

                if (downloadedVideo)
                {
                    Trace.TraceInformation($"[{DateTime.UtcNow}] Extracting frames from video '{videoPath}'.");

                    IEnumerable<string> imagePaths = videoImageExtractor.Extract(videoPath, snippetStartTime, imageDirectory, videoFileNamePrefix);

                    if (imagePaths.Any())
                    {
                        using (Image<Rgba32> firstImage = Image.Load<Rgba32>(imagePaths.First()))
                        {
                            if (firstImage.Height < MINIMUM_RESOLUTION_FOR_PROCESSING)
                            {
                                streamCollectorStats.VideosWithHighResolution -= 1;
                                Trace.TraceWarning($"Video {video.Id} has a vertical resolution of {firstImage.Height} which is too small to process, skipping video.");
                                break;
                            }
                        }
                    }

                    foreach (string imagePath in imagePaths)
                    {
                        if (marketImageDetector.ImageContainsTradingPost(imagePath))
                        {
                            Trace.TraceInformation($"Image {imagePath} contained a blue banner.");

                            TimeSpan from = snippetStartTime - videoStartTime;
                            currentMarketSegment = new TimeSegment
                            {
                                VideoId = video.Id,
                                From = from,
                                To = from + TimeSpan.FromMinutes(VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS),
                            };

                            // If the previous segment did contain the market, extend that segment
                            if (previousMarketSegment != null && Math.Abs(previousMarketSegment.ToMinutes - currentMarketSegment.FromMinutes) <= VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS)
                            {
                                previousMarketSegment.ToMinutes = currentMarketSegment.ToMinutes;
                                // Don't update the previous market segment since the previous was extended to be the current
                            }
                            // If the previous segment didn't contain the market, or overlap with this one, create a new one
                            else
                            {
                                segmentsContainingMarket.Add(currentMarketSegment);
                                previousMarketSegment = currentMarketSegment;
                            }

                            // Since we found a market image in this segment already, stop looking at this segment
                            break;
                        }

                        try
                        {
                            Trace.TraceInformation($"Deleting processed image at '{imagePath}'");
                            if (File.Exists(imagePath))
                            {
                                File.Delete(imagePath);
                            }
                        }
                        catch (Exception e)
                        {
                            Trace.TraceError($"Failed to delete image '{imagePath}'. Skipping deletion.\n{e.Message}");
                        }
                    }
                }

                try
                {
                    Trace.TraceInformation($"Deleting processed video at '{videoPath}'");
                    if (File.Exists(videoPath))
                    {
                        File.Delete(videoPath);
                    }
                }
                catch (Exception e)
                {
                    Trace.TraceError($"Failed to delete video '{videoPath}'. Skipping deletion.\n{e.Message}");
                }
            }

            // Re-download the full segments of video when the market was visible
            if (segmentsContainingMarket.Any(_ => _.VideoId == video.Id))
            {
                streamCollectorStats.VideosShowingMarket += 1;
                foreach (TimeSegment marketTimeSegment in segmentsContainingMarket.Where(_ => _.VideoId == video.Id))
                {
                    string videoSegmentPath = DownloadSegmentToCollectorFolder(video, serverId, videoStartTime, marketTimeSegment);
                    streamCollectorStats.VideoSegmentsShowingMarket += 1;
                    streamCollectorStats.VideoMinutesShowingMarket += (marketTimeSegment.ToMinutes - marketTimeSegment.FromMinutes);
                    streamCollectorStats.VideoBytesDownloaded += new FileInfo(videoSegmentPath).Length;
                }
                segmentsContainingMarket.RemoveAll(_ => _.VideoId == video.Id);
            }

            Trace.TraceInformation($"Finished processing video {video.Id}.");
            processedVideos.Add(video.Id);
            File.WriteAllText(PROCESSED_VIDEOS_PATH, JsonSerializer.Serialize(processedVideos));

            File.WriteAllText(MARKET_SEGMENTS_PATH, JsonSerializer.Serialize(segmentsContainingMarket));

            streamCollectorStats.To = DateTime.UtcNow;
            streamCollectorStatsRepository.SaveDatabaseToDisk();
        }

        private async Task<List<Video>> GetAllVideos(Func<string, Task<GetVideosResponse>> getVideos, int maxVideos = 520)
        {
            List<Video> twitchVideos = new List<Video>();

            bool hasMoreVideos = true;
            string cursor = null;
            int twitchApiCallFailures = 0;
            while (hasMoreVideos && twitchApiCallFailures < VIDEO_GET_FAILURES_MAX && twitchVideos.Count < maxVideos)
            {
                try
                {
                    GetVideosResponse getVideosResponse = await getVideos(cursor);

                    twitchVideos.AddRange(getVideosResponse.Videos);
                    cursor = getVideosResponse.Pagination.Cursor;
                    hasMoreVideos = cursor != null;
                }
                catch (Exception)
                {
                    twitchApiCallFailures++;

                    int sleepTimeMs = (1 + new Random().Next((int)Math.Pow(2, twitchApiCallFailures))) * 1000;
                    Trace.TraceInformation($"Twitch API call failed ({twitchApiCallFailures}), sleeping for {sleepTimeMs} ms then retrying.");
                }

                if (cursor == null)
                {
                    Trace.TraceWarning($"Found null cursor after processing cursor '{cursor}'. This means we've hit our page limit of 500.");
                    break;
                }
            }

            return twitchVideos;
        }

        private string DownloadSegmentToCollectorFolder(Video video, string serverId, DateTime videoStartTime, TimeSegment marketTimeSegment)
        {
            DateTime segmentStartTime = videoStartTime + marketTimeSegment.From - TimeSpan.FromMinutes(VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS);
            long fileTime = segmentStartTime.ToFileTimeUtc();

            // Copy video segments over for the Market_Collector to run on
            string videoFileName = GetVideoFileName(GetVideoPrefix(video.UserLogin, video.Id, serverId), fileTime);
            string videoPath = Path.Combine(FULL_MARKET_VIDEO_OUTPUT_DIRECTORY, serverId, videoFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(videoPath));
            string downloadPath = Path.Combine(VIDEO_OUTPUT_DIRECTORY, serverId, videoFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(downloadPath));

            int fullMarketVideoGetFailures = 0;
            bool downloadedVideo = false;
            while (!downloadedVideo)
            {
                try
                {
                    int durationExtensionMinutes = 0;

                    // Start one interval sooner than the first market image
                    TimeSpan startOffset = marketTimeSegment.From;
                    if (startOffset.TotalMinutes >= VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS)
                    {
                        startOffset -= TimeSpan.FromMinutes(VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS);
                        durationExtensionMinutes += VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS;
                    }

                    // End at the end of the interval that saw the last market image
                    TimeSpan duration = marketTimeSegment.To - marketTimeSegment.From;
                    if ((startOffset + duration + TimeSpan.FromMinutes(VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS)) <= ParseDuration(video.Duration))
                    {
                        durationExtensionMinutes += VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS;
                    }

                    duration += TimeSpan.FromMinutes(durationExtensionMinutes);

                    Trace.TraceInformation($"Downloading full market video {video.Id} from {startOffset} to {duration} to '{downloadPath}'.");
                    livestreamerApiClient.Download($"twitch.tv/videos/{video.Id}", "best", downloadPath, startOffset, duration);
                    downloadedVideo = true;
                }
                catch (Exception)
                {
                    fullMarketVideoGetFailures++;

                    if (fullMarketVideoGetFailures >= VIDEO_GET_FAILURES_MAX)
                    {
                        Trace.TraceError($"Failed to download full market video {video.Id} {VIDEO_GET_FAILURES_MAX} times. Skipping.");
                        break;
                    }
                }
            }

            if (downloadedVideo)
            {
                Trace.TraceInformation($"Moving downloaded full market video to '{videoPath}'.");

                if (!File.Exists(videoPath))
                {
                    File.Move(downloadPath, videoPath);

                    return videoPath;
                }
                else
                {
                    Trace.TraceInformation($"File already exists at '{videoPath}', likely overlapping segments. Skipping.");
                }
            }

            return downloadPath;
        }

        private static string GetVideoPrefix(string userLogin, string videoId, string serverId)
        {
            return $"{userLogin}-{videoId}_{serverId}_";
        }

        private static string GetVideoFileName(string videoFileNamePrefix, long fileTime)
        {
            return $"{videoFileNamePrefix}{fileTime}.mp4";
        }

        private class TimeSegment
        {
            public string VideoId { get; set; }
            public int FromMinutes { get; set; }
            public int ToMinutes { get; set; }
            [JsonIgnore]
            public TimeSpan From
            {
                get
                {
                    return TimeSpan.FromMinutes(FromMinutes);
                }
                set
                {
                    FromMinutes = (int)Math.Floor(value.TotalMinutes);
                }
            }
            [JsonIgnore]
            public TimeSpan To
            {
                get
                {
                    return TimeSpan.FromMinutes(ToMinutes);
                }
                set
                {
                    ToMinutes = (int)Math.Ceiling(value.TotalMinutes);
                }
            }
        }

        private TimeSpan ParseDuration(string duration) //4h13m21s
        {
            int seconds = 0;
            int minutes = 0;
            int hours = 0;
            if (duration.Contains("s"))
            {
                string[] secondParts = duration.Split("s");

                if (duration.Contains("m"))
                {
                    string[] minuteParts = secondParts[0].Split("m");
                    seconds = int.Parse(minuteParts[1]);

                    if (duration.Contains("h"))
                    {
                        string[] hoursParts = minuteParts[0].Split("h");
                        minutes = int.Parse(hoursParts[1]);

                        hours = int.Parse(hoursParts[0]);
                    }
                    else
                    {
                        minutes = int.Parse(minuteParts[0]);
                    }
                }
                else
                {
                    seconds = int.Parse(secondParts[0]);
                }
            }

            return new TimeSpan(hours, minutes, seconds);
        }
    }
}
