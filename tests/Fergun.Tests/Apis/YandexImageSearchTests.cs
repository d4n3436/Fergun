using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Fergun.Apis.Yandex;
using Moq;
using Moq.Protected;
using Xunit;

namespace Fergun.Tests.Apis;

public class YandexImageSearchTests
{
    private readonly IYandexImageSearch _yandexImageSearch = new YandexImageSearch();

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

    [Theory]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/thumb/4/4d/Cat_November_2010-1a.jpg/1200px-Cat_November_2010-1a.jpg", YandexSearchFilterMode.None)]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/1/18/Dog_Breeds.jpg", YandexSearchFilterMode.Moderate)]
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/0/0e/Landscape-2454891_960_720.jpg", YandexSearchFilterMode.Family)]
    public async Task ReverseImageSearchAsync_Returns_Results(string url, YandexSearchFilterMode mode)
    {
        var results = (await _yandexImageSearch.ReverseImageSearchAsync(url, mode)).ToArray();

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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"type\":\"captcha\"}") });

        var yandexImageSearch = new YandexImageSearch(new HttpClient(messageHandlerMock.Object));

        var task = yandexImageSearch.ReverseImageSearchAsync("https://example.com/image.png");

        await Assert.ThrowsAsync<YandexException>(() => task);
    }

    [Fact]
    public async Task ReverseImageSearchAsync_Ignores_Invalid_Results()
    {
        var rawResults = new[]
        {
            string.Empty,
            "{[",
            @"
{
    ""serp-item"":
    {
        ""img_href"": null
    }
}",
            @"
{
    ""serp-item"":
    {
        ""img_href"": ""https://example.com/image.png"",
        ""snippet"":
        {
            ""url"": null,
            ""text"": ""sample text""
        }
    }
}",
            @"
{
    ""serp-item"":
    {
        ""img_href"": ""https://example.com/image.png"",
        ""snippet"":
        {
            ""url"": ""https://example.com"",
            ""text"": null
        }
    }
}"
        };

        var context = BrowsingContext.New();
        var document = await context.OpenNewAsync();
        var serpList = document.CreateElement<IHtmlDivElement>();
        serpList.ClassName = "serp-list";

        serpList.Append(rawResults.Select(x =>
        {
            var item = document.CreateElement<IHtmlDivElement>();
            item.ClassName = "serp-item";

            item.SetAttribute("data-bem", x);
            return (INode)item;
        }).ToArray());

        string html = serpList.ToHtml();

        string json = $@"
{{
    ""blocks"":
    [
        {{
            ""html"": ""{{{JsonEncodedText.Encode(html)}}}""
        }}
    ]
}}";

        var messageHandlerMock = new Mock<HttpMessageHandler>();

        messageHandlerMock
            .Protected()
            .As<HttpClient>()
            .SetupSequence(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });

        var yandexImageSearch = new YandexImageSearch(new HttpClient(messageHandlerMock.Object));

        var results = (await yandexImageSearch.ReverseImageSearchAsync("https://example.com/image.png")).ToArray();

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Disposed_UrbanDictionary_Usage_Throws_ObjectDisposedException()
    {
        (_yandexImageSearch as IDisposable)?.Dispose();
        (_yandexImageSearch as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _yandexImageSearch.OcrAsync(It.IsAny<string>()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _yandexImageSearch.ReverseImageSearchAsync(It.IsAny<string>()));
    }
}