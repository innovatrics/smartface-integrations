using System;

public class AeosLockers
{
    public long Id { get; set; }
    public string Name { get; set; }
    public DateTime LastUsed { get; set; }
    public long AssignedTo { get; set; }


    public AeosLockers(long id, string name, DateTime lastUsed, long assignedTo)
    {
        this.Id = id;

        this.Name = name;
        this.LastUsed = lastUsed;
        this.AssignedTo = assignedTo;

    }

    public override string ToString()
    {
        return $"Locker: {Id},{Name},{LastUsed},{AssignedTo}";
    }
}