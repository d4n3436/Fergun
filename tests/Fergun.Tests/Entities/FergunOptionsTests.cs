using System.Collections.Generic;
using System.Linq;
using AutoBogus;
using Xunit;

namespace Fergun.Tests.Entities;

public class FergunOptionsTests
{
    [Theory]
    [MemberData(nameof(GetFergunOptionsTestData))]
    public void FergunOptions_Properties_Has_Expected_Values(FergunOptions options)
    {
        var other = new FergunOptions
        {
            SupportServerUrl = options.SupportServerUrl,
            CloudflareClearance = options.CloudflareClearance,
            PaginatorTimeout = options.PaginatorTimeout,
            SelectionTimeout = options.SelectionTimeout,
            PaginatorEmotes = options.PaginatorEmotes
        };

        Assert.Equal(options.SupportServerUrl, other.SupportServerUrl);
        Assert.Equal(options.CloudflareClearance, other.CloudflareClearance);
        Assert.Equal(options.PaginatorTimeout, other.PaginatorTimeout);
        Assert.Equal(options.SelectionTimeout, other.SelectionTimeout);
        Assert.True(options.PaginatorEmotes.SequenceEqual(other.PaginatorEmotes));
    }

    public static IEnumerable<object[]> GetFergunOptionsTestData()
        => AutoFaker.Generate<FergunOptions>(10).Select(x => new object[] { x });
}