using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoBogus;
using Fergun.Apis.Yandex;
using Moq;
using Moq.Protected;
using Xunit;

namespace Fergun.Tests.Apis;

public class YandexImageSearchTests
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

    [Fact]
    public async Task OcrAsync_Throws_YandexException_With_Content_As_Message_On_Error()
    {
        const string message = "400 Bad request Incorrect avatar size";

        var messageHandlerMock = new Mock<HttpMessageHandler>();

        messageHandlerMock
            .Protected()
            .As<HttpClient>()
            .SetupSequence(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(message) });

        var yandexImageSearch = new YandexImageSearch(new HttpClient(messageHandlerMock.Object));

        var task = yandexImageSearch.OcrAsync("https://example.com", TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<YandexException>(() => task);
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public async Task OcrAsync_Throws_YandexException_If_Captcha_Is_Present()
    {
        var messageHandlerMock = new Mock<HttpMessageHandler>();

        messageHandlerMock
            .Protected()
            .As<HttpClient>()
            .SetupSequence(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"image_id":"test","image_shard":0}""") })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"type":"captcha"}""") });

        var yandexImageSearch = new YandexImageSearch(new HttpClient(messageHandlerMock.Object));

        var task = yandexImageSearch.OcrAsync("https://example.com", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<YandexException>(() => task);
    }

    [Theory]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/thumb/4/4d/Cat_November_2010-1a.jpg/1200px-Cat_November_2010-1a.jpg", YandexSearchFilterMode.None)]
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

    [Fact]
    public async Task ReverseImageSearchAsync_Throws_YandexException_If_Captcha_Is_Present()
    {
        var messageHandlerMock = new Mock<HttpMessageHandler>();

        messageHandlerMock
            .Protected()
            .As<HttpClient>()
            .SetupSequence(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"type":"captcha"}""") });

        var yandexImageSearch = new YandexImageSearch(new HttpClient(messageHandlerMock.Object));

        var task = yandexImageSearch.ReverseImageSearchAsync("https://example.com/image.png", cancellationToken: TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<YandexException>(() => task);
    }

    [Fact]
    public async Task Disposed_YandexImageSearch_Usage_Throws_ObjectDisposedException()
    {
        (_yandexImageSearch as IDisposable)?.Dispose();
        (_yandexImageSearch as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _yandexImageSearch.OcrAsync(AutoFaker.Generate<string>(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _yandexImageSearch.ReverseImageSearchAsync(AutoFaker.Generate<string>(), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void YandexException_Has_Expected_Values()
    {
        var innerException = new HttpRequestException();

        var exception1 = new YandexException();
        var exception2 = new YandexException("Custom message");
        var exception3 = new YandexException("Custom message 2", innerException);

        Assert.Null(exception1.InnerException);

        Assert.Equal("Custom message", exception2.Message);
        Assert.Null(exception2.InnerException);

        Assert.Equal("Custom message 2", exception3.Message);
        Assert.Same(innerException, exception3.InnerException);
    }

    [Theory]
    [InlineData("\"{title:&quot;a&quot;}\"", """{title:"a"}""")]
    [InlineData("\"D&amp;D\"", "D&D")]
    public void HtmlEncodingConverter_Returns_Expected_Values(string encodedString, string decodedString)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new HtmlEncodingConverter());

        string deserialized = JsonSerializer.Deserialize<string>(encodedString, options)!;

        Assert.Equal(decodedString, deserialized);
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(decodedString, options));
    }
}