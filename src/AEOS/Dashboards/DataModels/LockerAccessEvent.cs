using System;

namespace Innovatrics.SmartFace.Integrations.AeosDashboards
{
    /// <summary>
    /// Represents a locker access event from AEOS.
    /// </summary>
    public class LockerAccessEvent
    {
        /// <summary>
        /// The unique identifier of the event.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// The event type ID (236 for LockerAccessEvent).
        /// </summary>
        public long EventTypeId { get; set; }

        /// <summary>
        /// The event type name.
        /// </summary>
        public string? EventTypeName { get; set; }

        /// <summary>
        /// The date and time when the event occurred.
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// The host name of the locker controller.
        /// </summary>
        public string? HostName { get; set; }

        /// <summary>
        /// The name of the access point (locker).
        /// </summary>
        public string? AccesspointName { get; set; }

        /// <summary>
        /// The identifier ID used for the access.
        /// </summary>
        public long? IdentifierId { get; set; }

        /// <summary>
        /// The identifier (badge number) used for the access.
        /// </summary>
        public string? Identifier { get; set; }

        /// <summary>
        /// The carrier (employee) ID.
        /// </summary>
        public long? CarrierId { get; set; }

        /// <summary>
        /// The full name of the carrier (employee).
        /// </summary>
        public string? CarrierFullName { get; set; }

        /// <summary>
        /// Additional integer value from the event.
        /// </summary>
        public int? IntValue { get; set; }

        /// <summary>
        /// Additional attribute data from the event.
        /// </summary>
        public string? Attribute { get; set; }

        public LockerAccessEvent()
        {
        }

        public LockerAccessEvent(
            long id,
            long eventTypeId,
            string? eventTypeName,
            DateTime dateTime,
            string? hostName,
            string? accesspointName,
            long? identifierId,
            string? identifier,
            long? carrierId,
            string? carrierFullName,
            int? intValue,
            string? attribute)
        {
            Id = id;
            EventTypeId = eventTypeId;
            EventTypeName = eventTypeName;
            DateTime = dateTime;
            HostName = hostName;
            AccesspointName = accesspointName;
            IdentifierId = identifierId;
            Identifier = identifier;
            CarrierId = carrierId;
            CarrierFullName = carrierFullName;
            IntValue = intValue;
            Attribute = attribute;
        }
    }
}

