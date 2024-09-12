using System;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Sources;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Factories
{
    public interface INotificationSourceFactory
    {
        INotificationSource Create(string type);
    }
}