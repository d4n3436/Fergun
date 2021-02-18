using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fergun.Extensions
{
    public static class StringExtensions
    {
        public static string Repeat(this string input, int count)
        {
            return string.Join(string.Empty, Enumerable.Repeat(input, count));
        }

        public static string RepeatToLength(this string input, int length)
        {
            return Repeat(input, length / input.Length + 1).Truncate(length);
        }

        public static string Reverse(this string input)
        {
            return new string(Enumerable.Reverse(input).ToArray());
        }

        public static string ReverseEachLine(this string input)
        {
            return string.Join("\r\n", input.Split('\r', '\n').Reverse());
        }

        /// <summary>
        /// Converts a string to its full width form.
        /// </summary>
        /// <param name="input">The string to convert.</param>
        public static string ToFullWidth(this string input)
        {
            var sb = new StringBuilder(input.Length);
            foreach (char ch in input)
            {
                if (0x21 <= ch && ch <= 0x7E) // ASCII chars, excluding space
                    sb.Append((char)(ch + 0xFEE0));
                else if (ch == 0x20)
                    sb.Append((char)0x3000);
                else
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Truncates a string to the specified length.
        /// </summary>
        /// <param name="value">The string to truncate.</param>
        /// <param name="maxLength">The maximum length of the string.</param>
        /// <returns>The truncated string.</returns>
        public static string Truncate(this string value, int maxLength)
        {
            return value?.Substring(0, Math.Min(value.Length, maxLength));
        }

        public static bool ContainsAny(this string input, IEnumerable<string> containsKeywords, StringComparison comparisonType)
        {
            return containsKeywords.Any(keyword => input.Contains(keyword, comparisonType));
        }

        public static bool IsBase64(this string s)
        {
            Span<byte> buffer = new Span<byte>(new byte[s.Length]);
            return Convert.TryFromBase64String(s, buffer, out int _);
        }

        public static string ToTitleCase(this string s)
        {
            return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + (s.Length > 1 ? s.Substring(1).ToLowerInvariant() : "");
        }

        public static IEnumerable<string> SplitBySeparatorWithLimit(this string text, char separator, int maxLength)
        {
            var sb = new StringBuilder();
            foreach (var part in text.Split(separator))
            {
                if (part.Length + sb.Length >= maxLength)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }

                sb.Append(part);
                sb.Append(separator);
            }
            if (sb.Length != 0)
            {
                yield return sb.ToString();
            }
        }

        public static int ToColor(this string str)
        {
            int hash = 0;
            foreach (char ch in str)
            {
                hash = ch + ((hash << 5) - hash);
            }
            return hash;
            //string c = (hash & 0x00FFFFFF).ToString("X4").ToUpperInvariant();

            //return "00000".Substring(0, 6 - c.Length) + c;
        }
    }
}