public class AeosMember
{
    public long Id { get; set; }
    public string SmartFaceId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }


    public AeosMember(long Id, string SmartFaceId, string FirstName, string LastName)
    {
        this.Id = Id;
        
        /* if(SmartFaceId.Length > 28)
        {
            throw new System.Exception();
        }
        else
        {
            this.SmartFaceId = SmartFaceId;
        } */
        this.SmartFaceId = SmartFaceId;
        this.FirstName = FirstName;
        this.LastName = LastName;

        if (LastName == "")
        {
            this.LastName = " ";
        }
    }

    public string ReadMember()
    {
            return $"Member: {Id},{SmartFaceId},{FirstName},{LastName}";   
    }
}