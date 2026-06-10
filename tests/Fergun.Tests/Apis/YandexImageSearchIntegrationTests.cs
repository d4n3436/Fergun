using System.Threading.Tasks;
using Fergun.Apis.Yandex;
using Xunit;

namespace Fergun.Tests.Apis;

[Trait("Category", "Integration")]
public class YandexImageSearchIntegrationTests
{
    private readonly IYandexImageSearch _yandexImageSearch = new YandexImageSearch();

    [Theory]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/0/01/Windows_fonts_most_used.jpg")]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/5/57/Lorem_Ipsum_Helvetica.png")]
    public async Task OcrAsync_Returns_Text(string url)
    {
        string? text = await _yandexImageSearch.OcrAsync(url, TestContext.Current.CancellationToken);

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Theory]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/4/4d/Cat_November_2010-1a.jpg", YandexSearchFilterMode.None)]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/1/18/Dog_Breeds.jpg", YandexSearchFilterMode.Moderate)]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/0/0e/Landscape-2454891_960_720.jpg", YandexSearchFilterMode.Family)]
    public async Task ReverseImageSearchAsync_Returns_Results(string url, YandexSearchFilterMode mode)
    {
        var results = await _yandexImageSearch.ReverseImageSearchAsync(url, mode, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.NotNull(x.Url));
        Assert.All(results, x => Assert.NotNull(x.SourceUrl));
        Assert.All(results, x => Assert.NotNull(x.Text));
        Assert.All(results, x => Assert.NotNull(x.ToString()));
    }
}