using System;
using System.Threading.Tasks;
using Fergun.Apis.Bing;
using Xunit;

namespace Fergun.Tests.Apis;

[Trait("Category", "Integration")]
public class BingVisualSearchIntegrationTests
{
    private readonly IBingVisualSearch _bingVisualSearch = new BingVisualSearch();

    [Theory]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/thumb/8/86/Lorem_ipsum_design.svg/1008px-Lorem_ipsum_design.svg.png")]
    public async Task OcrAsync_Returns_Text(string url)
    {
        string text = await _bingVisualSearch.OcrAsync(url, TestContext.Current.CancellationToken);

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Theory]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/2/29/Suru_Bog_10000px.jpg")] // 10000px image
    [InlineData("https://simpl.info/bigimage/bigImage.jpg")] // 91 MB file
    public async Task OcrAsync_Returns_No_Text_If_Image_Is_Invalid(string url)
    {
        string text = await _bingVisualSearch.OcrAsync(url, TestContext.Current.CancellationToken);

        Assert.Empty(text);
    }

    [Theory]
    [InlineData("https://r.bing.com/rp/ecXQMr9jqKMeHE3ADTBrSN_WNyA.jpg", BingSafeSearchLevel.Off, null)]
    [InlineData("https://r.bing.com/rp/vXuQ5-3dSnE08_cK26jVzOTxREk.jpg", BingSafeSearchLevel.Moderate, "en")]
    [InlineData("https://r.bing.com/rp/NFrQjXWivF4omoTPSU03A6aosg0.jpg", BingSafeSearchLevel.Strict, "es")]
    public async Task ReverseImageSearchAsync_Returns_Results(string url, BingSafeSearchLevel safeSearch, string? language)
    {
        var results = await _bingVisualSearch.ReverseImageSearchAsync(url, safeSearch, language, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.NotNull(x.Url));
        Assert.All(results, x => Assert.NotNull(x.SourceUrl));
        Assert.All(results, x => Assert.NotNull(x.Text));
        Assert.All(results, x => Assert.Equal(0, x.AccentColor.A));
        Assert.All(results, x => Assert.NotNull(x.ToString()));
        Assert.All(results, x =>
        {
            if (x.FriendlyDomainName is null)
            {
                Assert.True(Uri.TryCreate(x.SourceUrl, UriKind.Absolute, out _));
            }
        });
    }

    [Theory]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/2/29/Suru_Bog_10000px.jpg")] // 10000px image
    [InlineData("https://simpl.info/bigimage/bigImage.jpg")] // 91 MB file
    public async Task ReverseImageSearchAsync_Returns_Empty_Results_If_Image_Is_Invalid(string url)
    {
        var results = await _bingVisualSearch.ReverseImageSearchAsync(url, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }
}