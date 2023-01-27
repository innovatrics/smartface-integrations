public class AeosMember
{
    public long Id { get; set; }
    public string SmartFaceId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }


    public AeosMember(long Id, string SmartFaceId, string FirstName, string LastName)
    {
        this.Id = Id;
        this.SmartFaceId = SmartFaceId;
        this.FirstName = FirstName;
        this.LastName = LastName;
    }
}