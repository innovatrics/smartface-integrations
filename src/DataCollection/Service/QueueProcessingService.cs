using System;
using System.IO;
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
        public readonly int MaxParallelBlocks;
        private readonly ILogger _logger;

        private readonly string _endpoint;
        private readonly int _port;
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly string _bucketName;
        private readonly bool _useSsl;

        private ActionBlock<Notification> _actionBlock;

        public QueueProcessingService(
            ILogger logger,
            IConfiguration configuration
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var config = configuration.GetSection("Config").Get<Config>();

            MaxParallelBlocks = config?.MaxParallelActionBlocks ?? 4;

            _endpoint = configuration.GetValue<string>("Minio:Endpoint");
            _port = configuration.GetValue<int>("Minio:Port", 9000);
            _accessKey = configuration.GetValue<string>("Minio:AccessKey");
            _secretKey = configuration.GetValue<string>("Minio:SecretKey");
            _bucketName = configuration.GetValue<string>("Minio:BucketName");
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

                    string objectName = $"{notification.WatchlistMemberId}/{notification.ReceivedAt.ToString("yyyy-MM-dd")}/{notification.Score}_{notification.ReceivedAt.ToString("HH-mm-ss")}.jpg";
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
                        await minioClient.PutObjectAsync(putObjectArgs);

                        _logger.Information("Successfully uploaded {objectName} to {bucketName}", objectName, _bucketName);
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
                MaxDegreeOfParallelism = MaxParallelBlocks
            });
        }

        public async Task StopAsync()
        {
            _actionBlock.Complete();
            await _actionBlock.Completion;
        }

        public void ProcessNotification(Notification notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            _actionBlock.Post(notification);
        }
    }
}
