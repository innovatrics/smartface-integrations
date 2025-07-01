using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.Cominfo
{
    public class TComServerConnector(
        ILogger logger,
        IHttpClientFactory httpClientFactory
        ) : IAccessControlConnector
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        private readonly ConcurrentDictionary<string, TServerClient> _tServerClients = new();

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
                        SendOpenCommand(tsServerClient, accessControlMapping.Action);
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
                        SendCloseCommand(tsServerClient, accessControlMapping.Action);
                        break;
                }
            }

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

        private void SendOpenCommand(TServerClient tsServerClient, string action = null)
        {
            ConnectIfNeeded(tsServerClient);

            var device = GetDevice(accessControlMapping);

            var action = new PrtclCmfJson.MsgAction(device)
            {
                mode = action ?? PrtclCmfJson.TurnstileMode.group_off
            };

            tsServerClient.SendMessage(action);
        }

        private void SendCloseCommand(TServerClient tsServerClient, string action = null)
        {
            ConnectIfNeeded(tsServerClient);

            var device = GetDevice(accessControlMapping);

            var action = new PrtclCmfJson.MsgAction(device)
            {
                mode = action ?? PrtclCmfJson.TurnstileMode.group_off
            };

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
    }
}