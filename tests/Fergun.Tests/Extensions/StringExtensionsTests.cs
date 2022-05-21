using Fergun.Extensions;
using Xunit;

namespace Fergun.Tests.Extensions;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("one two three", "one", "three")]
    [InlineData("1234", "123", "456")]
    [InlineData("1234", "012", "234")]
    [InlineData("abcde", "efg", "hij")]
    public void String_ContainsAny_Should_Return_Expected(string str, string str0, string str1)
    {
        bool containsFirst = str.Contains(str0);
        bool containsSecond = str.Contains(str1);

        bool containsAny = str.ContainsAny(str0, str1);

        Assert.Equal(containsAny, containsFirst || containsSecond);
    }
}