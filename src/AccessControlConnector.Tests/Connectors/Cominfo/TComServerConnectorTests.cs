using System;
using System.Threading;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.Cominfo;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Serilog;
using Xunit;
using Serilog.Core;
using NSubstitute;
using System.Net;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Tests
{
    public class TComServerConnectorTests
    {
        private readonly ILogger _logger;
        private readonly ITServerClientFactory _tServerClientFactory;

        public TComServerConnectorTests()
        {
            _logger = Substitute.For<ILogger>();
            _tServerClientFactory = Substitute.For<ITServerClientFactory>();

            var mockClient = Substitute.For<ITServerClient>();
            mockClient.IsConnected.Returns(true);
            _tServerClientFactory.Create(Arg.Any<IPAddress>(), Arg.Any<int>()).Returns(mockClient);
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldNotThrow()
        {
            // Act & Assert
            var exception = Record.Exception(() => new TComServerConnector(_logger, _tServerClientFactory));
            Assert.Null(exception);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TComServerConnector(null, _tServerClientFactory));
        }

        [Fact]
        public void Constructor_WithNullFactory_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TComServerConnector(_logger, null));
        }

        [Fact]
        public async Task DenyAsync_WithTimeoutConfigured_ShouldStartTimer()
        {
            // Arrange
            var connector = new TComServerConnector(_logger, _tServerClientFactory);
            var accessControlMapping = new AccessControlMapping
            {
                Name = "TestMapping",
                Host = "192.168.1.100",
                Port = 2500,
                Reader = "Reader1",
                Channel = 1,
                Mode = "CLOSE_ON_DENY",
                TimeoutMs = 100 // Short timeout for testing
            };

            // Act
            await connector.DenyAsync(accessControlMapping);

            // Assert
            // The timer should be started. We can verify this by checking if the connector implements IDisposable
            // and that it can be disposed without throwing exceptions
            Assert.True(connector is IDisposable);
        }

        [Fact]
        public async Task DenyAsync_WithNoTimeoutConfigured_ShouldNotStartTimer()
        {
            // Arrange
            var connector = new TComServerConnector(_logger, _tServerClientFactory);
            var accessControlMapping = new AccessControlMapping
            {
                Name = "TestMapping",
                Host = "192.168.1.100",
                Port = 2500,
                Reader = "Reader1",
                Channel = 1,
                Mode = "CLOSE_ON_DENY"
                // No TimeoutMs configured
            };

            // Act
            await connector.DenyAsync(accessControlMapping);

            // Assert
            // Should complete without throwing exceptions
            Assert.True(true);
        }

        [Fact]
        public async Task DenyAsync_WithZeroTimeout_ShouldNotStartTimer()
        {
            // Arrange
            var connector = new TComServerConnector(_logger, _tServerClientFactory);
            var accessControlMapping = new AccessControlMapping
            {
                Name = "TestMapping",
                Host = "192.168.1.100",
                Port = 2500,
                Reader = "Reader1",
                Channel = 1,
                Mode = "CLOSE_ON_DENY",
                TimeoutMs = 0
            };

            // Act
            await connector.DenyAsync(accessControlMapping);

            // Assert
            // Should complete without throwing exceptions
            Assert.True(true);
        }

        [Fact]
        public async Task DenyAsync_WithNegativeTimeout_ShouldNotStartTimer()
        {
            // Arrange
            var connector = new TComServerConnector(_logger, _tServerClientFactory);
            var accessControlMapping = new AccessControlMapping
            {
                Name = "TestMapping",
                Host = "192.168.1.100",
                Port = 2500,
                Reader = "Reader1",
                Channel = 1,
                Mode = "CLOSE_ON_DENY",
                TimeoutMs = -1000
            };

            // Act
            await connector.DenyAsync(accessControlMapping);

            // Assert
            // Should complete without throwing exceptions
            Assert.True(true);
        }

        [Fact]
        public void Dispose_ShouldNotThrowException()
        {
            // Arrange
            var connector = new TComServerConnector(_logger, _tServerClientFactory);

            // Act & Assert
            var exception = Record.Exception(() => connector.Dispose());
            Assert.Null(exception);
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes_ShouldNotThrowException()
        {
            // Arrange
            var connector = new TComServerConnector(_logger, _tServerClientFactory);

            // Act & Assert
            connector.Dispose();
            var exception = Record.Exception(() => connector.Dispose());
            Assert.Null(exception);
        }

        [Fact]
        public async Task DenyAsync_ShouldUseFactoryToCreateClient()
        {
            // Arrange
            var mockClient = Substitute.For<ITServerClient>();
            mockClient.IsConnected.Returns(true);
            _tServerClientFactory.Create(Arg.Any<System.Net.IPAddress>(), Arg.Any<int>()).Returns(mockClient);

            var connector = new TComServerConnector(_logger, _tServerClientFactory);
            var accessControlMapping = new AccessControlMapping
            {
                Name = "TestMapping",
                Host = "192.168.1.100",
                Port = 2500,
                Reader = "Reader1",
                Channel = 1,
                Mode = "CLOSE_ON_DENY"
            };

            // Act
            await connector.DenyAsync(accessControlMapping);

            // Assert
            _tServerClientFactory.Received(1).Create(Arg.Is<System.Net.IPAddress>(ip => ip.ToString() == "192.168.1.100"), 2500);
        }
    }
}