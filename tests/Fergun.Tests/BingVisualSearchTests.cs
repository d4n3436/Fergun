using System;
using System.Linq;
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

    [Theory]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/thumb/4/4d/Cat_November_2010-1a.jpg/1200px-Cat_November_2010-1a.jpg", BingSafeSearchLevel.Off)]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/1/18/Dog_Breeds.jpg", BingSafeSearchLevel.Moderate)]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/thumb/5/51/A_beautiful_landscape_of_nature.jpg/1024px-A_beautiful_landscape_of_nature.jpg", BingSafeSearchLevel.Strict)]
    public async Task ReverseImageSearchAsync_Returns_Results(string url, BingSafeSearchLevel safeSearch)
    {
        var results = (await _bingVisualSearch.ReverseImageSearchAsync(url, safeSearch)).ToArray();

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
    public async Task Disposed_UrbanDictionary_Usage_Throws_ObjectDisposedException()
    {
        _bingVisualSearch.Dispose();
        _bingVisualSearch.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _bingVisualSearch.OcrAsync(It.IsAny<string>()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _bingVisualSearch.ReverseImageSearchAsync(It.IsAny<string>(), It.IsAny<BingSafeSearchLevel>()));
    }
}