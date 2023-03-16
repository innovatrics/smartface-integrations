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

        public static string GetFirstName(string fullName)
        {
            var names = fullName.Split(' ');
            if (names.Length > 0)
            {
                return fullName.Split(' ')[0];
            }
            else
            {
                return "";
            }
        }

        public static string GetLastName(string fullName)
        {
            var names = fullName.Split(' ');
            string returnValue = "";

            if (names.Length > 1)
            {
                for (int x = 1; x < names.Length; x++)
                {
                    returnValue = returnValue + " " + (string)names[x];
                }
                return returnValue.Trim();
            }
            else
            {
                return "";
            }
        }

        public static string JoinNames(string firstName, string lastName)
        {
            return firstName + " " + lastName;
        }

        public static bool CompareUsers(AeosMember aeosMember, SmartFaceMember smartFaceMember)
        {
            if (smartFaceMember.FullName != aeosMember.FirstName + " " + aeosMember.LastName)
            {
                return false;
            }

            return true;
        }

    }
}