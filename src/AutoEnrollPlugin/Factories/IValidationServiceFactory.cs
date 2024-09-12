using System;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public interface IValidationServiceFactory
    {
        IValidationService Create(string streamId);
    }
}