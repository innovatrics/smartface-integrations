using System.Collections.Generic;

public class AeosMember
{
    public long Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Identifier { get; set; }
    public IList<AeosLockers> AssignedLockers { get; set; }


    public AeosMember(long id, string firstName, string lastName, string email = null, string identifier = null)
    {
        this.Id = id;
        this.FirstName = firstName;
        this.LastName = lastName;
        this.Email = email;
        this.Identifier = identifier;
        this.AssignedLockers = new List<AeosLockers>();

        if (lastName == "")
        {
            this.LastName = " ";
        }
    }

    public override string ToString()
    {
        return $"Member: {Id},{FirstName},{LastName},{Email},{Identifier}";
    }
}