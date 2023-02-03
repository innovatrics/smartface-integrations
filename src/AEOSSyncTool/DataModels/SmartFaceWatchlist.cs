public class SmartFaceWatchlist
{
    public string Id       { get; set; }
    public string FullName     { get; set; }
    public string DisplayName      { get; set; }

    public SmartFaceWatchlist(string Id, string fullName, string displayName)
    {
        this.Id = Id;
        this.FullName = fullName;
        this.DisplayName = displayName;
    }

    public string ReadWatchlist()
    {
        return $"Member: {Id},{FullName},{DisplayName}";   
    }
}