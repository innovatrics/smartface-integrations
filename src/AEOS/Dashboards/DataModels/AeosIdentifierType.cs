using System;

public class AeosIdentifierType
{
    public long Id { get; set; }
    public string Name { get; set; }
    
    public AeosIdentifierType(long id, string name)
    {
        this.Id = id;

        this.Name = name;
    }

    public override string ToString()
    {
        return $"IdentifierType: {Id},{Name}";
    }
}
