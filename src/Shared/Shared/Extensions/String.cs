using System;

namespace System
{
    public static class StringExtensions
    {
        public static string ReplaceAll(this string value, string[] oldValues, string[] newValues)
        {
            if (oldValues.Length != newValues.Length)
            {
                throw new ArgumentOutOfRangeException("Old and New values must have same lenght");
            }

            for (var i = 0; i < oldValues.Length; i++)
            {
                var oldValue = oldValues[i];
                var newValue = newValues[i];

                while (value.IndexOf(oldValue) > -1)
                {
                    value = value.Replace(oldValue, newValue);
                }
            }

            return value;
        }

        public static string RemoveAll(this string value, string[] removeValues)
        {
            for (var i = 0; i < removeValues.Length; i++)
            {
                var oldValue = removeValues[i];

                var j = -1;

                do
                {
                    j = value.IndexOf(oldValue);

                    if (j > -1)
                    {
                        value = value.Remove(j, oldValue.Length);
                    }
                }
                while (j > -1);
            }

            return value;
        }
    }
}