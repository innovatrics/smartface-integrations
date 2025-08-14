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
    public class TComServerConnector(ILogger logger, ITServerClientFactory tServerClientFactory) : IAccessControlConnector, IDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly ITServerClientFactory _tServerClientFactory = tServerClientFactory ?? throw new ArgumentNullException(nameof(tServerClientFactory));
        private readonly ConcurrentDictionary<string, ITServerClient> _tServerClients = new();
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
                        TrySendActionCommand(tsServerClient, accessControlMapping, passage: passage);
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
                        var turnstileMode = accessControlMapping.Action != null ? ParseTurnstileMode(accessControlMapping.Action) : PrtclCmfJson.TurnstileMode.all_modes_off;
                        TrySendActionCommand(tsServerClient, accessControlMapping, mode: turnstileMode);
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
                _logger.Debug("No timeout configured for access control mapping {name}", accessControlMapping.Group);
                return;
            }

            var mappingName = accessControlMapping.Group;

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

            _logger.Information("Starting timer for access control mapping {group} with timeout {timeout}ms", mappingName, accessControlMapping.TimeoutMs);

            var timer = new Timer(async _ => await OnTimeoutExpired(accessControlMapping, cts.Token), null, accessControlMapping.TimeoutMs.Value, Timeout.Infinite);

            _timers.TryAdd(mappingName, timer);
        }

        private async Task OnTimeoutExpired(AccessControlMapping accessControlMapping, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.Debug("Timer for access control mapping {group} was cancelled", accessControlMapping.Group);
                return;
            }

            _logger.Information("Timeout expired for access control mapping {group}. Opening gates permanently.", accessControlMapping.Group);

            try
            {
                var tsServerClient = GetClient(accessControlMapping.Host, accessControlMapping.Port);

                TrySendActionCommand(tsServerClient, accessControlMapping, mode: PrtclCmfJson.TurnstileMode.group_on);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while opening gates permanently for access control mapping {group}", accessControlMapping.Group);
            }
            finally
            {
                var mappingName = accessControlMapping.Group;
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

        private ITServerClient GetClient(string host, int? port)
        {
            if (port == null)
            {
                port = 2500;
            }

            var ip = IPAddress.Parse(host);
            var key = $"{ip}:{port}";

            if (!_tServerClients.TryGetValue(key, out var tsServerClient))
            {
                tsServerClient = _tServerClientFactory.Create(ip, port.Value);
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

        private static void ConnectIfNeeded(ITServerClient tsServerClient)
        {
            if (!tsServerClient.IsConnected)
            {
                tsServerClient.Open();
            }
        }

        private bool TrySendActionCommand(ITServerClient tsServerClient, AccessControlMapping accessControlMapping, PrtclCmfJson.TurnstileMode? mode = null, PrtclCmfJson.PassageAction? passage = null, int? nextCallDelayMs = null)
        {
            try
            {
                SendActionCommand(tsServerClient, accessControlMapping, mode, passage);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error occurred while sending action command to {host}:{port} for {reader} and channel {channel} with mode {mode} and passage {passage}", accessControlMapping.Host, accessControlMapping.Port, accessControlMapping.Reader, accessControlMapping.Channel, mode, passage);
                return false;
            }
        }

        private void SendActionCommand(ITServerClient tsServerClient, AccessControlMapping accessControlMapping, PrtclCmfJson.TurnstileMode? mode = null, PrtclCmfJson.PassageAction? passage = null)
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

        private PrtclCmfJson.PassageAction ParsePassageAction(string action)
        {
            try
            {
                return Enum.Parse<PrtclCmfJson.PassageAction>(action, true);
            }
            catch (ArgumentException ex)
            {
                _logger.Error(ex, "Invalid passage action: {action}", action);
                throw;
            }
        }

        private PrtclCmfJson.TurnstileMode ParseTurnstileMode(string action)
        {
            try
            {
                return Enum.Parse<PrtclCmfJson.TurnstileMode>(action, true);
            }
            catch (ArgumentException ex)
            {
                _logger.Error(ex, "Invalid turnstile mode: {action}", action);
                throw;
            }
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