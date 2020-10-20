using System;

namespace Fergun.Extensions
{
    public static class TimeSpanExtensions
    {
        /// <summary>
        /// Formats a TimeSpan to mm:ss or hh:mm:ss.
        /// </summary>
        public static string ToShortForm(this TimeSpan t) => t.ToString((t.Hours > 0 ? @"hh\:" : "") + @"mm\:ss");

        /// <summary>
        /// Formats a TimeSpan to ss, mm ss, hh mm ss or dd hh mm ss.
        /// </summary>
        public static string ToShortForm2(this TimeSpan t)
        {
            string format = "d'd 'h'h 'm'm 's's'";
            if (t.Days == 0)
                format = "h'h 'm'm 's's'";
            if (t.Days == 0 && t.Hours == 0)
                format = "m'm 's's'";
            if (t.Days == 0 && t.Hours == 0 && t.Minutes == 0)
                format = "s's'";
            return t.ToString(format);
        }
    }
}