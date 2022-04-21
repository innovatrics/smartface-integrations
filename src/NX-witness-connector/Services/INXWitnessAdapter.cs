using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;

using Innovatrics.SmartFace.Integrations.Shared.ZeroMQ;
using Innovatrics.SmartFace.Models.Notifications;

namespace Innovatrics.SmartFace.Integrations.NXWitnessConnector
{
    internal interface INXWitnessAdapter
    {
    }
}