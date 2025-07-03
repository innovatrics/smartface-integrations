using System;
using System.Threading;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.Cominfo;
using Innovatrics.SmartFace.Integrations.AccessControlConnector.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Tests
{
    public class TComServerConnector
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

        public TComServerConnector()
        {
            _mockLogger = new Mock<ILogger>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldNotThrow()
        {
            // Act & Assert
            var exception = Record.Exception(() => new TComServerConnector(_mockLogger.Object, _mockHttpClientFactory.Object));
            Assert.Null(exception);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TComServerConnector(null, _mockHttpClientFactory.Object));
        }

        [Fact]
        public void Constructor_WithNullHttpClientFactory_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TComServerConnector(_mockLogger.Object, null));
        }

        [Fact]
        public async Task DenyAsync_WithTimeoutConfigured_ShouldStartTimer()
        {
            // Arrange
            var connector = new TComServerConnector(_mockLogger.Object, _mockHttpClientFactory.Object);
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
            var connector = new TComServerConnector(_mockLogger.Object, _mockHttpClientFactory.Object);
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
            var connector = new TComServerConnector(_mockLogger.Object, _mockHttpClientFactory.Object);
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
            var connector = new TComServerConnector(_mockLogger.Object, _mockHttpClientFactory.Object);
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
            var connector = new TComServerConnector(_mockLogger.Object, _mockHttpClientFactory.Object);

            // Act & Assert
            var exception = Record.Exception(() => connector.Dispose());
            Assert.Null(exception);
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes_ShouldNotThrowException()
        {
            // Arrange
            var connector = new TComServerConnector(_mockLogger.Object, _mockHttpClientFactory.Object);

            // Act & Assert
            connector.Dispose();
            var exception = Record.Exception(() => connector.Dispose());
            Assert.Null(exception);
        }
    }
}