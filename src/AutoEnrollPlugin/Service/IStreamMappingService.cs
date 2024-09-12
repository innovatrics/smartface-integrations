using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public interface IStreamMappingService
    {
        ICollection<StreamMapping> CreateMappings(string streamId);
    }
}