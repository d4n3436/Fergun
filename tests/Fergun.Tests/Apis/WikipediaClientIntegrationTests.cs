using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Fergun.Apis.Wikipedia;
using Xunit;

namespace Fergun.Tests.Apis;

[Trait("Category", "Integration")]
public class WikipediaClientIntegrationTests
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

    private static HttpClient GetHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DiscordConfig.UserAgent);

        return httpClient;
    }
}