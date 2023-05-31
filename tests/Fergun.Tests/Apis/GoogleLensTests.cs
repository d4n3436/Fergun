using Fergun.Apis.Google;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Fergun.Tests.Apis;

public class GoogleLensTests
{
    private readonly IGoogleLensClient _googleLens = new GoogleLensClient();

    [Theory]
    [InlineData("https://r.bing.com/rp/ecXQMr9jqKMeHE3ADTBrSN_WNyA.jpg", null)]
    [InlineData("https://r.bing.com/rp/vXuQ5-3dSnE08_cK26jVzOTxREk.jpg", "en")]
    [InlineData("https://r.bing.com/rp/NFrQjXWivF4omoTPSU03A6aosg0.jpg", "es")]
    public async Task ReverseImageSearchAsync_Returns_Results(string url, string? language)
    {
        var results = await _googleLens.ReverseImageSearchAsync(url, language);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.NotNull(x.Title));
        Assert.All(results, x => Assert.NotNull(x.SourcePageUrl));
        Assert.All(results, x => Assert.NotNull(x.ThumbnailUrl));
        Assert.All(results, x => Assert.NotNull(x.SourceDomainName));
        Assert.All(results, x => Assert.NotNull(x.SourceIconUrl));
    }

    [Theory]
    [InlineData("https://cdn.discordapp.com/attachments/838832564583661638/954475252027641886/tts.mp3")] // MP3 file
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/2/29/Suru_Bog_10000px.jpg")] // 10000px image
    [InlineData("https://simpl.info/bigimage/bigImage.jpg")] // 91 MB file
    public async Task ReverseImageSearchAsync_Throws_GoogleLensException_If_Image_Is_Invalid(string url)
    {
        var task = _googleLens.ReverseImageSearchAsync(url);

        await Assert.ThrowsAsync<GoogleLensException>(() => task);
    }

    [Fact]
    public async Task Disposed_BingVisualSearch_Usage_Throws_ObjectDisposedException()
    {
        (_googleLens as IDisposable)?.Dispose();
        (_googleLens as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _googleLens.ReverseImageSearchAsync(It.IsAny<string>(), It.IsAny<string?>()));
    }

    [Fact]
    public void GoogleLensException_Has_Expected_Values()
    {
        var innerException = new Exception();

        var exception1 = new GoogleLensException();
        var exception2 = new GoogleLensException("Custom message");
        var exception3 = new GoogleLensException("Custom message 2", innerException);

        Assert.Null(exception1.InnerException);

        Assert.Equal("Custom message", exception2.Message);
        Assert.Null(exception2.InnerException);

        Assert.Equal("Custom message 2", exception3.Message);
        Assert.Same(innerException, exception3.InnerException);
    }
}