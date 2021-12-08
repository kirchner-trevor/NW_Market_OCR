using NW_Image_Analysis;
using NW_Market_Model;
using StreamApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Videos.GetVideos;

namespace NW_Stream_Collector
{
    class Program
    {
        private const string DATA_DIRECTORY = @"C:\Users\kirch\source\repos\NW_Market_OCR\Data";

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
            MarketImageDetector marketImageDetector = new MarketImageDetector();
            VideoImageExtractor videoImageExtractor = new VideoImageExtractor();
            StreamCollector streamCollector = new StreamCollector(twitchApi, streamApiClient, configurationDatabase, marketImageDetector, videoImageExtractor);

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
        private const string FULL_MARKET_VIDEO_OUTPUT_DIRECTORY = @"C:\Users\kirch\source\repos\NW_Market_OCR\NW_Market_Collector\bin\Debug\net5.0-windows\videos";
        private const string PROCESSED_VIDEOS_PATH = "processedVideos.json";
        private const string AUTHOR_INFO_PATH = "authorInfo.json";
        private const string MARKET_SEGMENTS_PATH = "testMarketSegments.json";
        private const int VIDEO_GET_FAILURES_MAX = 5;
        private const int VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS = 5;
        private readonly TwitchAPI twitchApi;
        private readonly StreamApiClient livestreamerApiClient;
        private readonly ConfigurationDatabase configurationDatabase;
        private readonly MarketImageDetector marketImageDetector;
        private readonly VideoImageExtractor videoImageExtractor;

        private readonly Dictionary<string, string> serverNamesLowercase;

        public StreamCollector(TwitchAPI twitchApi, StreamApiClient livestreamerApiClient, ConfigurationDatabase configurationDatabase, MarketImageDetector marketImageDetector, VideoImageExtractor videoImageExtractor)
        {
            this.twitchApi = twitchApi;
            this.livestreamerApiClient = livestreamerApiClient;
            this.configurationDatabase = configurationDatabase;
            this.marketImageDetector = marketImageDetector;
            this.videoImageExtractor = videoImageExtractor;

            serverNamesLowercase = this.configurationDatabase.Content.ServerList.Where(_ => _.Name != "Ophir").ToDictionary(_ => _.Name?.ToLowerInvariant(), _ => _.Id);
        }

        public async Task Run()
        {
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

            string cursor = null; // Start with the latest videos
            bool hasMoreVideos = true;
            DateTime oldestVideoCreateDate = DateTime.UtcNow;

            while (hasMoreVideos && oldestVideoCreateDate > DateTime.UtcNow.AddDays(-7))
            {
                // Find list of videos for NW
                twitchApi.Settings.AccessToken = twitchApi.Auth.GetAccessToken();
                GetVideosResponse getVideosResponse = await twitchApi.Helix.Videos.GetVideoAsync(gameId: NEW_WORLD_GAME_ID, after: cursor, language: "en", period: Period.Day, sort: VideoSort.Views, type: VideoType.Archive);

                // For each video get the duration and then
                // Download 1 second snippets every 5 minutes
                foreach (Video video in getVideosResponse.Videos)
                {
                    Trace.TraceInformation($"Started processing video {video.Id}");

                    // We've already seen this video
                    if (processedVideos.Contains(video.Id))
                    {
                        Trace.TraceInformation($"Already processed video {video.Id}. Skipping.");
                        continue;
                    }

                    // Filter videos to only those with server names in them
                    string serverId = TryExtractServer(video.Title + " " + video.Description, out string server) ? server : null;
                    AuthorInfo authorInfo = null;
                    if (authorInfos.TryGetValue($"twitch|{video.UserId}", out authorInfo))
                    {
                        if (serverId == null)
                        {
                            serverId = authorInfo.ServerId;
                        }
                        else if (serverId != authorInfo.ServerId)
                        {
                            Trace.TraceWarning($"Author {authorInfo.Id} has videos on multiple servers: {serverId}, {authorInfo.ServerId}. Preferring server in video title.");
                        }
                    }
                    else if (serverId != null)
                    {
                        string authorId = $"twitch|{video.UserId}";
                        authorInfo = new AuthorInfo
                        {
                            Id = authorId,
                            ServerId = serverId,
                        };
                        authorInfos.Add(authorId, authorInfo);
                        File.WriteAllText(AUTHOR_INFO_PATH, JsonSerializer.Serialize(authorInfos));
                    }
                    else
                    {
                        Trace.TraceInformation($"Could not find server for video '{video.Id}' in '{video.Title}{video.Description}', and there is no author information for 'twitch|{video.UserId}'. Skipping.");
                        continue;
                        //serverId = "unknown";
                        //Trace.TraceInformation($"Could not find server for video '{video.Id}' in '{video.Title}{video.Description}', and there is no author information for 'twitch|{video.UserId}'. Processing as '{serverId}' server.");
                    }

                    TimeSegment currentMarketSegment = null;

                    int videoGetFailures = 0;
                    int durationMinutes = (int)ParseDuration(video.Duration).TotalMinutes;
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
                            foreach (string imagePath in imagePaths)
                            {
                                if (marketImageDetector.ImageContainsBlueBanner(imagePath))
                                {
                                    Trace.TraceInformation($"Image {imagePath} contained a blue banner.");
                                    // If the previous segment didn't contain the market, create a new one
                                    if (currentMarketSegment == null)
                                    {
                                        TimeSpan from = snippetStartTime - videoStartTime;
                                        currentMarketSegment = new TimeSegment
                                        {
                                            VideoId = video.Id,
                                            From = from,
                                            To = from + TimeSpan.FromMinutes(VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS),
                                        };

                                        segmentsContainingMarket.Add(currentMarketSegment);
                                    }
                                    // If the previous segment did contain the market, extend that segment
                                    else
                                    {
                                        currentMarketSegment.To += TimeSpan.FromMinutes(VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS);
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
                    foreach (TimeSegment marketTimeSegment in segmentsContainingMarket.Where(_ => _.VideoId == video.Id))
                    {
                        DownloadSegmentToCollectorFolder(video, serverId, videoStartTime, marketTimeSegment);
                    }

                    Trace.TraceInformation($"Finished processing video {video.Id}.");
                    processedVideos.Add(video.Id);
                    File.WriteAllText(PROCESSED_VIDEOS_PATH, JsonSerializer.Serialize(processedVideos));

                    File.WriteAllText(MARKET_SEGMENTS_PATH, JsonSerializer.Serialize(segmentsContainingMarket));
                }

                hasMoreVideos = getVideosResponse.Videos.Any();

                DateTime oldestVideoCreateDateInPage = hasMoreVideos ? getVideosResponse.Videos.Min(_ => DateTime.Parse(_.CreatedAt).ToUniversalTime()) : oldestVideoCreateDate;
                oldestVideoCreateDate = oldestVideoCreateDateInPage < oldestVideoCreateDate ? oldestVideoCreateDateInPage : oldestVideoCreateDate;

                if (getVideosResponse.Pagination.Cursor == null)
                {
                    Trace.TraceWarning($"Found null cursor after processing cursor '{cursor}'. This means we've hit our page limit of 500. Exiting with oldest video of {oldestVideoCreateDate}.");
                    return;
                }

                cursor = getVideosResponse.Pagination.Cursor;
            }

            Trace.TraceInformation($"Finished processing all videos in the last day. Oldest create date was {oldestVideoCreateDate}.");
        }

        private void DownloadSegmentToCollectorFolder(Video video, string serverId, DateTime videoStartTime, TimeSegment marketTimeSegment)
        {
            DateTime segmentStartTime = videoStartTime + marketTimeSegment.From - TimeSpan.FromMinutes(VIDEO_CLIP_EXTRACTION_INTERVAL_IN_MINS);
            long fileTime = segmentStartTime.ToFileTimeUtc();

            // Copy video segments over for the Market_Collector to run on
            string videoFileName = GetVideoFileName(GetVideoPrefix(video.UserLogin, video.Id, serverId), fileTime);
            string videoPath = Path.Combine(FULL_MARKET_VIDEO_OUTPUT_DIRECTORY, serverId, videoFileName);

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

                    Trace.TraceInformation($"Downloading full market video {video.Id} from {startOffset} to {duration} to '{videoPath}'.");
                    livestreamerApiClient.Download($"twitch.tv/videos/{video.Id}", "best", videoPath, startOffset, duration);
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

        private readonly Regex wordRegex = new Regex(@"\w+");
        private bool TryExtractServer(string text, out string server)
        {
            server = null;

            if (text == null)
            {
                return false;
            }

            MatchCollection wordMatches = wordRegex.Matches(text);
            string[] textParts = wordMatches.Select(_ => _.Value.ToLowerInvariant()).ToArray();

            foreach (string serverName in serverNamesLowercase.Keys)
            {
                if (textParts.Any(_ => _ == serverName))
                {
                    server = serverNamesLowercase[serverName];
                    return true;
                }
            }

            return false;
        }
    }
}
