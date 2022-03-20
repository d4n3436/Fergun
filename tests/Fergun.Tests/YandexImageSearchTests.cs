using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Fergun.Apis.Yandex;
using Moq;
using Moq.Protected;
using Xunit;

namespace Fergun.Tests;

public class YandexImageSearchTests
{
    private readonly YandexImageSearch _yandexImageSearch = new();

    [Theory]
    [InlineData("https://cdn.discordapp.com/attachments/838832564583661638/954474328324460544/lorem_ipsum.png")]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/5/57/Lorem_Ipsum_Helvetica.png")]
    public async Task OcrAsync_Returns_Text(string url)
    {
        string? text = await _yandexImageSearch.OcrAsync(url);

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

        var task = yandexImageSearch.OcrAsync("https://example.com");

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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"image_id\":\"test\",\"image_shard\":0}") })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"type\":\"captcha\"}") });

        var yandexImageSearch = new YandexImageSearch(new HttpClient(messageHandlerMock.Object));

        var task = yandexImageSearch.OcrAsync("https://example.com");

        await Assert.ThrowsAsync<YandexException>(() => task);
    }

    [Fact]
    public async Task Disposed_UrbanDictionary_Usage_Throws_ObjectDisposedException()
    {
        _yandexImageSearch.Dispose();
        _yandexImageSearch.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _yandexImageSearch.OcrAsync(It.IsAny<string>()));
    }
}