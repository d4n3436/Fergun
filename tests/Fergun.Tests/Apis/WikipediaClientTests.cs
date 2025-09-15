using System;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Fergun.Apis.Wikipedia;
using Moq;
using Xunit;

namespace Fergun.Tests.Apis;

public class WikipediaClientTests
{
    private readonly IWikipediaClient _wikipediaClient = new WikipediaClient(GetHttpClient());

    [Theory]
    [InlineData(11846, "en")] // Guitar
    [InlineData(3328953, "es")] // Wikipedia
    public async Task GetArticleAsync_Returns_Valid_Article(int id, string language)
    {
        var article = await _wikipediaClient.GetArticleAsync(id, language, TestContext.Current.CancellationToken);

        Assert.NotNull(article);
        Assert.NotNull(article.Title);
        Assert.NotNull(article.Extract);
        Assert.True(article.Id >= 0);
        Assert.NotNull(article.ToString());

        if (article.Image is not null)
        {
            Assert.NotNull(article.Image.Url);
            Assert.True(article.Image.Width > 0);
            Assert.True(article.Image.Height > 0);
            Assert.NotNull(article.Image.ToString());
        }
    }

    [Theory]
    [InlineData(0, "en")]
    [InlineData(1, "es")]
    public async Task GetArticleAsync_Returns_Null_Article(int id, string language)
    {
        var article = await _wikipediaClient.GetArticleAsync(id, language, TestContext.Current.CancellationToken);

        Assert.Null(article);
    }

    [Theory]
    [InlineData("a", "en")]
    [InlineData("b", "es")]
    [InlineData("c", "fr")]
    public async Task SearchArticlesAsync_Returns_Results(string term, string language)
    {
        var results = await _wikipediaClient.SearchArticlesAsync(term, language, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.All(results, Assert.NotNull);
    }

    [Fact]
    public async Task Disposed_WikipediaClient_Usage_Should_Throw_ObjectDisposedException()
    {
        (_wikipediaClient as IDisposable)?.Dispose();
        (_wikipediaClient as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _wikipediaClient.GetArticleAsync(It.IsAny<int>(), "en", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _wikipediaClient.SearchArticlesAsync("test", "en", TestContext.Current.CancellationToken));
    }

    private static HttpClient GetHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DiscordConfig.UserAgent);

        return httpClient;
    }
}