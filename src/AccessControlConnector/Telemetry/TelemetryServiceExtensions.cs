using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Telemetry
{
    public static class TelemetryServiceExtensions
    {
        public static IServiceCollection AddTelemetry(this IServiceCollection services,
            IConfiguration configuration)
        {
            var tracingConfig = configuration
                .GetSection(TracingConfig.SECTION_NAME)
                .Get<TracingConfig>();

            if (!tracingConfig.Enabled)
            {
                return services;
            }

            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(
                        serviceName: Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
                            ?? AccessControlTelemetry.ServiceName,
                        serviceVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                    )
                    .AddAttributes(new[]
                    {
                        new KeyValuePair<string, object>("deployment.environment",
                            Environment.GetEnvironmentVariable("DEPLOYMENT_ENVIRONMENT") ?? "development")
                    }))
                .WithTracing(tracing => tracing
                    .AddSource(AccessControlTelemetry.ActivitySourceName)
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter());

            return services;
        }
    }
}
