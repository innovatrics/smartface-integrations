using System;

public class AeosIdentifier
{
    public long Id { get; set; }
    public string BadgeNumber { get; set; }
    public bool Blocked { get; set; }
    public long CarrierId { get; set; }
    public long IdentifierType { get; set; }
    public AeosIdentifier(long id, string badgeNumber, bool blocked, long carrierId, long identifierType)
    {
        this.Id = id;

        this.BadgeNumber = badgeNumber;
        this.Blocked = blocked;
        this.CarrierId = carrierId;
        this.IdentifierType = identifierType;
    }

    public override string ToString()
    {
        return $"Identifier: {Id},{BadgeNumber},{Blocked},{CarrierId},{IdentifierType}";
    }
}
