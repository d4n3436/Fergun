using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Fergun.Extensions
{
    public static class StringExtensions
    {
        public static string Repeat(this string input, int count)
        {
            return string.Join(string.Empty, Enumerable.Repeat(input, count));
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
            string output = "";
            foreach (char currentchar in input.ToCharArray())
            {
                if (0x21 <= currentchar && currentchar <= 0x7E) // ASCII chars, excluding space
                    output += (char)(currentchar + 0xFEE0);
                else if (currentchar == 0x20)
                    output += (char)0x3000;
                else
                    output += currentchar;
            }
            return output;
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
            string current = "";
            List<string> list = new List<string>();

            foreach (var part in text.Split(separator))
            {
                if (part.Length + current.Length >= maxLength)
                {
                    list.Add(current);
                    current = part + separator;
                }
                else
                {
                    current += part + separator;
                }
            }
            if (!string.IsNullOrEmpty(current))
            {
                list.Add(current);
            }

            return list;
        }

        public static int ToColor(this string str)
        {
            int hash = 0;
            foreach (char ch in str.ToCharArray())
            {
                hash = ch + ((hash << 5) - hash);
            }
            return hash;
            //string c = (hash & 0x00FFFFFF).ToString("X4").ToUpperInvariant();

            //return "00000".Substring(0, 6 - c.Length) + c;
        }

        public static string RunCommand(this string command)
        {
            var escapedArgs = command.Replace("\"", "\\\"", StringComparison.OrdinalIgnoreCase);
            var startInfo = new ProcessStartInfo
            {
                FileName = FergunClient.IsLinux ? "/bin/bash" : "cmd.exe",
                Arguments = FergunClient.IsLinux ? $"-c \"{escapedArgs}\"" : $"/c {escapedArgs}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = FergunClient.IsLinux ? "/home" : ""
            };

            string result;
            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit(10000);
                if (process.ExitCode == 0)
                {
                    result = process.StandardOutput.ReadToEnd();
                }
                else
                {
                    result = process.StandardError.ReadToEnd();
                }
            };

            return result;
        }
    }
}