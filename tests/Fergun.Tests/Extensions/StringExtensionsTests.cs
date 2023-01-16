using Bogus;
using Fergun.Extensions;
using System.Collections.Generic;
using System.Linq;
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

    [Theory]
    [MemberData(nameof(GetSplitStringData))]
    public void String_SplitWithoutWordBreaking_Should_Not_Divide_Words(string str, int length)
    {
        var split = str.SplitWithoutWordBreaking(length);
        string joined = string.Join(' ', split);

        Assert.Equal(str, joined);
    }

    public static IEnumerable<object[]> GetSplitStringData()
    {
        var faker = new Faker();
        return faker.MakeLazy(10, () => (faker.Lorem.Sentence(100), faker.Random.Int(20, 30)))
            .Select(x => new object[] { x.Item1, x.Item2 });
    }
}