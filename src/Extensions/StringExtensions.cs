using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fergun.Extensions
{
    public static class StringExtensions
    {
        public static string Repeat(this string text, int count)
        {
            return string.Concat(Enumerable.Repeat(text, count));
        }

        public static string RepeatToLength(this string text, int length)
        {
            return Repeat(text, length / text.Length + 1).Truncate(length);
        }

        public static string Reverse(this string text)
        {
            char[] chars = text.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        public static string ReverseWords(this string text)
        {
            return string.Join(' ', text.Split(' ').Reverse());
        }

        public static string ReverseEachLine(this string text)
        {
            return string.Join("\r\n", text.Split('\r', '\n').Reverse());
        }

        public static string Randomize(this string input, Random rng = null)
        {
            var arr = input.ToCharArray();
            arr.Shuffle(rng);
            return new string(arr);
        }

        public static string ToRandomCase(this string text, Random rng = null)
        {
            rng ??= Random.Shared;

            return string.Create(text.Length, (input: text, rng), (chars, state) =>
            {
                for (int i = 0; i < chars.Length; i++)
                {
                    chars[i] = state.rng.Next(2) == 0 ? char.ToUpperInvariant(state.input[i]) : char.ToLowerInvariant(state.input[i]);
                }
            });
        }

        /// <summary>
        /// Converts a string to its full width form.
        /// </summary>
        /// <param name="text">The string to convert.</param>
        public static string ToFullWidth(this string text)
        {
            return string.Create(text.Length, text, (chars, state) =>
            {
                for (int i = 0; i < chars.Length; i++)
                {
                    if (0x21 <= state[i] && state[i] <= 0x7E) // ASCII chars, excluding space
                        chars[i] = (char)(state[i] + 0xFEE0);
                    else if (state[i] == 0x20)
                        chars[i] = (char)0x3000;
                }
            });
        }

        /// <summary>
        /// Truncates a string to the specified length.
        /// </summary>
        /// <param name="text">The string to truncate.</param>
        /// <param name="maxLength">The maximum length of the string.</param>
        /// <returns>The truncated string.</returns>
        public static string Truncate(this string text, int maxLength)
        {
            return text?.Substring(0, Math.Min(text.Length, maxLength));
        }

        public static bool ContainsAny(this string text, IEnumerable<string> containsKeywords, StringComparison comparisonType)
        {
            return containsKeywords.Any(keyword => text.Contains(keyword, comparisonType));
        }

        public static bool TryBase64Decode(this string text, out string decoded)
        {
            var buffer = new Span<byte>(new byte[text.Length]);
            bool success = Convert.TryFromBase64String(text, buffer, out int bytesWritten);

            decoded = success ? Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten)) : null;

            return success;
        }

        public static string ToTitleCase(this string text)
        {
            return string.Create(text.Length, text, (chars, state) =>
            {
                state.AsSpan().ToLowerInvariant(chars);
                chars[0] = char.ToUpperInvariant(state[0]);
            });
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