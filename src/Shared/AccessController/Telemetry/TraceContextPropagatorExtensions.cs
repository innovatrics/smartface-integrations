using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Serilog;

namespace AccessController.Telemetry
{
    internal static class TraceContextPropagatorExtensions
    {
        internal static PropagationContext ExtractFromDictionary(this TraceContextPropagator traceContextPropagator,
            IDictionary<string, string> carrier)
        {
            return traceContextPropagator.Extract(default, carrier, ExtractTraceContext);
        }

        internal static void InjectIntoHeaders(this TraceContextPropagator traceContextPropagator,
            ActivityContext activityContext, IDictionary<string, string> headers)
        {
            traceContextPropagator.Inject(
                new PropagationContext(activityContext, Baggage.Current),
                headers,
                static (carrier, key, value) =>
                {
                    carrier[key] = value;
                });
        }

        private static IEnumerable<string> ExtractTraceContext(IDictionary<string, string> props, string key)
        {
            try
            {
                if (props == null)
                    return [];

                if (props.TryGetValue(key, out var value))
                {
                    return [value];
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to extract trace context");
            }

            return [];
        }
    }
}
