using System;

namespace System
{
    public static class DateTimeExtensions
    {
        public static DateTime GetLocalDateTime(this DateTime dateTime)
        {
            var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
            return dateTime.Add(offset);
        }
    }
}