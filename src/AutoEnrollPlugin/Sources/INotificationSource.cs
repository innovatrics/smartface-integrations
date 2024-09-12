using System;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Sources
{
    public interface INotificationSource
    {
        event Func<Notification, Task> OnNotification;

        Task StartAsync();

        Task StopAsync();
    }
}   