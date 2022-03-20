using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Bing;
using Fergun.Apis.Yandex;
using Fergun.Modules;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Fergun.Tests;

public class OcrModuleTests
{
    private readonly Mock<IInteractionContext> _contextMock = new();
    private readonly Mock<IDiscordInteraction> _interactionMock = new();
    private readonly Mock<IBingVisualSearch> _bingVisualSearchMock = new();
    private readonly Mock<IYandexImageSearch> _yandexImageSearchMock = new();
    private readonly Mock<ILogger<OcrModule>> _loggerMock = new();
    private readonly Mock<OcrModule> _ocrModuleMock;
    private const string _textImageUrl = "https://example.com/image.png";
    private const string _emptyImageUrl = "https://example.com/empty.png";
    private const string _invalidImageUrl = "https://example.com/file.bin";

    public OcrModuleTests()
    {
        _bingVisualSearchMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == _textImageUrl))).ReturnsAsync("test");
        _bingVisualSearchMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == _emptyImageUrl))).ReturnsAsync(string.Empty);
        _bingVisualSearchMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == _invalidImageUrl))).ThrowsAsync(new BingException("Invalid image."));
        _yandexImageSearchMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == _textImageUrl))).ReturnsAsync("test");
        _yandexImageSearchMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == _emptyImageUrl))).ReturnsAsync(string.Empty);
        _yandexImageSearchMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == _invalidImageUrl))).ThrowsAsync(new YandexException("Invalid image."));

        var sharedLogger = Mock.Of<ILogger<SharedModule>>();
        var shared = new SharedModule(sharedLogger, new(), new());

        _ocrModuleMock = new Mock<OcrModule>(() => new OcrModule(_loggerMock.Object, shared, _bingVisualSearchMock.Object, _yandexImageSearchMock.Object));
        _contextMock.SetupGet(x => x.Interaction).Returns(_interactionMock.Object);
        ((IInteractionModuleBase)_ocrModuleMock.Object).SetContext(_contextMock.Object);
    }

    [Theory]
    [InlineData(_textImageUrl)]
    [InlineData(_emptyImageUrl)]
    public async Task Bing_Uses_BingVisualSearch(string url)
    {
        var module = _ocrModuleMock.Object;
        const bool isEphemeral = false;

        await module.Bing(url);

        _ocrModuleMock
            .Protected()
            .As<IDiscordInteraction>()
            .Verify(x => x.DeferAsync(It.Is<bool>(b => b == isEphemeral), It.IsAny<RequestOptions>()), Times.Once());

        _bingVisualSearchMock.Verify(x => x.OcrAsync(It.Is<string>(s => s == url)), Times.Once);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => b == isEphemeral),
                It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once());
    }

    [Theory]
    [InlineData(_textImageUrl)]
    [InlineData(_emptyImageUrl)]
    public async Task Yandex_Uses_YandexImageSearch(string url)
    {
        var module = _ocrModuleMock.Object;
        const bool isEphemeral = false;

        await module.Yandex(url);

        _ocrModuleMock
            .Protected()
            .As<IDiscordInteraction>()
            .Verify(x => x.DeferAsync(It.Is<bool>(b => b == isEphemeral), It.IsAny<RequestOptions>()), Times.Once());

        _yandexImageSearchMock.Verify(x => x.OcrAsync(It.Is<string>(s => s == url)), Times.Once);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => b == isEphemeral),
                It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once());
    }

    [Fact]
    public async Task OcrAsync_Returns_Warning_If_Url_Is_Invalid()
    {
        var module = _ocrModuleMock.Object;
        const bool isEphemeral = true;

        await module.OcrAsync(It.IsAny<OcrModule.OcrEngine>(), string.Empty, isEphemeral);

        _interactionMock.Verify(x => x.RespondAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => b == isEphemeral),
                It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once());
    }

    [Fact]
    public async Task Invalid_OcrEngine_Throws_ArgumentException()
    {
        var module = _ocrModuleMock.Object;
        const bool isEphemeral = true;

        var task = module.OcrAsync((OcrModule.OcrEngine)2, _textImageUrl, isEphemeral);

        await Assert.ThrowsAsync<ArgumentException>(() => task);
    }

    [Fact]
    public async Task OcrAsync_Returns_Warning_On_Exception()
    {
        var module = _ocrModuleMock.Object;
        const bool isEphemeral = true;

        await module.OcrAsync(It.IsAny<OcrModule.OcrEngine>(), _invalidImageUrl, isEphemeral);

        _ocrModuleMock
            .Protected()
            .As<IDiscordInteraction>()
            .Verify(x => x.DeferAsync(It.Is<bool>(b => b == isEphemeral), It.IsAny<RequestOptions>()), Times.Once());

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => b == isEphemeral),
                It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once());
    }
}