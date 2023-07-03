using System;

public class SmartFaceMember
{
    public string Id       { get; set; }
    public string FullName     { get; set; }
    public string DisplayName      { get; set; }
    public string Note {get;set;}
    public byte[] ImageData      { get; set; }
    public string ImageDataId   {get;set;}
    
    public SmartFaceMember(string id, string fullName, string displayName, byte[] imageData = null, string note = null, string imageDataId = null)
    {
        this.Id = id;
        this.FullName = fullName;
        this.DisplayName = displayName;
        this.ImageData = imageData;
        this.Note = note;
        this.ImageDataId = imageDataId;

    }

    public override string ToString()
    {
        return $"Member: {Id},{FullName},{DisplayName},{Note},{ImageDataId}";   
    }
}