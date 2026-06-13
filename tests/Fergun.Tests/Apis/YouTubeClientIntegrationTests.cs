using System;
using System.Threading.Tasks;
using Fergun.Apis.YouTube;
using JetBrains.Annotations;
using Xunit;

namespace Fergun.Tests.Apis;

[Trait("Category", "Integration")]
public class YouTubeClientIntegrationTests
{
    private readonly IYouTubeClient _youTubeClient = new YouTubeClient();

    [Theory]
    [InlineData("discord")]
    [InlineData("lofi hip hop")]
    public async Task SearchVideosAsync_Returns_Valid_Videos(string query)
    {
        var results = await _youTubeClient.SearchVideosAsync(query, TestContext.Current.CancellationToken);

        Assert.NotEmpty(results);

        Assert.All(results, [AssertionMethod] (x) =>
        {
            Assert.NotEmpty(x.Id);
            Assert.NotEmpty(x.Title);
            Assert.NotEmpty(x.Author);
            Assert.True(Uri.IsWellFormedUriString(x.Url, UriKind.Absolute));

            // Null for live streams
            if (x.Duration is not null)
            {
                Assert.True(x.Duration > TimeSpan.Zero);
            }
        });
    }
}