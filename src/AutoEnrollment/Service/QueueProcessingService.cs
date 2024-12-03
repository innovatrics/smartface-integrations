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
        public readonly int MaxParallelBlocks;
        private readonly ILogger _logger;
        private readonly ValidationService _validationService;
        private readonly StreamConfigurationService _streamMappingService;
        private readonly DebouncingService _debouncingService;
        private readonly AutoEnrollmentService _autoEnrollmentService;

        private ActionBlock<Notification> _actionBlock;

        public QueueProcessingService(
            ILogger logger,
            IConfiguration configuration,
            DebouncingService debouncingService,
            ValidationService validationService,
            StreamConfigurationService streamMappingService,
            AutoEnrollmentService restAutoEnrollmentService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _streamMappingService = streamMappingService ?? throw new ArgumentNullException(nameof(streamMappingService));
            _debouncingService = debouncingService ?? throw new ArgumentNullException(nameof(debouncingService));
            _autoEnrollmentService = restAutoEnrollmentService ?? throw new ArgumentNullException(nameof(restAutoEnrollmentService));

            var config = configuration.GetSection("Config").Get<Config>();

            MaxParallelBlocks = config?.MaxParallelActionBlocks ?? 4;            
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
                            var isBlocked = _debouncingService.IsBlocked(notification, mapping);

                            if (isBlocked)
                            {
                                continue;
                            }

                            _debouncingService.Block(notification, mapping);

                            await EnrollAsync(notification, mapping);                            
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

        private async Task EnrollAsync(Notification notification, StreamConfiguration mapping)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            await _autoEnrollmentService.EnrollAsync(notification, mapping);
        }
    }
}
