namespace SmartFace.AutoEnrollment.NotificationReceivers
{
    public interface INotificationSourceFactory
    {
        INotificationSource Create(string type);
    }
}