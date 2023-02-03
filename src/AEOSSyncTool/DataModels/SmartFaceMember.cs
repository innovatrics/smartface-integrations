public class SmartFaceMember
{
    public string Id       { get; set; }
    public string fullName     { get; set; }
    public string displayName      { get; set; }
    public string ImageBase64      { get; set; }
    
    public SmartFaceMember(string Id, string fullName, string displayName, string ImageBase64 = null)
    {
        this.Id = Id;
        this.fullName = fullName;
        this.displayName = displayName;
        this.ImageBase64 = ImageBase64;
    }

    public string ReadMember()
    {
        return $"Member: {Id},{fullName},{displayName},{ImageBase64}";   
    }
}