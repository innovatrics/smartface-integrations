using System.Linq;
using ServiceReference;

namespace Innovatrics.SmartFace.Integrations.AeosSync
{
    public static class AeosExtensions
    {
        public static string GetFreefieldValue(this EmployeeInfo employeeInfo, string freefieldName, string defaultValue = null)
        {
            var freeField = employeeInfo.Freefield?.FirstOrDefault(f => f.Name == freefieldName);
            if (freeField == null)
                return defaultValue;
            return freeField.value;
        }

        public static string GetFirstName(string fullName, string firstNameOrder)
        {
            var names = fullName.Split(' ');
            if (names.Length > 0)
            {
                if(firstNameOrder == "first")
                {
                    return names[0];
                }
                else if(firstNameOrder == "last")
                {
                    return names[names.Length -1];
                }
                else
                {
                    return "";
                }
                
            }
            else
            {
                return "";
            }
        }

        public static string GetLastName(string fullName, string firstNameOrder)
        {
            var names = fullName.Split(' ');
            string returnValue = "";

            if (names.Length > 1)
            {
                if(firstNameOrder == "first")
                {
                    for (int x = 1; x < names.Length; x++)
                    {
                        returnValue = returnValue + " " + (string)names[x];
                    }
                    return returnValue.Trim();
                }
                else if(firstNameOrder == "last")
                {
                    for (int x = 0; x < names.Length -1; x++)
                    {
                        returnValue = returnValue + " " + (string)names[x];
                    }
                    return returnValue.Trim();
                }
                else
                {
                    return returnValue;
                }
                
            }
            else
            {
                return returnValue;
            }
        }

        public static string JoinNames(string firstName, string lastName,string firstNameOrder)
        {
            if(firstNameOrder == "first")
            {
                return firstName + " " + lastName;
            }
            else if(firstNameOrder == "last")
            {
                return lastName + " " + firstName;
            }
            else
            {
                throw new System.Exception($"Incorrect value for firstNameOrder was used: {firstNameOrder}.");
            }
            
        }

        public static bool CompareUsers(AeosMember aeosMember, SmartFaceMember smartFaceMember, string firstNameOrder)
        {
            if(firstNameOrder == "first")
            {
                if (smartFaceMember.FullName != aeosMember.FirstName + " " + aeosMember.LastName)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else if(firstNameOrder == "last")
            {
                if (smartFaceMember.FullName != aeosMember.LastName + " " + aeosMember.FirstName)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return false;
            }

        }

    }
}