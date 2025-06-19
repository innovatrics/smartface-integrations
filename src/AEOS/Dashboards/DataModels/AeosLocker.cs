using System;

public class AeosLockers
{
    public long Id { get; set; }
    public string Name { get; set; }
    public DateTime LastUsed { get; set; }
    public long AssignedTo { get; set; }
    public string Location { get; set; }
    public string HostName { get; set; }
    public bool Online { get; set; }
    public string LockerFunction { get; set; }
    public long LockerGroupId { get; set; }
    
    public AeosLockers(long id, string name, DateTime lastUsed, long assignedTo, string location, string hostName, bool online, string lockerFunction, long lockerGroupId)
    {
        this.Id = id;

        this.Name = name;
        this.LastUsed = lastUsed;
        this.AssignedTo = assignedTo;
        this.Location = location;
        this.HostName = hostName;
        this.Online = online;
        this.LockerFunction = lockerFunction;
        this.LockerGroupId = lockerGroupId; 
    }

    public override string ToString()
    {
        return $"Locker: {Id},{Name},{LastUsed},{AssignedTo},{Location},{HostName},{Online},{LockerFunction},{LockerGroupId}";
    }
}