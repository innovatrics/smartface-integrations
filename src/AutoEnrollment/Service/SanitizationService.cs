using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

using Serilog;

namespace SmartFace.AutoEnrollment.Service
{
    public class SanitizationService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly TimeSpan _interval;
        private readonly TimeSpan _startTime;
        private readonly IConfiguration _configuration;

        public SanitizationService(ILogger logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));


            // Read settings from IConfiguration
            string startTimeString = configuration["Sanitization:StartTime"] ?? "23:00:00";
            string intervalString = configuration["Sanitization:IntervalHours"] ?? "6";

            if (!TimeSpan.TryParse(startTimeString, out _startTime))
            {
                _logger.Error("Invalid StartTime format in configuration. Using default 23:00:00 UTC.");
                _startTime = new TimeSpan(23, 0, 0);
            }

            if (!int.TryParse(intervalString, out int intervalHours))
            {
                _logger.Error("Invalid IntervalHours format in configuration. Using default 6 hours.");
                intervalHours = 6;
            }

            _interval = TimeSpan.FromHours(intervalHours);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Information("SanitizationService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                DateTime now = DateTime.UtcNow;
                DateTime nextRun = now.Date.Add(_startTime);

                // If current time is past today's scheduled time, find the next valid slot
                while (now > nextRun)
                    nextRun = nextRun.Add(_interval);

                TimeSpan delay = nextRun - now;
                _logger.Information($"Next cleanup scheduled at {nextRun} UTC. Sleeping for {delay}...");

                try
                {
                    await Task.Delay(delay, stoppingToken);
                    await RunCleanup(stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    _logger.Information("Shutdown detected. Exiting cleanup service.");
                    break;
                }
            }
        }

        private async Task RunCleanup(CancellationToken stoppingToken)
        {
            _logger.Information($"Running sanitization at {DateTime.UtcNow} UTC...");

            try
            {
                // Simulate cleanup work
                await Task.Delay(2000, stoppingToken);
                _logger.Information("Sanitization completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during sanitization.");
            }
        }

        private async Task SanitizeWatchlist(CancellationToken stoppingToken)
        {
            _logger.Information($"Sanitizing watchlist at {DateTime.UtcNow} UTC...");

            var watchlistIds = _configuration.GetValue<string[]>("Sanitization:WatchlistIds", Array.Empty<string>());

            foreach (var watchlistId in watchlistIds)
            {
                _logger.Information($"Sanitizing watchlist {watchlistId}...");

                await Task.Delay(2000, stoppingToken);
            }

            _logger.Information("Sanitization completed successfully.");
        }
    }
}