using System;
using System.Threading.Tasks;
using Fergun.Apis.Bing;
using Moq;
using Xunit;

namespace Fergun.Tests;

public class BingVisualSearchTests
{
    private readonly BingVisualSearch _bingVisualSearch = new();

    [Theory]
    [InlineData("https://cdn.discordapp.com/attachments/838832564583661638/954474328324460544/lorem_ipsum.png")]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/5/57/Lorem_Ipsum_Helvetica.png")]
    public async Task OcrAsync_Returns_Text(string url)
    {
        string? text = await _bingVisualSearch.OcrAsync(url);

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Theory]
    [InlineData("https://cdn.discordapp.com/attachments/838832564583661638/954475252027641886/tts.mp3")] // MP3 file
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/2/29/Suru_Bog_10000px.jpg")] // 10000px image
    [InlineData("https://simpl.info/bigimage/bigImage.jpg")] // 91 MB file
    public async Task OcrAsync_Throws_BingException_If_Image_Is_Invalid(string url)
    {
        var task = _bingVisualSearch.OcrAsync(url);

        await Assert.ThrowsAsync<BingException>(() => task);
    }

    [Fact]
    public async Task Disposed_UrbanDictionary_Usage_Throws_ObjectDisposedException()
    {
        _bingVisualSearch.Dispose();
        _bingVisualSearch.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _bingVisualSearch.OcrAsync(It.IsAny<string>()));
    }
}