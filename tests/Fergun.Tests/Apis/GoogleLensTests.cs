using System;
using System.Net.Http;
using System.Threading.Tasks;
using AutoBogus;
using Fergun.Apis.Google;
using Moq;
using Xunit;

namespace Fergun.Tests.Apis;

public class GoogleLensTests
{
    private readonly IGoogleLensClient _googleLens = new GoogleLensClient();

    [Theory]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/0/01/Windows_fonts_most_used.jpg")]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/5/57/Lorem_Ipsum_Helvetica.png")]
    public async Task OcrAsync_Returns_Text(string url)
    {
        string text = await _googleLens.OcrAsync(url, TestContext.Current.CancellationToken);

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Theory]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/2/29/Suru_Bog_10000px.jpg")] // 10000px image
    [InlineData("https://simpl.info/bigimage/bigImage.jpg")] // 91 MB file
    public async Task OcrAsync_Throws_GoogleLensException_If_Image_Is_Invalid(string url)
    {
        var task = _googleLens.OcrAsync(url, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<GoogleLensException>(() => task);
    }

    [Theory(Skip = "Skipped until Google Lens reverse image search is fixed.")] // TODO: Remove when Google Lens reverse image search is fixed
    [InlineData("https://r.bing.com/rp/ecXQMr9jqKMeHE3ADTBrSN_WNyA.jpg", null)]
    [InlineData("https://r.bing.com/rp/vXuQ5-3dSnE08_cK26jVzOTxREk.jpg", "en")]
    [InlineData("https://r.bing.com/rp/NFrQjXWivF4omoTPSU03A6aosg0.jpg", "es")]
    public async Task ReverseImageSearchAsync_Returns_Results(string url, string? language)
    {
        var results = await _googleLens.ReverseImageSearchAsync(url, language, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.NotNull(x.Title));
        Assert.All(results, x => Assert.NotNull(x.SourcePageUrl));
        Assert.All(results, x => Assert.NotNull(x.ThumbnailUrl));
        Assert.All(results, x => Assert.NotNull(x.SourceDomainName));
        Assert.All(results, x => Assert.NotNull(x.SourceIconUrl));
    }

    [Theory]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/2/29/Suru_Bog_10000px.jpg")] // 10000px image
    [InlineData("https://simpl.info/bigimage/bigImage.jpg")] // 91 MB file
    public async Task ReverseImageSearchAsync_Throws_GoogleLensException_If_Image_Is_Invalid(string url)
    {
        var task = _googleLens.ReverseImageSearchAsync(url, cancellationToken: TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<GoogleLensException>(() => task);
    }

    [Fact]
    public async Task Disposed_BingVisualSearch_Usage_Throws_ObjectDisposedException()
    {
        (_googleLens as IDisposable)?.Dispose();
        (_googleLens as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _googleLens.ReverseImageSearchAsync(AutoFaker.Generate<string>(), It.IsAny<string?>(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GoogleLensException_Has_Expected_Values()
    {
        var innerException = new HttpRequestException();

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