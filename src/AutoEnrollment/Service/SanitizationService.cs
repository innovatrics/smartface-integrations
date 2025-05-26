using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

using Serilog;
using SmartFace.AutoEnrollment.Service.Clients;

namespace SmartFace.AutoEnrollment.Service
{
    public class SanitizationService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly TimeSpan _interval;
        private readonly TimeSpan _startTime;
        private readonly IConfiguration _configuration;
        private readonly OAuthService _oAuthService;
        private readonly IHttpClientFactory _httpClientFactory;

        public SanitizationService(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            OAuthService oAuthService
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _oAuthService = oAuthService ?? throw new ArgumentNullException(nameof(oAuthService));

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

                while (now > nextRun)
                {
                    nextRun = nextRun.Add(_interval);
                }

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
                await SanitizeWatchlistsAsync(stoppingToken);
                _logger.Information("Sanitization completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during sanitization.");
            }
        }

        private async Task SanitizeWatchlistsAsync(CancellationToken stoppingToken)
        {
            _logger.Information($"Sanitizing watchlists at {DateTime.UtcNow} UTC...");

            var watchlistIds = _configuration.GetValue<string[]>("Sanitization:WatchlistIds", Array.Empty<string>());

            _logger.Information($"Watchlists to sanitize: {string.Join(", ", watchlistIds)}");

            var smartFaceGraphQLClient = new SmartFaceGraphQLClient(_logger, _configuration, _httpClientFactory, _oAuthService);

            var olderThan = DateTime.UtcNow.AddDays(-1);

            foreach (var watchlistId in watchlistIds)
            {
                _logger.Information($"Sanitizing watchlist {watchlistId}...");

                int skip = 0;
                int take = 1000;
                bool hasNextPage;

                var watchlistMembers = new List<WatchlistMember>();

                do
                {
                    _logger.Information($"Fetching watchlist members for watchlist {watchlistId} older than {olderThan} with skip {skip} and take {take}");

                    var watchlistMembersResponse = await smartFaceGraphQLClient.GetWatchlistMembersPerWatchlistAsync(watchlistId, skip, take, olderThan);

                    hasNextPage = watchlistMembersResponse.WatchlistMembers.PageInfo.HasNextPage;

                    watchlistMembers.AddRange(watchlistMembersResponse.WatchlistMembers.Items);

                    skip += take;
                } while (hasNextPage);

                _logger.Information($"Found {watchlistMembers.Count} watchlist members for watchlist {watchlistId}");

                await DeleteWatchlistMembersAsync(watchlistMembers);
                
            }

            _logger.Information("Sanitization completed successfully.");
        }

        private async Task DeleteWatchlistMembersAsync(List<WatchlistMember> watchlistMembers)
        {
            _logger.Information($"Deleting {watchlistMembers.Count} watchlist members...");

            var schema = _configuration.GetValue("Target:Schema", "http");
            var host = _configuration.GetValue("Target:Host", "SFApi");
            var port = _configuration.GetValue("Target:Port", 8098);

            var baseUri = new Uri($"{schema}://{host}:{port}/");

            var httpClient = _httpClientFactory.CreateClient();

            if (_oAuthService.IsEnabled)
            {
                var authToken = await _oAuthService.GetTokenAsync();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
            }

            var client = new Innovatrics.SmartFace.Integrations.Shared.SmartFaceRestApiClient.SmartFaceRestApiClient(baseUri.ToString(), httpClient);

            foreach (var watchlistMember in watchlistMembers)
            {
                _logger.Information($"Deleting watchlist member {watchlistMember.Id}...");

                await client.WatchlistMembersDELETEAsync($"{watchlistMember.Id}");
            }
        }
    }
}