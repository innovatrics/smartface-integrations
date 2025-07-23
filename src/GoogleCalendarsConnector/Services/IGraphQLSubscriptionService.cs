using System;
using System.Threading.Tasks;
using SmartFace.GoogleCalendarsConnector.Models;

namespace SmartFace.GoogleCalendarsConnector.Services
{
    public interface IGraphQLSubscriptionService
    {
        event Func<StreamGroupAggregation, Task> OnStreamGroupAggregation;
        Task StartAsync();
        Task StopAsync();
    }
} 