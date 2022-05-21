using System;
using System.Linq;
using System.Threading.Tasks;
using Fergun.Apis.Bing;
using Moq;
using Xunit;

namespace Fergun.Tests.Apis;

public class BingVisualSearchTests
{
    private readonly IBingVisualSearch _bingVisualSearch = new BingVisualSearch();

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

    [Theory]
    [InlineData("https://bingvsdevportalprodgbl.blob.core.windows.net/demo-images/876bb7a8-e8dd-4e36-ab3a-f0b9aba942e5.jpg", BingSafeSearchLevel.Off, null)]
    [InlineData("https://bingvsdevportalprodgbl.blob.core.windows.net/demo-images/391126cd-977a-43c7-9937-4f139623cd58.jpeg", BingSafeSearchLevel.Moderate, "en")]
    [InlineData("https://bingvsdevportalprodgbl.blob.core.windows.net/demo-images/5a5e947c-c248-4e4c-a717-d1f798ddb1ba.jpeg", BingSafeSearchLevel.Strict, "es")]
    public async Task ReverseImageSearchAsync_Returns_Results(string url, BingSafeSearchLevel safeSearch, string? language)
    {
        var results = (await _bingVisualSearch.ReverseImageSearchAsync(url, safeSearch, language)).ToArray();

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.NotNull(x.Url));
        Assert.All(results, x => Assert.NotNull(x.SourceUrl));
        Assert.All(results, x => Assert.NotNull(x.Text));
        Assert.All(results, x => Assert.NotNull(x.ToString()));
    }

    [Theory]
    [InlineData("https://cdn.discordapp.com/attachments/838832564583661638/954475252027641886/tts.mp3")] // MP3 file
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/2/29/Suru_Bog_10000px.jpg")] // 10000px image
    [InlineData("https://simpl.info/bigimage/bigImage.jpg")] // 91 MB file
    public async Task ReverseImageSearchAsync_Throws_BingException_If_Image_Is_Invalid(string url)
    {
        var task = _bingVisualSearch.ReverseImageSearchAsync(url);

        await Assert.ThrowsAsync<BingException>(() => task);
    }

    [Fact]
    public async Task Disposed_BingVisualSearch_Usage_Throws_ObjectDisposedException()
    {
        (_bingVisualSearch as IDisposable)?.Dispose();
        (_bingVisualSearch as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _bingVisualSearch.OcrAsync(It.IsAny<string>()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _bingVisualSearch.ReverseImageSearchAsync(It.IsAny<string>(), It.IsAny<BingSafeSearchLevel>(), It.IsAny<string?>()));
    }
}