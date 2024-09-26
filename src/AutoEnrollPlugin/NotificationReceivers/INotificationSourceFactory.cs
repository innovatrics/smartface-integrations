using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Sources;

namespace AutoEnrollPlugin.Sources
{
    public interface INotificationSourceFactory
    {
        INotificationSource Create(string type);
    }
}