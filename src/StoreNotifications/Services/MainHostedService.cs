using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.StoreNotifications.Models;
using Innovatrics.SmartFace.StoreNotifications.Data;

namespace Innovatrics.SmartFace.StoreNotifications.Services
{
    public class MainHostedService : IHostedService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly MainDbContext _mainDbContext;
        private readonly QueueProcessingService _queueProcessingService;
        private List<GraphQLPedestriansService> _notificationSources;
        private List<GraphQLMatchResultsService> _matchResultsSources;

        public MainHostedService(
            ILogger logger,
            IConfiguration configuration,
            MainDbContext mainDbContext,
            QueueProcessingService queueProcessingService
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _mainDbContext = mainDbContext ?? throw new ArgumentNullException(nameof(mainDbContext));
            _queueProcessingService = queueProcessingService ?? throw new ArgumentNullException(nameof(queueProcessingService));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{nameof(MainHostedService)} is starting");

            await InitAndSeedDatabaseAsync();

            _notificationSources = new List<GraphQLPedestriansService>();
            _matchResultsSources = new List<GraphQLMatchResultsService>();

            _notificationSources.AddRange(StartPedestrianProcessing());
            _matchResultsSources.AddRange(StartMatchResultsProcessing());

            _queueProcessingService.Start();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"{nameof(MainHostedService)} is stopping");

            foreach (var notificationSource in _notificationSources)
            {
                notificationSource.Stop();
            }

            await _queueProcessingService.StopAsync();
        }

        private List<GraphQLPedestriansService> StartPedestrianProcessing()
        {
            var observer = new PedestrianObserver(_logger);

            observer.OnNotification += (notification) =>
            {
                _queueProcessingService.ProcessNotification(
                    new PedestrianProcessed
                    {
                        StreamId = notification.FrameInformation.StreamId,
                        FrameId = notification.FrameInformation.FrameId,
                        FrameTimestampMicroseconds = notification.FrameInformation.FrameTimestampMicroseconds,
                        ProcessedAt = notification.FrameInformation.ProcessedAt,

                        TrackletId = notification.PedestrianInformation.TrackletId,
                        Size = notification.PedestrianInformation.Size,
                        Quality = notification.PedestrianInformation.Quality,
                        PedestrianOrder = notification.PedestrianInformation.PedestrianOrder,
                        PedestriansOnFrameCount = notification.PedestrianInformation.PedestriansOnFrameCount
                    }
                );
                return Task.CompletedTask;
            };

            var sources = _configuration.GetSection("Sources").Get<Source[]>();

            var notificationSources = new List<GraphQLPedestriansService>();

            foreach (var source in sources)
            {
                _logger.Information("Starting {type} source for {source}", nameof(GraphQLPedestriansService), source);

                var notificationSource = new GraphQLPedestriansService(
                    _logger,
                    source.Schema,
                    source.Host,
                    source.Port,
                    source.Path,
                    observer
                );

                notificationSources.Add(notificationSource);

                notificationSource.Start();
            }

            return notificationSources;
        }

        private List<GraphQLMatchResultsService> StartMatchResultsProcessing()
        {
            var observer = new MatchResultObserver(_logger);

            observer.OnNotification += (notification) =>
            {
                _queueProcessingService.ProcessNotification(
                    new MatchResult
                    {
                        StreamId = notification.StreamId,
                        FrameId = notification.FrameId,
                        ProcessedAt = notification.ProcessedAt,
                        TrackletId = notification.TrackletId,
                        WatchlistId = notification.WatchlistId,
                        WatchlistMemberId = notification.WatchlistMemberId,
                        WatchlistMemberDisplayName = notification.WatchlistMemberDisplayName,
                        FaceSize = notification.FaceSize,
                        FaceOrder = notification.FaceOrder,
                        FacesOnFrameCount = notification.FacesOnFrameCount,
                        FaceQuality = notification.FaceQuality,
                    }
                );
                return Task.CompletedTask;
            };

            var sources = _configuration.GetSection("Sources").Get<Source[]>();

            var notificationSources = new List<GraphQLMatchResultsService>();

            foreach (var source in sources)
            {
                _logger.Information("Starting {type} source for {source}", nameof(GraphQLMatchResultsService), source);

                var notificationSource = new GraphQLMatchResultsService(
                    _logger,
                    source.Schema,
                    source.Host,
                    source.Port,
                    source.Path,
                    observer
                );

                notificationSources.Add(notificationSource);

                notificationSource.Start();
            }

            return notificationSources;
        }

        private async Task InitAndSeedDatabaseAsync()
        {
            await _mainDbContext.Database.EnsureCreatedAsync();
        }
    }
}