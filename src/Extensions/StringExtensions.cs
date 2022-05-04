namespace Fergun.Extensions;

public static class StringExtensions
{
    public static bool ContainsAny(this string str, string str0, string str1) => str.Contains(str0) || str.Contains(str1);

    // From GTranslate
    public static IEnumerable<ReadOnlyMemory<char>> SplitWithoutWordBreaking(this string text, int maxLength)
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
                index = current[..maxLength].Span.LastIndexOf(' ');
                length = index == -1 ? maxLength : index;
            }

            var line = current[..length];
            // skip a single space if there's one
            if (index != -1)
            {
                length++;
            }

            current = current[length..];
            yield return line;
        }
    }
}