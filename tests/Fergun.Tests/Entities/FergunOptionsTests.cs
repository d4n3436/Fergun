using System.Linq;
using AutoBogus;
using Fergun.Configuration;
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
            VoteUrl = options.VoteUrl,
            DonationUrl = options.DonationUrl,
            PaginatorTimeout = options.PaginatorTimeout,
            SelectionTimeout = options.SelectionTimeout,
            PaginatorEmotes = options.PaginatorEmotes,
            ExtraEmotes = options.ExtraEmotes
        };

        Assert.Equal(options.SupportServerUrl, other.SupportServerUrl);
        Assert.Equal(options.VoteUrl, other.VoteUrl);
        Assert.Equal(options.DonationUrl, other.DonationUrl);
        Assert.Equal(options.PaginatorTimeout, other.PaginatorTimeout);
        Assert.Equal(options.SelectionTimeout, other.SelectionTimeout);
        Assert.True(options.PaginatorEmotes.SequenceEqual(other.PaginatorEmotes));
        Assert.Equal(options.ExtraEmotes, other.ExtraEmotes);
    }

    public static TheoryData<FergunOptions> GetFergunOptionsTestData()
        => AutoFaker.Generate<FergunOptions>(10).ToTheoryData();
}