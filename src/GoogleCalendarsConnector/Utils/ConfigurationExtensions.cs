using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Configuration;
using Serilog;
using SmartFace.GoogleCalendarsConnector.Models;
using SmartFace.GoogleCalendarsConnector.Services;

namespace SmartFace.GoogleCalendarsConnector.Services
{
    public static class ConfigurationExtensions
    {
        public static StreamGroupMapping[] GetStreamGroupsMapping(this IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var streamGroupsMapping = configuration.GetSection("StreamGroupsMapping").Get<StreamGroupMapping[]>();

            if (streamGroupsMapping == null)
            {
                return new StreamGroupMapping[] { };
            }

            return streamGroupsMapping;
        }
    }
}
