using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector
{
    public class Program
    {
        private const string ZERO_MQ_DEFAULT_HOST = "localhost";
        private const int ZERO_MQ_DEFAULT_PORT = 2406;
        private const string ZERO_MQ_ALL_NOTIFICATIONS = "all";

        public const string HOST_ARG_NAME = "-host";
        public const string PORT_ARG_NAME = "-port";
        public const string TOPICS_ARG_NAME = "-topics";

        public static CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        public static bool ReadKeyToStop = Environment.UserInteractive;

        public static async Task Main(string[] args)
        {
            try
            {
                var logger = SetupBasicLogging();

                var configurationRoot = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json", optional: false)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args)
                    .Build();

                
                var hostBuilder = CreateHostBuilder(args, logger, configurationRoot);
                using var host = hostBuilder.Build();

                var hostApplicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

                var applicationTask = host.RunAsync();

                await using (hostApplicationLifetime.ApplicationStarted.Register(() =>
                {
                    Log.Information("Application started.");
                })) { }

                await applicationTask;

                Log.Information("Application stopped.");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application start-up failed.");
                Log.CloseAndFlush();
                throw;
            }

            ParseArguments(args, out string hostName, out int port, out var subscribedTopics);

            // Create notification reader
            var reader = new ZeroMqNotificationReader(hostName, port, CancellationTokenSource.Token);

            // Log errors
            reader.OnError += (ex) => { Console.WriteLine($"ERROR : {ex.Message}"); };

            // Hook on notification event
            reader.OnNotificationReceived += (topic, json) =>
            {
                // Skip not subscribed topics
                if (!subscribedTopics.Contains(topic))
                    return;

                Console.WriteLine($"Notification with topic : {topic} received.");

                switch (topic)
                {
                    case ZeroMqNotificationTopic.FACE_CREATED:
                        {
                            var dto = JsonConvert.DeserializeObject<FaceCreatedNotificationDTO>(json);
                            Console.WriteLine($"Face with Id {dto.Id} was created.");
                            break;
                        }
                    case ZeroMqNotificationTopic.FACE_EXTRACTED:
                    {
                        var dto = JsonConvert.DeserializeObject<FaceExtractedNotificationDTO>(json);
                        Console.WriteLine($"Face {dto.Id} properties were extracted.");
                        break;
                    }
                    case ZeroMqNotificationTopic.TRACKLET_COMPLETED:
                        {
                            var dto = JsonConvert.DeserializeObject<TrackletCompletedNotificationDTO>(json);
                            Console.WriteLine($"Tracklet with Id {dto.Id} was completed.");
                            break;
                        }
                    case ZeroMqNotificationTopic.VIDEO_RECORD_STATE_UPDATE:
                        {
                            var dto = JsonConvert.DeserializeObject<VideoRecordNotificationStateChangedDTO>(json);
                            Console.WriteLine($"Video record with Id {dto.Id} was updated.");
                            Console.WriteLine($"State changed to {dto.State}");
                            break;
                        }
                    case ZeroMqNotificationTopic.GROUPING_PROGRESS_INFO:
                        {
                            var dto = JsonConvert.DeserializeObject<GroupingProgressDTO>(json);
                            Console.WriteLine($"Grouping {dto.Id} changed to {dto.Status}.");
                            break;
                        }
                    case ZeroMqNotificationTopic.MATCH_RESULT_MATCH:
                        {
                            var dto = JsonConvert.DeserializeObject<MatchResultNotificationDTO>(json);
                            Console.WriteLine($"Match result received.");
                            Console.WriteLine($"{nameof(MatchResultNotificationDTO.WatchlistId)} : {dto.WatchlistId}");
                            Console.WriteLine($"{nameof(MatchResultNotificationDTO.Score)} : {dto.Score}");
                            Console.WriteLine($"{nameof(MatchResultNotificationDTO.WatchlistDisplayName)} : {dto.WatchlistDisplayName}");
                            Console.WriteLine($"{nameof(MatchResultNotificationDTO.WatchlistMemberDisplayName)} : {dto.WatchlistMemberDisplayName}");
                            // MatchResultNotificationDTO contains also cropped image of face (dto.CropImage)
                            break;
                        }
                    case ZeroMqNotificationTopic.MATCH_RESULT_MATCH_INSERTED:
                        {
                            var dto = JsonConvert.DeserializeObject<MatchResultNotificationInsertDTO>(json);
                            Console.WriteLine($"Match result with Id {dto.Id} inserted into database.");
                            Console.WriteLine($"{nameof(MatchResultNotificationDTO.WatchlistMemberId)}");
                            Console.WriteLine($"{nameof(MatchResultNotificationDTO.WatchlistId)}");
                            Console.WriteLine($"{nameof(MatchResultNotificationDTO.Score)} : {dto.Score}");
                            Console.WriteLine($"{nameof(MatchResultNotificationDTO.WatchlistDisplayName)} : {dto.WatchlistDisplayName}");                    
                            break;
                        }
                    default:
                        {
                            Console.WriteLine("Message with unknown topic received.");
                            Console.WriteLine($"Payload :");
                            Console.WriteLine(json);
                            break;
                        }
                }

                Console.WriteLine(Environment.NewLine);
            };

            Console.WriteLine($"Notification reader initialized at {reader.EndPoint}.");

            Console.WriteLine("Listening for topics :");
            foreach (var subscribedTopic in subscribedTopics)
            {
                Console.WriteLine($"{subscribedTopic}");
            }

            // Start listening for ZeroMQ messages
            reader.Init();

            // Wait until some key is pressed
            if (ReadKeyToStop)
            {
                Console.ReadKey();
                CancellationTokenSource.Cancel();
            }

            Console.WriteLine("Disposing notification reader.");
            // Make sure we stop notification reader
            // This dispose call will block until the CancellationTokenSource is cancelled
            reader.Dispose();

            return Task.CompletedTask;
        }

        private static void ParseArguments(string[] args, out string hostName, out int port, out string []notificationTopics)
        {
            var allTopics = ZeroMqNotificationTopic.GetAll();

            hostName = ZERO_MQ_DEFAULT_HOST;
            port = ZERO_MQ_DEFAULT_PORT;
            notificationTopics = allTopics;
            
            var hostArg = args.SkipWhile(a => a != HOST_ARG_NAME).Skip(1).FirstOrDefault();
            var portArg = args.SkipWhile(a => a != PORT_ARG_NAME).Skip(1).FirstOrDefault();
            var topicsArg = args.SkipWhile(a => a != TOPICS_ARG_NAME).Skip(1).FirstOrDefault();

            if (hostArg != null)
            {
                hostName = hostArg;
            }

            if (portArg != null && int.TryParse(portArg, out int p))
            {
                port = p;
            }

            if (topicsArg != null)
            {
                if (topicsArg == ZERO_MQ_ALL_NOTIFICATIONS)
                {
                    notificationTopics = allTopics;
                    return;
                }

                var inputTopics = topicsArg.Split('|');

                foreach (var inputTopic in inputTopics)
                {
                    if (!allTopics.Contains(inputTopic))
                        throw new ArgumentException($"Unknown notification topic {inputTopic}.");
                }

                notificationTopics = inputTopics;
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args, ILogger logger, IConfigurationRoot configurationRoot)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder =>
                {
                    builder.Sources.Clear();
                    builder.AddConfiguration(configurationRoot);
                })
                .ConfigureWebHostDefaults(webHostBuilder =>
                    {
                        webHostBuilder
                            .UseConfiguration(configurationRoot)
                            .UseSerilog(logger)
                            .UseStartup<Startup>();
                    })
                .UseWindowsService()
                .UseSystemd();
        }

        private static ILogger SetupBasicLogging()
        {
            var logRootDir = Path.GetFullPath(Path.Combine(FileApplicationData.AppDataDirPath(), "logs")); // , $"{fileNameWithoutExtension}.log"));

            var absLogFilePath = Path.GetFullPath(Path.Combine(FileApplicationData.AppDataDirPath(), "logs", $"{fileNameWithoutExtension}.log"));
            return absLogFilePath;
            
            var loggingFile = Path.Combine(logRootDir, $"{LOG_FILE_NAME}.log");
            var errorLogFile = Path.Combine(logRootDir, $"{ERROR_LOG_FILE_NAME}.log");

            var logger = new LoggerConfiguration().
                WithBasicConfiguration(loggingFile, errorLogFile)
                .CreateLogger();

            Log.Logger = logger;

            return logger;
        }
    }
}
