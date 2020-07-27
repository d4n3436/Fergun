using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Fergun.Extensions
{
    public static class StringExtension
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
        /// Converts a string to it's full width form.
        /// </summary>
        /// <param name="input">The string to convert.</param>
        public static string Fullwidth(this string input)
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
            if (string.IsNullOrEmpty(value))
                return value;
            return value.Substring(0, Math.Min(value.Length, maxLength));
        }

        public static bool ContainsAny(this string input, IEnumerable<string> containsKeywords, StringComparison comparisonType)
        {
            return containsKeywords.Any(keyword => input.IndexOf(keyword, comparisonType) >= 0);
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

        public static IEnumerable<string> SplitToLines(this string stringToSplit, int maximumLineLength)
        {
            /*
            int charCount = 0;

            var lines = input.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .GroupBy(w => (charCount += w.Length + 1) / maximumLineLength)
                .Select(g => string.Join(" ", g));
            */
            var words = stringToSplit.Split(' ').Concat(new[] { "" });
            return words
                .Skip(1)
                .Aggregate(
                words.Take(1).ToList(),
                        (a, w) =>
                        {
                            var last = a.Last();
                            while (last.Length > maximumLineLength)
                            {
                                a[a.Count - 1] = last.Substring(0, maximumLineLength);
                                last = last.Substring(maximumLineLength);
                                a.Add(last);
                            }
                            var test = last + " " + w;
                            if (test.Length > maximumLineLength)
                            {
                                a.Add(w);
                            }
                            else
                            {
                                a[a.Count - 1] = test;
                            }
                            return a;
                        });
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