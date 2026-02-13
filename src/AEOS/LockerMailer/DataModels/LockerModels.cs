using System;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.LockerMailer.DataModels
{
    public class AeosLockers
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime LastUsed { get; set; }
        public int AssignedTo { get; set; }
        public string Location { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public bool Online { get; set; }
        public string LockerFunction { get; set; } = string.Empty;
        public int LockerGroupId { get; set; }

        public AeosLockers() { }

        public AeosLockers(int id, string name, DateTime lastUsed, int assignedTo, string location, string hostName, bool online, string lockerFunction, int lockerGroupId)
        {
            Id = id;
            Name = name;
            LastUsed = lastUsed;
            AssignedTo = assignedTo;
            Location = location;
            HostName = hostName;
            Online = online;
            LockerFunction = lockerFunction;
            LockerGroupId = lockerGroupId;
        }
    }

    public class KeilaLockers
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime LastUsed { get; set; }
        public int AssignedTo { get; set; }
        public string Location { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public bool Online { get; set; }
        public string LockerFunction { get; set; } = string.Empty;
        public int LockerGroupId { get; set; }

        public KeilaLockers() { }

        public KeilaLockers(int id, string name, DateTime lastUsed, int assignedTo, string location, string hostName, bool online, string lockerFunction, int lockerGroupId)
        {
            Id = id;
            Name = name;
            LastUsed = lastUsed;
            AssignedTo = assignedTo;
            Location = location;
            HostName = hostName;
            Online = online;
            LockerFunction = lockerFunction;
            LockerGroupId = lockerGroupId;
        }
    }

    public class AeosIdentifierType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class AeosIdentifier
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
        public int IdentifierTypeId { get; set; }
        public int PersonId { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Expires { get; set; }
    }

    public class AeosMember
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public List<AeosIdentifier> Identifiers { get; set; } = new List<AeosIdentifier>();
    }

    public class AeosLockerGroups
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ParentGroupId { get; set; }
    }

    public class LockerAnalytics
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }

    public class LockerAssignmentChange
    {
        public int Id { get; set; }
        public int LockerId { get; set; }
        public int PreviousAssignedTo { get; set; }
        public int NewAssignedTo { get; set; }
        public DateTime ChangeTimestamp { get; set; }
        public string ChangeType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a locker access event from the Dashboards API.
    /// </summary>
    public class LockerAccessEvent
    {
        public long Id { get; set; }
        public long EventTypeId { get; set; }
        public string? EventTypeName { get; set; }
        public DateTime DateTime { get; set; }
        public string? HostName { get; set; }
        public string? AccesspointName { get; set; }
        public long? IdentifierId { get; set; }
        public string? Identifier { get; set; }
        public long? CarrierId { get; set; }
        public string? CarrierFullName { get; set; }
        public int? IntValue { get; set; }
        public string? Attribute { get; set; }
    }
}
