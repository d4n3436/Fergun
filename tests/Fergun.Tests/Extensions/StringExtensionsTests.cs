using Bogus;
using Fergun.Extensions;
using Xunit;

namespace Fergun.Tests.Extensions;

public class StringExtensionsTests
{
    [Theory]
    [MemberData(nameof(GetSplitStringData))]
    public void String_SplitForPagination_Should_Not_Divide_Words(string str, int length)
    {
        var split = str.SplitForPagination(length);
        string joined = string.Join(' ', split);

        Assert.Equal(str, joined);
    }

    public static TheoryData<string, int> GetSplitStringData()
    {
        var faker = new Faker();
        return faker.MakeLazy(10, () => (faker.Lorem.Sentence(100), faker.Random.Int(20, 30)))
            .ToTheoryData();
    }
}