using System;
using System.Collections.Generic;
using System.Linq;
using Bogus;
using Fergun.Extensions;
using Xunit;

namespace Fergun.Tests.Extensions;

public class TimestampExtensionsTests
{
    [Theory]
    [MemberData(nameof(GetDatesAndStyles))]
    public void DateTimeOffset_ToDiscordTimestamp_Should_Return_Expected(DateTimeOffset dateTimeOffset, char style)
    {
        long unixSeconds = dateTimeOffset.ToUnixTimeSeconds();

        string timestamp = dateTimeOffset.ToDiscordTimestamp(style);

        Assert.Equal(timestamp, $"<t:{unixSeconds}:{style}>");
    }

    public static IEnumerable<object[]> GetDatesAndStyles()
    {
        var faker = new Faker();
        return faker.MakeLazy(10, () => (faker.Date.BetweenOffset(DateTimeOffset.MinValue, DateTimeOffset.MaxValue), faker.Random.Char()))
            .Select(x => new object[] { x.Item1, x.Item2 });
    }
}