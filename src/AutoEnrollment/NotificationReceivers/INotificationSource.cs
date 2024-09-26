using System;
using System.Threading.Tasks;
using SmartFace.AutoEnrollment.Models;

namespace SmartFace.AutoEnrollment.NotificationReceivers
{
    public interface INotificationSource
    {
        event Func<Notification, Task> OnNotification;

        Task StartAsync();
        Task StopAsync();
    }
}   