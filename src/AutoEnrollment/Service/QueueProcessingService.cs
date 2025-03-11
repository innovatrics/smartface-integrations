using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Innovatrics.SmartFace.Integrations.Shared.SmartFaceRestApiClient;
using Microsoft.Extensions.Configuration;
using Serilog;
using SmartFace.AutoEnrollment.Models;

namespace SmartFace.AutoEnrollment.Service
{
    public class QueueProcessingService
    {
        private readonly int MAX_PARALLEL_BLOCKS = 4;
        private readonly EnrollStrategy ENROLL_STRATEGY = EnrollStrategy.FirstPassingCriteria;

        private readonly ILogger _logger;
        private readonly ValidationService _validationService;
        private readonly StreamConfigurationService _streamMappingService;
        private readonly DebouncingService _debouncingService;
        private readonly TrackletDebounceService _trackletTimer;
        private readonly AutoEnrollmentService _autoEnrollmentService;

        private ActionBlock<Notification> _actionBlock;

        public QueueProcessingService(
            ILogger logger,
            IConfiguration configuration,
            DebouncingService debouncingService,
            TrackletDebounceService trackletTimer,
            ValidationService validationService,
            StreamConfigurationService streamMappingService,
            AutoEnrollmentService restAutoEnrollmentService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _streamMappingService = streamMappingService ?? throw new ArgumentNullException(nameof(streamMappingService));
            _debouncingService = debouncingService ?? throw new ArgumentNullException(nameof(debouncingService));
            _trackletTimer = trackletTimer ?? throw new ArgumentNullException(nameof(trackletTimer));
            _autoEnrollmentService = restAutoEnrollmentService ?? throw new ArgumentNullException(nameof(restAutoEnrollmentService));

            var config = configuration.GetSection("Config").Get<Config>();

            MAX_PARALLEL_BLOCKS = config?.MaxParallelActionBlocks ?? MAX_PARALLEL_BLOCKS;
            ENROLL_STRATEGY = config?.EnrollStrategy ?? ENROLL_STRATEGY;
        }

        public void Start()
        {
            _actionBlock = new ActionBlock<Notification>(async notification =>
            {
                try
                {
                    var mappings = _streamMappingService.CreateMappings(notification.StreamId);

                    _logger.Debug("Found {Mappings} mappings for stream {Stream}", mappings?.Count, notification.StreamId);

                    foreach (var mapping in mappings)
                    {
                        var isValidationPassed = _validationService.Validate(notification, mapping);

                        if (isValidationPassed)
                        {
                            switch (ENROLL_STRATEGY)
                            {
                                case EnrollStrategy.FirstPassingCriteria:
                                    var isBlocked = _debouncingService.IsBlocked(notification, mapping);

                                    if (isBlocked)
                                    {
                                        return;
                                    }

                                    _debouncingService.Block(notification, mapping);

                                    await EnrollAsync(notification, mapping);
                                    break;

                                case EnrollStrategy.BestOfTracklet:
                                    _trackletTimer.Enqueue(notification, mapping);
                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to process message");
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = MAX_PARALLEL_BLOCKS
            });

            _trackletTimer.HandleTimeout(EnrollAsync);
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

        private async Task EnrollAsync(Notification notification, StreamConfiguration mapping)
        {
            ArgumentNullException.ThrowIfNull(notification);
            ArgumentNullException.ThrowIfNull(mapping);

            await _autoEnrollmentService.EnrollAsync(notification, mapping);
        }
    }
}
