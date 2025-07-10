using System;
using System.Threading.Tasks;
using SmartFace.GoogleCalendarsConnector.Models;

namespace SmartFace.GoogleCalendarsConnector.Service
{
    public interface IGraphQLSubscriptionService
    {
        event Func<StreamGroupAggregation, Task> OnStreamGroupAggregation;
        Task StartAsync();
        Task StopAsync();
    }
} 