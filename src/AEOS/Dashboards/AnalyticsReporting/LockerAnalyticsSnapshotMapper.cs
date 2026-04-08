using System;
using System.Globalization;
using System.Linq;

using Innovatrics.SmartFace.Integrations.AeosDashboards;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards.AnalyticsReporting
{
    /// <summary>
    /// Maps locker analytics to the POST /api/v1/lockers/snapshot contract.
    /// OpenAPI reference: https://bh-analytics-1u.ba.innovatrics.net/swagger (when available; swagger.json may 500 on some deployments).
    /// </summary>
    internal static class LockerAnalyticsSnapshotMapper
    {
        private const string DefaultFunction = "personal";

        /// <summary>UTC ISO-8601 second precision, e.g. 2026-03-25T10:35:00Z</summary>
        private static string FormatUtcIsoSeconds(DateTime utc) =>
            utc.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "Z";

        /// <summary>API schema typically expects lowercase enum values (e.g. personal, shared); AEOS returns Title Case.</summary>
        private static string NormalizeFunction(string? function)
        {
            if (string.IsNullOrWhiteSpace(function))
                return DefaultFunction;
            return function.Trim().ToLowerInvariant();
        }

        private const string SharedFunction = "shared";

        public static LockerAnalyticsSnapshotPayload FromAnalytics(LockerAnalytics analytics, bool forcePersonalLockType = false)
        {
            var snapshotAt = FormatUtcIsoSeconds(DateTime.UtcNow);
            var groups = analytics.Groups.Select(g =>
            {
                var lockers = g.AllLockers.Select(l => new LockerAnalyticsSnapshotLocker
                {
                    Name = l.Name ?? string.Empty,
                    LastUsed = l.LastUsed.HasValue ? FormatUtcIsoSeconds(l.LastUsed.Value.ToUniversalTime()) : null,
                    AssignedEmployeeName = string.IsNullOrWhiteSpace(l.AssignedEmployeeName) ? null : l.AssignedEmployeeName.Trim(),
                    AssignedEmployeeEmail = string.IsNullOrWhiteSpace(l.AssignedEmployeeEmail) ? null : l.AssignedEmployeeEmail.Trim()
                }).ToList();

                // Coherence with serialized rows: assigned count must match what validators infer from employee fields
                // (AEOS AssignedTo can be set while name/email are missing from cache).
                var assignedVisible = lockers.Count(l =>
                    !string.IsNullOrWhiteSpace(l.AssignedEmployeeEmail) || !string.IsNullOrWhiteSpace(l.AssignedEmployeeName));

                var function = NormalizeFunction(g.Function);
                if (forcePersonalLockType && string.Equals(function, SharedFunction, StringComparison.Ordinal))
                    function = DefaultFunction;

                return new LockerAnalyticsSnapshotGroup
                {
                    Name = g.Name ?? string.Empty,
                    Function = function,
                    TotalLockers = lockers.Count,
                    AssignedLockers = assignedVisible,
                    Lockers = lockers
                };
            }).ToList();

            return new LockerAnalyticsSnapshotPayload
            {
                Groups = groups,
                SnapshotAt = snapshotAt
            };
        }
    }
}
