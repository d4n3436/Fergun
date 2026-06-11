using AutoBogus;
using Fergun.Configuration;
using Xunit;

namespace Fergun.Tests.Entities;

public class StartupOptionsTests
{
    [Theory]
    [MemberData(nameof(GetStartupOptionsTestData), DisableDiscoveryEnumeration = true)]
    public void StartupOptions_Properties_Has_Expected_Values(StartupOptions options)
    {
        var other = new StartupOptions
        {
            Token = options.Token,
            TestingGuildId = options.TestingGuildId,
            OwnerCommandsGuildId = options.OwnerCommandsGuildId,
            MobileStatus = options.MobileStatus
        };

        Assert.Equal(options.Token, other.Token);
        Assert.Equal(options.TestingGuildId, other.TestingGuildId);
        Assert.Equal(options.OwnerCommandsGuildId, other.OwnerCommandsGuildId);
        Assert.Equal(options.MobileStatus, other.MobileStatus);
    }

    public static TheoryData<StartupOptions> GetStartupOptionsTestData()
        => new AutoFaker<StartupOptions>().UseSeed(42).Generate(10).ToTheoryData();
}