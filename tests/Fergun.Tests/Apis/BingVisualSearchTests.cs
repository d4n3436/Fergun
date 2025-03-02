using System;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;
using Fergun.Apis.Bing;
using Moq;
using System.Net.Http;
using Xunit;

namespace Fergun.Tests.Apis;

public class BingVisualSearchTests
{
    private readonly IBingVisualSearch _bingVisualSearch = new BingVisualSearch();

    [Theory]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/0/01/Windows_fonts_most_used.jpg")]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/5/57/Lorem_Ipsum_Helvetica.png")]
    public async Task OcrAsync_Returns_Text(string url)
    {
        string text = await _bingVisualSearch.OcrAsync(url);

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Theory]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/2/29/Suru_Bog_10000px.jpg")] // 10000px image
    [InlineData("https://simpl.info/bigimage/bigImage.jpg")] // 91 MB file
    public async Task OcrAsync_Returns_No_Text_If_Image_Is_Invalid(string url)
    {
        string text = await _bingVisualSearch.OcrAsync(url);

        Assert.Empty(text);
    }

    [Theory]
    [InlineData("https://r.bing.com/rp/ecXQMr9jqKMeHE3ADTBrSN_WNyA.jpg", BingSafeSearchLevel.Off, null)]
    [InlineData("https://r.bing.com/rp/vXuQ5-3dSnE08_cK26jVzOTxREk.jpg", BingSafeSearchLevel.Moderate, "en")]
    [InlineData("https://r.bing.com/rp/NFrQjXWivF4omoTPSU03A6aosg0.jpg", BingSafeSearchLevel.Strict, "es")]
    public async Task ReverseImageSearchAsync_Returns_Results(string url, BingSafeSearchLevel safeSearch, string? language)
    {
        var results = await _bingVisualSearch.ReverseImageSearchAsync(url, safeSearch, language);

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
        var results = await _bingVisualSearch.ReverseImageSearchAsync(url);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Disposed_BingVisualSearch_Usage_Throws_ObjectDisposedException()
    {
        (_bingVisualSearch as IDisposable)?.Dispose();
        (_bingVisualSearch as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _bingVisualSearch.OcrAsync(It.IsAny<string>()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _bingVisualSearch.ReverseImageSearchAsync(It.IsAny<string>(), It.IsAny<BingSafeSearchLevel>(), It.IsAny<string?>()));
    }

    [Fact]
    public void BingException_Has_Expected_Values()
    {
        var innerException = new HttpRequestException();

        var exception1 = new BingException();
        var exception2 = new BingException("Custom message");
        var exception3 = new BingException("Custom message 2", innerException);

        Assert.Null(exception1.InnerException);

        Assert.Equal("Custom message", exception2.Message);
        Assert.Null(exception2.InnerException);

        Assert.Equal("Custom message 2", exception3.Message);
        Assert.Same(innerException, exception3.InnerException);
    }

    [Theory]
    [InlineData("\"B38E18\"", 0xB38E18)]
    [InlineData("\"73A02B\"", 0x73A02B)]
    [InlineData("\"676962\"", 0x676962)]
    public void ColorConverter_Returns_Expected_Values(string hexString, int number)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new Fergun.Apis.Bing.ColorConverter());

        var color = JsonSerializer.Deserialize<Color>(hexString, options);
        string serializedColor = JsonSerializer.Serialize(color, options);

        Assert.Equal(number, color.ToArgb());
        Assert.Equal(hexString, serializedColor);
    }
}