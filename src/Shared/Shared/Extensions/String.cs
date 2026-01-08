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
    }
}