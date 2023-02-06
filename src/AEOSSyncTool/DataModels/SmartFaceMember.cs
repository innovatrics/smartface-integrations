public class SmartFaceMember
{
    public string Id       { get; set; }
    public string FullName     { get; set; }
    public string DisplayName      { get; set; }
    public byte[] ImageData      { get; set; }
    
    public SmartFaceMember(string id, string fullName, string displayName, byte[] imageData = null)
    {
        this.Id = id;
        this.FullName = fullName;
        this.DisplayName = displayName;
        this.ImageData = imageData;
    }

    public override string ToString()
    {
        return $"Member: {Id},{FullName},{DisplayName}";   
    }
}