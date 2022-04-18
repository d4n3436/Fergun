using System;
using System.Linq;
using System.Threading.Tasks;
using Fergun.Apis.Wikipedia;
using Moq;
using Xunit;

namespace Fergun.Tests.Apis;

public class WikipediaClientTests
{
    private readonly IWikipediaClient _wikipediaClient = new WikipediaClient();

    [Theory]
    [InlineData("Guitar", "en")]
    [InlineData("Wikipedia", "es")]
    public async Task GetArticlesAsync_Returns_Articles(string term, string language)
    {
        var articles = (await _wikipediaClient.GetArticlesAsync(term, language)).ToArray();

        Assert.NotNull(articles);
        Assert.NotEmpty(articles);
        Assert.All(articles, Assert.NotNull);
        Assert.All(articles, x => Assert.NotNull(x.Title));
        Assert.All(articles, x => Assert.NotNull(x.Extract));
        Assert.All(articles, x => Assert.True(x.Id >= 0));
        Assert.All(articles, x => Assert.NotNull(x.ToString()));

        Assert.All(articles, x =>
        {
            if (x.Image is not null)
            {
                Assert.NotNull(x.Image.Url.ToString());
                Assert.True(x.Image.Width > 0);
                Assert.True(x.Image.Height > 0);
                Assert.NotNull(x.Image.ToString());
            }
        });
    }

    [Theory]
    [InlineData("a", "en")]
    [InlineData("b", "es")]
    [InlineData("c", "fr")]
    public async Task GetAutocompleteResultsAsync_Returns_Results(string term, string language)
    {
        var results = (await _wikipediaClient.GetAutocompleteResultsAsync(term, language)).ToArray();

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.All(results, Assert.NotNull);
    }

    [Fact]
    public async Task Disposed_WikipediaClient_Usage_Should_Throw_ObjectDisposedException()
    {
        (_wikipediaClient as IDisposable)?.Dispose();
        (_wikipediaClient as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _wikipediaClient.GetArticlesAsync(It.IsAny<string>(), It.IsAny<string>()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _wikipediaClient.GetAutocompleteResultsAsync(It.IsAny<string>(), It.IsAny<string>()));
    }
}