namespace Fergun.Extensions;

public static class StringExtensions
{
    public static bool ContainsAny(this string str, string str0, string str1) => str.Contains(str0) || str.Contains(str1);
}