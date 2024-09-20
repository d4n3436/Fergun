using System;
using System.Collections.Generic;

namespace Fergun.Extensions;

public static class StringExtensions
{
    public static bool ContainsAny(this string str, string str0, string str1) => str.Contains(str0) || str.Contains(str1);

    /// <summary>
    /// Splits a string into chunks, each of at most <paramref name="maxLength"/> of length, in a way that is suitable for pagination.<br/>
    /// The method tries to avoid breaking the text by splitting it with 3 separators in the following order:<br/>
    /// 2 newlines ("\n\n") -> 1 newline ('\n') -> 1 space (' ')<br/>
    /// If it's not possible to do so, the method will fall back to split the text without restrictions and continue with the next chunks.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="maxLength">The max. length of the chunks.</param>
    /// <returns>An enumerator that iterates over the chunks.</returns>
    public static IEnumerable<ReadOnlyMemory<char>> SplitForPagination(this string text, int maxLength)
    {
        var current = text.AsMemory();

        while (!current.IsEmpty)
        {
            int index = -1;
            int length;

            if (current.Length <= maxLength)
            {
                length = current.Length;
            }
            else
            {
                var portion = current[..(maxLength + 1)].Span;

                index = portion.LastIndexOf("\n\n");
                if (index == -1)
                    index = portion.LastIndexOf('\n');
                if (index == -1)
                    index = portion.LastIndexOf(' ');

                length = index == -1 ? maxLength : index;
            }

            yield return current[..length];

            if (index != -1)
            {
                // Consume more newlines and spaces if any
                while (length < current.Length && current.Span[length] is '\n' or ' ')
                {
                    length++;
                }
            }

            current = current[length..];
        }
    }
}