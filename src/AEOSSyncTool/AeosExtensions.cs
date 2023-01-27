using System.Linq;
using ServiceReference;

namespace Innovatrics.SmartFace.Integrations.AEOSSync 
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


    }
}