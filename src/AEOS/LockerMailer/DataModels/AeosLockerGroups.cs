using System.Collections.Generic;

public class AeosLockerGroups
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public List<long> LockerIds { get; set; }
    public long LockerBehaviourTemplate { get; set; }
    public string LockerFunction { get; set; }
    
    public AeosLockerGroups(long id, string name, string description, List<long> lockerIds, long lockerBehaviourTemplate, string lockerFunction)
    {
        this.Id = id;
        this.Name = name;
        this.Description = description;
        this.LockerIds = lockerIds ?? new List<long>();
        this.LockerBehaviourTemplate = lockerBehaviourTemplate;
        this.LockerFunction = lockerFunction;
    }

    public override string ToString()
    {
        return $"Locker Group: {Id},{Name},{Description},{LockerFunction}";
    }
}