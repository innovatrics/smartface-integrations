using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;

using Innovatrics.SmartFace.Integrations.AeosDashboards;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards.AnalyticsReporting
{
    /// <summary>
    /// Periodically maps locker analytics (same data as GET /api/lockeranalytics/groups) to a snapshot payload
    /// and POSTs it to the configured analytics endpoint.
    /// </summary>
    public class AnalyticsReportingHostedService : BackgroundService
    {
        public const string HttpClientName = "AnalyticsReporting";

        private const string ConfigPrefix = "AeosDashboards:AnalyticsReporting:";
        private const string DefaultSnapshotPath = "/api/v1/lockers/snapshot";
        private const int InitialSyncPollMs = 500;
        /// <summary>Max characters per verbose log chunk to avoid single-line truncation in sinks/viewers.</summary>
        private const int VerbosePayloadChunkChars = 3500;

        private readonly Serilog.ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IDataOrchestrator dataOrchestrator;
        private readonly IHttpClientFactory httpClientFactory;

        private static readonly JsonSerializerSettings SnapshotJsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
            Formatting = Formatting.None
        };

        public AnalyticsReportingHostedService(
            Serilog.ILogger logger,
            IConfiguration configuration,
            IDataOrchestrator dataOrchestrator,
            IHttpClientFactory httpClientFactory)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.dataOrchestrator = dataOrchestrator ?? throw new ArgumentNullException(nameof(dataOrchestrator));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!configuration.GetValue<bool>($"{ConfigPrefix}Enabled", false))
            {
                logger.Information("Analytics reporting is disabled (AeosDashboards:AnalyticsReporting:Enabled).");
                return;
            }

            var periodMs = configuration.GetValue<int>($"{ConfigPrefix}RefreshPeriodMs");
            if (periodMs <= 0)
            {
                logger.Warning(
                    "Analytics reporting is enabled but RefreshPeriodMs is {Period}; expected a positive value. Service will not run.",
                    periodMs);
                return;
            }

            var requestUri = BuildRequestUri();
            if (requestUri == null)
            {
                logger.Error("Analytics reporting: Host is missing or invalid; service will not run.");
                return;
            }

            logger.Information(
                "Analytics reporting enabled; waiting for the first successful AEOS sync (lockers, groups, employees, templates) before sending snapshots to {Uri}.",
                requestUri);

            try
            {
                await WaitForInitialAeosDataAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            logger.Information(
                "Analytics reporting: initial AEOS data ready; POST every {PeriodMs}ms to {Uri}",
                periodMs,
                requestUri);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SendSnapshotAsync(requestUri, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Analytics reporting: failed to send locker snapshot.");
                }

                try
                {
                    await Task.Delay(periodMs, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            logger.Information("Analytics reporting stopped.");
        }

        private async Task WaitForInitialAeosDataAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !dataOrchestrator.IsInitialAeosDataLoaded)
            {
                logger.Debug("Analytics reporting: still waiting for initial AEOS data load…");
                await Task.Delay(InitialSyncPollMs, cancellationToken);
            }
        }

        private Uri? BuildRequestUri()
        {
            var host = configuration.GetValue<string>($"{ConfigPrefix}Host");
            if (string.IsNullOrWhiteSpace(host))
                return null;

            host = host.Trim().TrimEnd('/');

            var path = configuration.GetValue<string>($"{ConfigPrefix}SnapshotPath");
            if (string.IsNullOrWhiteSpace(path))
                path = DefaultSnapshotPath;
            path = path.Trim();
            if (!path.StartsWith('/'))
                path = '/' + path;

            if (Uri.TryCreate(host, UriKind.Absolute, out var baseUri))
                return new Uri(baseUri, path);

            var port = configuration.GetValue($"{ConfigPrefix}Port", 443);
            var builder = new UriBuilder(Uri.UriSchemeHttps, host, port)
            {
                Path = path
            };
            return builder.Uri;
        }

        private async Task SendSnapshotAsync(Uri requestUri, CancellationToken cancellationToken)
        {
            var analytics = await dataOrchestrator.GetLockerAnalytics();
            var forcePersonalLockType = configuration.GetValue<bool>($"{ConfigPrefix}ForcePersonalLockType");
            var payload = LockerAnalyticsSnapshotMapper.FromAnalytics(analytics, forcePersonalLockType);
            var json = JsonConvert.SerializeObject(payload, SnapshotJsonSettings);

            if (configuration.GetValue<bool>($"{ConfigPrefix}VerboseLogging"))
            {
                LogVerbosePayload(requestUri, json);
            }

            var client = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var user = configuration.GetValue<string>($"{ConfigPrefix}User");
            var pass = configuration.GetValue<string>($"{ConfigPrefix}Pass");
            if (!string.IsNullOrEmpty(user))
            {
                var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass ?? string.Empty}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
            }

            var apiKey = configuration.GetValue<string>($"{ConfigPrefix}ApiKey");
            if (!string.IsNullOrWhiteSpace(apiKey))
                request.Headers.TryAddWithoutValidation("X-API-Key", apiKey.Trim());

            var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.Information(
                    "Analytics reporting: snapshot sent successfully. Groups={GroupCount} snapshotAt={SnapshotAt} status={Status} responseLength={Length}",
                    payload.Groups.Count,
                    payload.SnapshotAt,
                    (int)response.StatusCode,
                    body.Length);
            }
            else
            {
                // Error bodies (especially 400 validation) embed long JSON strings; keep more than default truncate.
                logger.Warning(
                    "Analytics reporting: snapshot POST failed. status={Status} body={Body}",
                    (int)response.StatusCode,
                    TruncateForLog(body, maxLen: 16384));
            }
        }

        private static string TruncateForLog(string? text, int maxLen = 512)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "…";
        }

        /// <summary>
        /// Logs the exact JSON sent on the wire. Large payloads are split across multiple log lines (same correlation id,
        /// part index) so sinks that cap line/field length do not corrupt the text. Concatenate parts in order to recover the full body.
        /// Uses :l so braces inside JSON are not treated as message-template holes when rendered.
        /// </summary>
        private void LogVerbosePayload(Uri requestUri, string json)
        {
            var correlation = Guid.NewGuid().ToString("N");
            var total = json.Length;
            if (total == 0)
            {
                logger.Information(
                    "Analytics reporting: Verbose POST {Uri} correlation {Correlation} empty JSON body.",
                    requestUri,
                    correlation);
                return;
            }

            var partCount = (total + VerbosePayloadChunkChars - 1) / VerbosePayloadChunkChars;
            logger.Information(
                "Analytics reporting: Verbose POST {Uri} correlation {Correlation} application/json {TotalLength} chars in {PartCount} log part(s); concatenate PayloadPart in order.",
                requestUri,
                correlation,
                total,
                partCount);

            for (var i = 0; i < partCount; i++)
            {
                var offset = i * VerbosePayloadChunkChars;
                var len = Math.Min(VerbosePayloadChunkChars, total - offset);
                var chunk = json.Substring(offset, len);
                logger.Information(
                    "Analytics reporting: Verbose payload part {PartIndex}/{PartCount} correlation {Correlation}:\n{PayloadPart:l}",
                    i + 1,
                    partCount,
                    correlation,
                    chunk);
            }
        }
    }
}
