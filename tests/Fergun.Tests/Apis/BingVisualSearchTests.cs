using System;
using System.Drawing;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Fergun.Apis.Bing;
using Moq;
using Xunit;

namespace Fergun.Tests.Apis;

public class BingVisualSearchTests
{
    [Fact]
    public async Task Disposed_BingVisualSearch_Usage_Throws_ObjectDisposedException()
    {
        var bingVisualSearch = new BingVisualSearch();
        bingVisualSearch.Dispose();
        bingVisualSearch.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => bingVisualSearch.OcrAsync("https://example.com/image.png", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => bingVisualSearch.ReverseImageSearchAsync("https://example.com/image.png", It.IsAny<BingSafeSearchLevel>(), It.IsAny<string?>(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void BingException_Has_Expected_Values()
    {
        var innerException = new HttpRequestException();

        var exception1 = new BingException();
        var exception2 = new BingException("Custom message");
        var exception3 = new BingException("Custom message 2", innerException);
        var exception4 = new BingException("Custom message 3", "ImageByteSizeExceedsLimit");

        Assert.Null(exception1.InnerException);

        Assert.Equal("Custom message", exception2.Message);
        Assert.Null(exception2.InnerException);

        Assert.Equal("Custom message 2", exception3.Message);
        Assert.Same(innerException, exception3.InnerException);

        Assert.Equal("Custom message 3", exception4.Message);
        Assert.Equal("ImageByteSizeExceedsLimit", exception4.ImageCategory);
        Assert.Null(exception4.InnerException);
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