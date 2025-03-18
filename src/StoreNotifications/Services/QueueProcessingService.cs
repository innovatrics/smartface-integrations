using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Microsoft.Extensions.Configuration;

using Serilog;

using Innovatrics.SmartFace.StoreNotifications.Models;
using Innovatrics.SmartFace.StoreNotifications.Data;

namespace Innovatrics.SmartFace.StoreNotifications.Services
{
    public class QueueProcessingService
    {
        private readonly int _maxParallelBlocks;
        private readonly string[] _watchlistIds;
        
        private readonly ILogger _logger;
        private readonly MainDbContext _mainDbContext;

        private ActionBlock<object> _actionBlock;

        public QueueProcessingService(
            ILogger logger,
            IConfiguration configuration,
            MainDbContext mainDbContext
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mainDbContext = mainDbContext ?? throw new ArgumentNullException(nameof(mainDbContext));

            var config = configuration.GetSection("Config").Get<Config>();

            _maxParallelBlocks = config?.MaxParallelActionBlocks ?? 4;
            _watchlistIds = config?.WatchlistIds ?? new string[] { };
        }

        public void Start()
        {
            _actionBlock = new ActionBlock<object>(async notification =>
            {
                try
                {
                    _logger.Information("Processing notification {notification}", notification);

                    _mainDbContext.Add(notification);

                    await _mainDbContext.SaveChangesAsync();
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

        public void ProcessNotification(object notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            _actionBlock.Post(notification);
        }
    }
}
