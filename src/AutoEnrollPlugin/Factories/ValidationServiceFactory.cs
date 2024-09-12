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
            // var cameraToAccessControlMappings = this.getCameraMappings(streamId);

            // if (cameraToAccessControlMappings.Length == 0)
            // {
            //     this.logger.Warning("Stream {streamId} has not any mapping to AccessControl", notification.StreamId);
            //     return;
            // }

            // foreach (var cameraToAccessControlMapping in cameraToAccessControlMappings)
            // {
            //     this.logger.Warning("Handling mapping {type}", cameraToAccessControlMapping.Type);

            //     if (cameraToAccessControlMapping.WatchlistExternalIds != null)
            //     {
            //         if (cameraToAccessControlMapping.WatchlistExternalIds.Length > 0 && !cameraToAccessControlMapping.WatchlistExternalIds.Contains(notification.WatchlistExternalId))
            //         {
            //             this.logger.Warning("Watchlist {watchlistExternalId} has no right to enter through this gate {streamId}.", notification.WatchlistExternalId, notification.StreamId);
            //             return;
            //         }
            //     }

            //     string accessControlUser = null;

            //     var notificationSource = this.notificationSourceFactory.Create(cameraToAccessControlMapping.Type);

            //     if (cameraToAccessControlMapping.UserResolver != null)
            //     {
            //         var userResolver = this.userResolverFactory.Create(cameraToAccessControlMapping.UserResolver);

            //         accessControlUser = await userResolver.ResolveUserAsync(notification.WatchlistMemberId);

            //         this.logger.Information("Resolved {wlMember} to {accessControlUser}", notification.WatchlistMemberFullName, accessControlUser);

            //         if (accessControlUser == null)
            //         {
            //             return;
            //         }
            //     }

            //     await notificationSource.OpenAsync(cameraToAccessControlMapping, accessControlUser);
            // }
        }

        private AccessControlMapping[] getCameraMappings(string streamId)
        {
            if (!Guid.TryParse(streamId, out var streamGuid))
            {
                throw new InvalidOperationException($"{nameof(streamId)} is expected as GUID");
            }

            var accessControlMapping = this.configuration.GetSection("AccessControlMapping").Get<AccessControlMapping[]>();

            if (accessControlMapping == null)
            {
                return new AccessControlMapping[] { };
            }

            return accessControlMapping
                        .Where(w => w.StreamId == streamGuid)
                        .ToArray();
        }

        private AccessControlMapping[] getAllCameraMappings()
        {
            return this.configuration
                            .GetSection("AccessControlMapping")
                            .Get<AccessControlMapping[]>();
        }
    }
}
