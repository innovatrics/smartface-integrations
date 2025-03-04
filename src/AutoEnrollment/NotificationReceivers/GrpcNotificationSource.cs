using System;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AccessController.Clients.Grpc;
using Innovatrics.SmartFace.Integrations.AccessController.Notifications;
using Innovatrics.SmartFace.Integrations.AccessController.Readers;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace SmartFace.AutoEnrollment.NotificationReceivers;

public class GrpcNotificationSource : INotificationSource
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly GrpcReaderFactory _grpcReaderFactory;
    private GrpcNotificationReader _grpcNotificationReader;
    private System.Timers.Timer _accessControllerPingTimer;
    private DateTime _lastGrpcPing;

    public event Func<Models.Notification, Task> OnNotification;

    public GrpcNotificationSource(
        ILogger logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public Task StartAsync()
    {
        _logger.Information("Start receiving gRPC notifications");

        StartReceivingGrpcNotifications();

        StartPingTimer();

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _logger.Information($"Stopping receiving gRPC notifications");

        await StopReceivingGrpcNotificationsAsync();

        _accessControllerPingTimer?.Stop();
        _accessControllerPingTimer?.Dispose();
    }

    private GrpcNotificationReader CreateGrpcReader()
    {
        var grpcHost = _configuration.GetValue<string>("AccessController:Host");
        var grpcPort = _configuration.GetValue<int>("AccessController:Port");

        _logger.Information("gRPC configured to host={host}, port={port}", grpcHost, grpcPort);

        return _grpcReaderFactory.Create(grpcHost, grpcPort);
    }

    private void StartReceivingGrpcNotifications()
    {
        _logger.Information("Start receiving gRPC notifications");

        _grpcNotificationReader = CreateGrpcReader();

        _grpcNotificationReader.OnGrpcGrantedNotification += (GrantedNotification Notification) =>
        {
            _logger.Information("Processing 'GRANTED' notification skipped");
            return Task.CompletedTask;
        };

        _grpcNotificationReader.OnGrpcDeniedNotification += (DeniedNotification notification) =>
        {
            _logger.Information("Processing 'DENIED' notification skipped");

            var notification2 = OnNotification?.Invoke(new Models.Notification
            {
                notification.StreamId,
                FaceId = notification.FaceId,
                TrackletId = notification.TrackletId,
                CropImage = notification.CropImage,
                ReceivedAt = DateTime.UtcNow
            });
        };

        _grpcNotificationReader.OnGrpcBlockedNotification += (BlockedNotification notification) =>
        {
            _logger.Information("Processing 'BLOCKED' notification skipped");
        };

        _grpcNotificationReader.OnGrpcPing += OnGrpcPing;

        _grpcNotificationReader.StartReceiving();
    }

    private async Task StopReceivingGrpcNotificationsAsync()
    {
        await _grpcNotificationReader.DisposeAsync();
    }

    private Task OnGrpcPing(DateTime sentAt)
    {
        _logger.Debug("gRPC ping received");
        _lastGrpcPing = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    private void StartPingTimer()
    {
        _lastGrpcPing = DateTime.UtcNow;

        _accessControllerPingTimer = new System.Timers.Timer
        {
            Interval = 5000
        };

        _accessControllerPingTimer.Elapsed += async (_, e) =>
        {
            var timeDiff = DateTime.UtcNow - _lastGrpcPing;

            _logger.Debug("Timer ping check: {Ms} ms", timeDiff.TotalMilliseconds);

            if (timeDiff.TotalSeconds > 15)
            {
                _logger.Warning("gRPC ping not received, last {Sec} sec ago", timeDiff.TotalSeconds);
            }

            if (timeDiff.TotalSeconds > 60)
            {
                _logger.Error("gRPC ping timeout reached");
                _logger.Information("gRPC restarting");

                _accessControllerPingTimer.Stop();

                await StopReceivingGrpcNotificationsAsync();
                StartReceivingGrpcNotifications();

                _accessControllerPingTimer.Start();

                _logger.Information("gRPC restarted");
            }
        };

        _accessControllerPingTimer.Start();
    }
}