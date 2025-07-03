using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using Serilog;

using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.Cominfo
{
    public class TComServerConnector(
        ILogger logger,
        IHttpClientFactory httpClientFactory
        ) : IAccessControlConnector, IDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        private readonly ConcurrentDictionary<string, TServerClient> _tServerClients = new();
        private readonly ConcurrentDictionary<string, Timer> _timers = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokenSources = new();


        public const string MODE_CLOSE_ON_DENY = "CLOSE_ON_DENY";
        public const string MODE_OPEN_ON_GRANT = "OPEN_ON_GRANT";

        public Task OpenAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            _logger.Information("OpenAsync to {host}:{port} for {reader} and channel {channel}", accessControlMapping.Host, accessControlMapping.Port, accessControlMapping.Reader, accessControlMapping.Channel);

            var tsServerClient = GetClient(accessControlMapping.Host, accessControlMapping.Port);

            var modes = ParseModes(accessControlMapping.Mode);

            foreach (var mode in modes)
            {
                switch (mode)
                {
                    case MODE_OPEN_ON_GRANT:
                        var passage = accessControlMapping.Action != null ? ParsePassageAction(accessControlMapping.Action) : PrtclCmfJson.PassageAction.passL;
                        SendActionCommand(tsServerClient, accessControlMapping, passage: passage);
                        break;
                }
            }

            return Task.CompletedTask;
        }

        public Task DenyAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            _logger.Information("DenyAsync to {host}:{port} for {reader} and channel {channel}", accessControlMapping.Host, accessControlMapping.Port, accessControlMapping.Reader, accessControlMapping.Channel);

            var tsServerClient = GetClient(accessControlMapping.Host, accessControlMapping.Port);

            var modes = ParseModes(accessControlMapping.Mode);

            foreach (var mode in modes)
            {
                switch (mode)
                {
                    case MODE_CLOSE_ON_DENY:
                        var turnstileMode = accessControlMapping.Action != null ? ParseTurnstileMode(accessControlMapping.Action) : PrtclCmfJson.TurnstileMode.group_off;
                        SendActionCommand(tsServerClient, accessControlMapping, mode: turnstileMode);
                        break;
                }
            }

            StartOrResetTimer(accessControlMapping);

            return Task.CompletedTask;
        }

        public Task BlockAsync(AccessControlMapping accessControlMapping, string accessControlUserId = null)
        {
            _logger.Information("BlockAsync to {host}:{port} for {reader} and channel {channel}", accessControlMapping.Host, accessControlMapping.Port, accessControlMapping.Reader, accessControlMapping.Channel);

            return Task.CompletedTask;
        }

        public Task SendKeepAliveAsync(string schema, string host, int? port, int? channel = null, string accessControlUserId = null, string username = null, string password = null)
        {
            return Task.CompletedTask;
        }

        private void StartOrResetTimer(AccessControlMapping accessControlMapping)
        {
            if (accessControlMapping.TimeoutMs == null || accessControlMapping.TimeoutMs <= 0)
            {
                _logger.Debug("No timeout configured for access control mapping {name}", accessControlMapping.Name);
                return;
            }

            var mappingName = accessControlMapping.Name;

            if (_cancellationTokenSources.TryGetValue(mappingName, out var existingCts))
            {
                existingCts.Cancel();
                _cancellationTokenSources.TryRemove(mappingName, out _);
            }

            if (_timers.TryGetValue(mappingName, out var existingTimer))
            {
                existingTimer.Dispose();
                _timers.TryRemove(mappingName, out _);
            }

            var cts = new CancellationTokenSource();
            _cancellationTokenSources.TryAdd(mappingName, cts);

            _logger.Information("Starting timer for access control mapping {name} with timeout {timeout}ms", mappingName, accessControlMapping.TimeoutMs);

            var timer = new Timer(async _ =>
                                {
                                    await OnTimeoutExpired(accessControlMapping, cts.Token);
                                },
                                null, accessControlMapping.TimeoutMs.Value, Timeout.Infinite);

            _timers.TryAdd(mappingName, timer);
        }

        private async Task OnTimeoutExpired(AccessControlMapping accessControlMapping, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.Debug("Timer for access control mapping {name} was cancelled", accessControlMapping.Name);
                return;
            }

            _logger.Information("Timeout expired for access control mapping {name}. Opening gates permanently.", accessControlMapping.Name);

            try
            {
                var tsServerClient = GetClient(accessControlMapping.Host, accessControlMapping.Port);

                SendActionCommand(tsServerClient, accessControlMapping, mode: PrtclCmfJson.TurnstileMode.group_on, passage: PrtclCmfJson.PassageAction.passL);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while opening gates permanently for access control mapping {name}", accessControlMapping.Name);
            }
            finally
            {
                var mappingName = accessControlMapping.Name;
                if (_timers.TryRemove(mappingName, out var timer))
                {
                    timer.Dispose();
                }
                if (_cancellationTokenSources.TryRemove(mappingName, out var cts))
                {
                    cts.Dispose();
                }
            }
        }

        private TServerClient GetClient(string host, int? port)
        {
            if (port == null)
            {
                port = 2500;
            }

            var ip = IPAddress.Parse(host);
            var key = $"{ip}:{port}";

            if (!_tServerClients.TryGetValue(key, out var tsServerClient))
            {
                tsServerClient = new TServerClient(ip, port.Value);
                _tServerClients.TryAdd(key, tsServerClient);
            }

            return tsServerClient;
        }

        private static PrtclCmfJson.Device GetDevice(AccessControlMapping accessControlMapping)
        {
            string line = accessControlMapping.Reader;
            ushort address = (ushort)accessControlMapping.Channel;

            return new PrtclCmfJson.Device(line, address);
        }

        private static void ConnectIfNeeded(TServerClient tsServerClient)
        {
            if (!tsServerClient.IsConnected)
            {
                tsServerClient.Open();
            }
        }

        private void SendActionCommand(TServerClient tsServerClient, AccessControlMapping accessControlMapping, PrtclCmfJson.TurnstileMode? mode = null, PrtclCmfJson.PassageAction? passage = null)
        {
            _logger.Information("Sending action command to {host}:{port} for {reader} and channel {channel} with mode {mode} and passage {passage}", accessControlMapping.Host, accessControlMapping.Port, accessControlMapping.Reader, accessControlMapping.Channel, mode, passage);

            ConnectIfNeeded(tsServerClient);

            var device = GetDevice(accessControlMapping);

            var action = new PrtclCmfJson.MsgAction(device);

            if (mode.HasValue)
            {
                action.mode = mode.Value;
            }

            if (passage.HasValue)
            {
                action.passage = passage.Value;
            }

            tsServerClient.SendMessage(action);
        }

        private string[] ParseModes(string modes)
        {
            if (string.IsNullOrEmpty(modes))
            {
                return new string[] { };
            }

            return modes
                    .Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(mode => mode.Trim())
                    .Where(mode => !string.IsNullOrEmpty(mode))
                    .ToArray();
        }

        private static PrtclCmfJson.PassageAction ParsePassageAction(string action)
        {
            return action?.ToLowerInvariant() switch
            {
                "passl" => PrtclCmfJson.PassageAction.passL,
                "passr" => PrtclCmfJson.PassageAction.passR,
                "partpassl" => PrtclCmfJson.PassageAction.partpassL,
                "partpassr" => PrtclCmfJson.PassageAction.partpassR,
                "passl_verify" => PrtclCmfJson.PassageAction.passL_verify,
                "passr_verify" => PrtclCmfJson.PassageAction.passR_verify,
                _ => PrtclCmfJson.PassageAction.passL
            };
        }

        private static PrtclCmfJson.TurnstileMode ParseTurnstileMode(string action)
        {
            return action?.ToLowerInvariant() switch
            {
                "all_modes_off" => PrtclCmfJson.TurnstileMode.all_modes_off,
                "free_off" => PrtclCmfJson.TurnstileMode.free_off,
                "free_on" => PrtclCmfJson.TurnstileMode.free_on,
                "lockdown_off" => PrtclCmfJson.TurnstileMode.lockdown_off,
                "lockdown_on" => PrtclCmfJson.TurnstileMode.lockdown_on,
                "optical_off" => PrtclCmfJson.TurnstileMode.optical_off,
                "optical_on" => PrtclCmfJson.TurnstileMode.optical_on,
                "group_off" => PrtclCmfJson.TurnstileMode.group_off,
                "group_on" => PrtclCmfJson.TurnstileMode.group_on,
                "cflow_off" => PrtclCmfJson.TurnstileMode.cflow_off,
                "cflow_on" => PrtclCmfJson.TurnstileMode.cflow_on,
                _ => PrtclCmfJson.TurnstileMode.group_off
            };
        }

        public void Dispose()
        {
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }
            _timers.Clear();

            foreach (var cts in _cancellationTokenSources.Values)
            {
                cts.Dispose();
            }
            _cancellationTokenSources.Clear();
        }
    }
}