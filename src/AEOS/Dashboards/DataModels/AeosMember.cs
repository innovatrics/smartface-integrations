public class AeosMember
{
    public long Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }

    public AeosMember(long id, string firstName, string lastName)
    {
        this.Id = id;
        this.FirstName = firstName;
        this.LastName = lastName;

        if (lastName == "")
        {
            this.LastName = " ";
        }
    }

    public override string ToString()
    {
        return $"Member: {Id},{FirstName},{LastName}";
    }
}