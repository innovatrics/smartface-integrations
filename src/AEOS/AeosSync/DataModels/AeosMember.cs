public class AeosMember
{
    public long Id { get; set; }
    public string SmartFaceId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public byte[] ImageData { get; set; }


    public AeosMember(long id, string smartFaceId, string firstName, string lastName, byte[] imageData = null)
    {
        this.Id = id;
        
        this.SmartFaceId = smartFaceId;
        this.FirstName = firstName;
        this.LastName = lastName;

        if (lastName == "")
        {
            this.LastName = " ";
        }

        this.ImageData = imageData;
    }

    public override string ToString()
    {
            return $"Member: {Id},{SmartFaceId},{FirstName},{LastName}";   
    }
}