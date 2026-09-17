using System;
using System.Collections.Generic;

public class SmartFaceMember
{
    public string Id { get; set; }
    public string FullName { get; set; }
    public string DisplayName { get; set; }
    public string Note { get; set; }
    public byte[] ImageData { get; set; }
    public string ImageDataId { get; set; }
    public string PhotoHash { get; set; }
    public IDictionary<string, string> Labels { get; set; }

    public SmartFaceMember(string id, string fullName, string displayName, byte[] imageData = null, string photoHash = null, string imageDataId = null, string note = null, IDictionary<string, string> labels = null)
    {
        this.Id = id;
        this.FullName = fullName;
        this.DisplayName = displayName;
        this.ImageData = imageData;
        this.PhotoHash = photoHash;
        this.ImageDataId = imageDataId;
        this.Note = note;
        this.Labels = labels;

    }

    public override string ToString()
    {
        return $"Member: {Id},{FullName},{DisplayName},{PhotoHash},{ImageDataId}";
    }
}
