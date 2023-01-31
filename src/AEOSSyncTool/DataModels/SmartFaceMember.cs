public class SmartFaceMember
{
    public string Id       { get; set; }
    public string fullName     { get; set; }
    public string displayName      { get; set; }

    public SmartFaceMember(string Id, string fullName, string displayName)
    {
        this.Id = Id;
        this.fullName = fullName;
        this.displayName = displayName;
    }

    public string ReadMember()
    {
        return $"Member: {Id},{fullName},{displayName}";   
    }
}