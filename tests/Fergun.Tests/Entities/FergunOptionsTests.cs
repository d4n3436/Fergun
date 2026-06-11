using AutoBogus;
using Fergun.Configuration;
using Xunit;

namespace Fergun.Tests.Entities;

public class FergunOptionsTests
{
    [Theory]
    [MemberData(nameof(GetFergunOptionsTestData), DisableDiscoveryEnumeration = true)]
    public void FergunOptions_Properties_Has_Expected_Values(FergunOptions options)
    {
        var other = new FergunOptions
        {
            SupportServerUrl = options.SupportServerUrl,
            VoteUrl = options.VoteUrl,
            DonationUrl = options.DonationUrl,
            PaginatorTimeout = options.PaginatorTimeout,
            SelectionTimeout = options.SelectionTimeout
        };

        Assert.Equal(options.SupportServerUrl, other.SupportServerUrl);
        Assert.Equal(options.VoteUrl, other.VoteUrl);
        Assert.Equal(options.DonationUrl, other.DonationUrl);
        Assert.Equal(options.PaginatorTimeout, other.PaginatorTimeout);
        Assert.Equal(options.SelectionTimeout, other.SelectionTimeout);
    }

    public static TheoryData<FergunOptions> GetFergunOptionsTestData()
        => new AutoFaker<FergunOptions>().UseSeed(42).Generate(10).ToTheoryData();
}