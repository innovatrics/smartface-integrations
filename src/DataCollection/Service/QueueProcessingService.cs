using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Microsoft.Extensions.Configuration;

using Serilog;

using Minio;
using Minio.DataModel.Args;

using Innovatrics.SmartFace.DataCollection.Models;

namespace Innovatrics.SmartFace.DataCollection.Services
{
    public class QueueProcessingService
    {
        private readonly int _maxParallelBlocks;
        private readonly string[] _watchlistIds;
        private readonly ILogger _logger;

        private readonly string _endpoint;
        private readonly int _port;
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly string _bucketName;
        private readonly string _targetFolder;
        private readonly bool _useSsl;

        private ActionBlock<Notification> _actionBlock;

        public QueueProcessingService(
            ILogger logger,
            IConfiguration configuration
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var config = configuration.GetSection("Config").Get<Config>();

            _maxParallelBlocks = config?.MaxParallelActionBlocks ?? 4;
            _watchlistIds = config?.WatchlistIds ?? new string[] { };

            _endpoint = configuration.GetValue<string>("Minio:Endpoint");
            _port = configuration.GetValue<int>("Minio:Port", 9000);
            _accessKey = configuration.GetValue<string>("Minio:AccessKey");
            _secretKey = configuration.GetValue<string>("Minio:SecretKey");
            _bucketName = configuration.GetValue<string>("Minio:BucketName");
            _targetFolder = configuration.GetValue<string>("Minio:TargetFolder");
            _useSsl = configuration.GetValue<bool>("Minio:UseSsl", false);
        }

        public void Start()
        {
            var minioClient = new MinioClient()
                                    .WithEndpoint(_endpoint, _port)
                                    .WithCredentials(_accessKey, _secretKey)
                                    .WithSSL(_useSsl)
                                    .Build();

            _actionBlock = new ActionBlock<Notification>(async notification =>
            {
                try
                {                    
                    _logger.Information($"Processing {nameof(ActionBlock<Notification>)}: for {{watchlistMemberId}}", notification.WatchlistMemberId);

                    if (_watchlistIds.Length > 0 && !_watchlistIds.Contains(notification.WatchlistId))
                    {
                        _logger.Information("Watchlist {watchlistId} not in watchlistIds", notification.WatchlistId); 
                        return;
                    }

                    string objectName = $"{_targetFolder}/{notification.WatchlistMemberId}/{notification.ReceivedAt.ToString("yyyy-MM-dd")}/{notification.Score}__{notification.ReceivedAt.ToString("HH-mm-ss")}.jpg";
                    byte[] imageData = notification.CropImage;

                    using var ms = new MemoryStream(imageData);

                    try
                    {
                        // Ensure the bucket exists
                        bool found = await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucketName));
                        if (!found)
                        {
                            await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName));
                        }

                        var putObjectArgs = new PutObjectArgs()
                            .WithBucket(_bucketName)
                            .WithObject(objectName)
                            .WithStreamData(ms)
                            .WithObjectSize(ms.Length)
                            .WithContentType("image/jpeg");

                        _logger.Information("Uploading object: {objectName}, Size: {size} bytes", objectName, ms.Length);

                        // Upload the file
                        var putObjectResponse = await minioClient.PutObjectAsync(putObjectArgs);

                        _logger.Information("Upload finished with status {status} for {objectName}", putObjectResponse?.ResponseStatusCode, putObjectResponse?.ObjectName);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to upload image to Minio");
                    }

                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to process message");
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = _maxParallelBlocks
            });
        }

        public async Task StopAsync()
        {
            _actionBlock.Complete();
            await _actionBlock.Completion;
        }

        public void ProcessNotification(Notification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);
            
            _actionBlock.Post(notification);
        }
    }
}
