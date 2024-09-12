using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Models;
using Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Factories;

namespace Innovatrics.SmartFace.Integrations.AutoEnrollPlugin.Services
{
    public class ValidationServiceFactory : IValidationServiceFactory
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;

        public ValidationServiceFactory(
            ILogger logger,
            IConfiguration configuration
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public IValidationService Create(string streamId)
        {
            // var cameraToStreamMappings = this.getCameraMappings(streamId);

            // if (cameraToStreamMappings.Length == 0)
            // {
            //     this.logger.Warning("Stream {streamId} has not any mapping to AccessControl", notification.StreamId);
            //     return;
            // }

            // foreach (var cameraToStreamMapping in cameraToStreamMappings)
            // {
            //     this.logger.Warning("Handling mapping {type}", cameraToStreamMapping.Type);

            //     if (cameraToStreamMapping.WatchlistExternalIds != null)
            //     {
            //         if (cameraToStreamMapping.WatchlistExternalIds.Length > 0 && !cameraToStreamMapping.WatchlistExternalIds.Contains(notification.WatchlistExternalId))
            //         {
            //             this.logger.Warning("Watchlist {watchlistExternalId} has no right to enter through this gate {streamId}.", notification.WatchlistExternalId, notification.StreamId);
            //             return;
            //         }
            //     }

            //     string accessControlUser = null;

            //     var notificationSource = this.notificationSourceFactory.Create(cameraToStreamMapping.Type);

            //     if (cameraToStreamMapping.UserResolver != null)
            //     {
            //         var userResolver = this.userResolverFactory.Create(cameraToStreamMapping.UserResolver);

            //         accessControlUser = await userResolver.ResolveUserAsync(notification.WatchlistMemberId);

            //         this.logger.Information("Resolved {wlMember} to {accessControlUser}", notification.WatchlistMemberFullName, accessControlUser);

            //         if (accessControlUser == null)
            //         {
            //             return;
            //         }
            //     }

            //     await notificationSource.OpenAsync(cameraToStreamMapping, accessControlUser);
            // }
        }

        private StreamMapping[] getCameraMappings(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var streamMapping = this.configuration.GetSection("StreamMapping").Get<StreamMapping[]>();

            if (streamMapping == null)
            {
                return new StreamMapping[] { };
            }

            return streamMapping
                        .Where(w => w.StreamId == streamGuid)
                        .ToArray();
        }

        private StreamMapping[] getAllCameraMappings()
        {
            return this.configuration
                            .GetSection("StreamMapping")
                            .Get<StreamMapping[]>();
        }
    }
}
