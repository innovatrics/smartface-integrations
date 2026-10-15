using System;
using System.Collections.Generic;
using System.Linq;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    /// <summary>
    /// State of one locker read directly from AEOS, bypassing the cached analytics.
    /// </summary>
    public sealed class LiveLockerState
    {
        public long Id { get; }

        /// <summary>Carrier the locker is assigned to, or null when AEOS reports none.</summary>
        public long? AssignedTo { get; }

        /// <summary>
        /// AEOS LastUsed. It is a date that AEOS sets when the locker gets allocated and clears
        /// when the locker is released, so it is null for every free locker.
        /// </summary>
        public DateTime? LastUsed { get; }

        public LiveLockerState(long id, long? assignedTo, DateTime? lastUsed)
        {
            Id = id;
            AssignedTo = assignedTo;
            LastUsed = lastUsed;
        }

        public bool IsAssigned => AssignedTo.HasValue;

        /// <summary>
        /// AEOS still holds the locker as allocated although no carrier is assigned to it.
        /// AEOS rejects addCarrierLockerAuthorization for such a locker
        /// ("Add locker authorizations failed") until the locker is released.
        /// </summary>
        public bool IsHeldWithoutOwner => !AssignedTo.HasValue && LastUsed.HasValue;

        /// <summary>
        /// Builds the state of <paramref name="requestedId"/> from a findLocker result. Returns null
        /// unless the result contains exactly one locker with that id, so a search that AEOS did
        /// not filter by id can never be mistaken for the requested locker.
        /// </summary>
        public static LiveLockerState? FromAeos(IEnumerable<ServiceReference.LockerInfo?>? lockers, long requestedId)
        {
            var matches = lockers?
                .Where(l => l != null && l.Id == requestedId)
                .ToList();

            if (matches == null || matches.Count != 1)
            {
                return null;
            }

            var locker = matches[0]!;

            // AEOS leaves AssignedTo and LastUsed out for a free locker; the cached analytics
            // treat a missing value the same way (AssignedTo 0, LastUsed DateTime.MinValue).
            long? assignedTo = locker.AssignedToSpecified && locker.AssignedTo > 0 ? locker.AssignedTo : null;
            DateTime? lastUsed = locker.LastUsedSpecified && locker.LastUsed != DateTime.MinValue ? locker.LastUsed : null;

            return new LiveLockerState(locker.Id, assignedTo, lastUsed);
        }
    }
}
