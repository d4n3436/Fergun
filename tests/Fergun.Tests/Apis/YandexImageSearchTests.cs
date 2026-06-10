using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AutoBogus;
using Fergun.Apis.Yandex;
using Xunit;

namespace Fergun.Tests.Apis;

public class YandexImageSearchTests
{
    [Fact]
    public async Task OcrAsync_Throws_YandexException_With_Content_As_Message_On_Error()
    {
        const string message = "400 Bad request Incorrect avatar size";
        var yandexImageSearch = new YandexImageSearch(Utils.CreateMockedHttpClient((HttpStatusCode.BadRequest, message)));

        var exception = await Assert.ThrowsAsync<YandexException>(() => yandexImageSearch.OcrAsync("https://example.com", TestContext.Current.CancellationToken));
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public async Task OcrAsync_Throws_YandexException_If_Captcha_Is_Present()
    {
        var yandexImageSearch = new YandexImageSearch(Utils.CreateMockedHttpClient(
            (HttpStatusCode.OK, """{"image_id":"test","image_shard":0}"""),
            (HttpStatusCode.OK, """{"type":"captcha"}""")));

        await Assert.ThrowsAsync<YandexException>(() => yandexImageSearch.OcrAsync("https://example.com", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReverseImageSearchAsync_Throws_YandexException_If_Captcha_Is_Present()
    {
        var yandexImageSearch = new YandexImageSearch(Utils.CreateMockedHttpClient((HttpStatusCode.OK, """{"type":"captcha"}""")));

        await Assert.ThrowsAsync<YandexException>(() => yandexImageSearch.ReverseImageSearchAsync("https://example.com/image.png", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Disposed_YandexImageSearch_Usage_Throws_ObjectDisposedException()
    {
        IYandexImageSearch yandexImageSearch = new YandexImageSearch(Utils.CreateMockedHttpClient());
        ((IDisposable)yandexImageSearch).Dispose();
        ((IDisposable)yandexImageSearch).Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => yandexImageSearch.OcrAsync(AutoFaker.Generate<string>(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => yandexImageSearch.ReverseImageSearchAsync(AutoFaker.Generate<string>(), cancellationToken: TestContext.Current.CancellationToken));
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