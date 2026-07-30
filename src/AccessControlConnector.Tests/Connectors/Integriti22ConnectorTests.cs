using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.InnerRange;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Serilog;
using Xunit;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Tests.Connectors
{
    public class Integriti22ConnectorTests
    {
        private const string CardData = "9dfa98";

        private sealed class CapturingHandler : HttpMessageHandler
        {
            public HttpRequestMessage LastRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }

        private static (Integriti22Connector connector, CapturingHandler handler) CreateConnector()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Integriti22:Host"] = "10.11.110.2",
                    ["Integriti22:Port"] = "8080",
                    ["Integriti22:Username"] = "user",
                    ["Integriti22:Password"] = "pass",
                    ["Integriti22:Controller"] = "650",
                })
                .Build();

            var handler = new CapturingHandler();
            var httpClientFactory = Substitute.For<IHttpClientFactory>();
            httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

            var logger = new LoggerConfiguration().CreateLogger();

            return (new Integriti22Connector(logger, configuration, httpClientFactory), handler);
        }

        [Fact]
        public async Task OpenAsync_DoorIdWithoutDoorName_SendsDoorIdUrl()
        {
            var (connector, handler) = CreateConnector();

            await connector.OpenAsync(new StreamConfig { DoorId = "5069341309534211" }, CardData);

            var uri = handler.LastRequest.RequestUri.ToString();
            Assert.Equal($"http://10.11.110.2:8080/CardBadge?CardData={CardData}&CardBitLength=32&DoorId=5069341309534211&Controller=650", uri);
        }

        [Fact]
        public async Task OpenAsync_DoorIdWithEmptyDoorName_SendsDoorIdUrl()
        {
            var (connector, handler) = CreateConnector();

            await connector.OpenAsync(new StreamConfig { DoorName = "", DoorId = "5069341309534211" }, CardData);

            var uri = handler.LastRequest.RequestUri.ToString();
            Assert.Contains("DoorId=5069341309534211&Controller=650", uri);
            Assert.DoesNotContain("DoorName", uri);
        }

        [Fact]
        public async Task OpenAsync_DoorIdAndDoorName_PrefersDoorId()
        {
            var (connector, handler) = CreateConnector();

            await connector.OpenAsync(new StreamConfig { DoorName = "R38/X01", DoorId = "5069341309534211" }, CardData);

            var uri = handler.LastRequest.RequestUri.ToString();
            Assert.Contains("DoorId=5069341309534211&Controller=650", uri);
            Assert.DoesNotContain("DoorName", uri);
        }

        [Fact]
        public async Task OpenAsync_DoorNameWithoutDoorId_SendsDoorNameUrl()
        {
            var (connector, handler) = CreateConnector();

            await connector.OpenAsync(new StreamConfig { DoorName = "R38/X01" }, CardData);

            var uri = handler.LastRequest.RequestUri.ToString();
            Assert.Contains("DoorName=R38/X01&Controller=650", uri);
            Assert.DoesNotContain("DoorId", uri);
        }

        [Fact]
        public async Task OpenAsync_DoorIdWithoutController_FallsBackToReaderUrl()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Integriti22:Host"] = "10.11.110.2",
                    ["Integriti22:Port"] = "8080",
                })
                .Build();

            var handler = new CapturingHandler();
            var httpClientFactory = Substitute.For<IHttpClientFactory>();
            httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

            var connector = new Integriti22Connector(new LoggerConfiguration().CreateLogger(), configuration, httpClientFactory);

            await connector.OpenAsync(new StreamConfig { DoorId = "5069341309534211", Reader = "RM-1", Channel = 2 }, CardData);

            var uri = handler.LastRequest.RequestUri.ToString();
            Assert.Contains("ReaderModuleID=RM-1&ReaderNumber=2", uri);
            Assert.DoesNotContain("DoorId", uri);
        }

        [Fact]
        public async Task OpenAsync_NoDoorNameNorDoorId_FallsBackToReaderUrl()
        {
            var (connector, handler) = CreateConnector();

            await connector.OpenAsync(new StreamConfig { Reader = "RM-1", Channel = 2 }, CardData);

            var uri = handler.LastRequest.RequestUri.ToString();
            Assert.Contains("ReaderModuleID=RM-1&ReaderNumber=2", uri);
            Assert.DoesNotContain("DoorName", uri);
            Assert.DoesNotContain("DoorId", uri);
        }
    }
}
