using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Apis.Bing;
using Fergun.Apis.Google;
using Fergun.Apis.Yandex;
using Fergun.Interactive;
using Fergun.Modules;
using Fergun.Services;
using GTranslate.Translators;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Fergun.Tests.Modules;

public class OcrModuleTests
{
    private readonly Mock<IInteractionContext> _contextMock = new();
    private readonly Mock<IDiscordInteraction> _interactionMock = new();
    private readonly Mock<IGoogleLensClient> _googleLensMock = new();
    private readonly Mock<IBingVisualSearch> _bingVisualSearchMock = new();
    private readonly Mock<IYandexImageSearch> _yandexImageSearchMock = new();
    private readonly Mock<ILogger<OcrModule>> _loggerMock = new();
    private readonly DiscordSocketClient _client = new();
    private readonly InteractiveConfig _interactiveConfig = new() { DeferStopSelectionInteractions = false };
    private readonly IFergunLocalizer<OcrModule> _ocrLocalizer = Utils.CreateMockedLocalizer<OcrModule>();
    private readonly Mock<OcrModule> _moduleMock;
    private const string TextImageUrl = "https://example.com/image.png";
    private const string EmptyImageUrl = "https://example.com/empty.png";
    private const string InvalidImageUrl = "https://example.com/file.bin";

    public OcrModuleTests()
    {
        _googleLensMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == TextImageUrl), It.IsAny<CancellationToken>())).ReturnsAsync("test");
        _googleLensMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == EmptyImageUrl), It.IsAny<CancellationToken>())).ReturnsAsync(string.Empty);
        _googleLensMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == InvalidImageUrl), It.IsAny<CancellationToken>())).ThrowsAsync(new GoogleLensException("Invalid image."));
        _bingVisualSearchMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == TextImageUrl), It.IsAny<CancellationToken>())).ReturnsAsync("test");
        _bingVisualSearchMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == EmptyImageUrl), It.IsAny<CancellationToken>())).ReturnsAsync(string.Empty);
        _bingVisualSearchMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == InvalidImageUrl), It.IsAny<CancellationToken>())).ThrowsAsync(new BingException("Invalid image."));
        _yandexImageSearchMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == TextImageUrl), It.IsAny<CancellationToken>())).ReturnsAsync("test");
        _yandexImageSearchMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == EmptyImageUrl), It.IsAny<CancellationToken>())).ReturnsAsync(string.Empty);
        _yandexImageSearchMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == InvalidImageUrl), It.IsAny<CancellationToken>())).ThrowsAsync(new YandexException("Invalid image."));

        var emoteProvider = Mock.Of<FergunEmoteProvider>();
        var sharedLogger = Mock.Of<ILogger<SharedModule>>();
        var sharedLocalizer = Utils.CreateMockedLocalizer<SharedResource>();
        var shared = new SharedModule(sharedLogger, sharedLocalizer, Mock.Of<IFergunTranslator>(), new GoogleTranslator2());

        var interactive = new InteractiveService(_client, _interactiveConfig);
        _moduleMock = new Mock<OcrModule>(() => new OcrModule(_loggerMock.Object, _ocrLocalizer, emoteProvider, shared, interactive,
            _googleLensMock.Object, _bingVisualSearchMock.Object, _yandexImageSearchMock.Object))
        { CallBase = true };
        _contextMock.SetupGet(x => x.Interaction).Returns(_interactionMock.Object);
        ((IInteractionModuleBase)_moduleMock.Object).SetContext(_contextMock.Object);
    }

    [Fact]
    public void BeforeExecute_Sets_Language()
    {
        _interactionMock.SetupGet(x => x.UserLocale).Returns("en");
        _moduleMock.Object.BeforeExecute(It.IsAny<ICommandInfo>());
        Assert.Equal("en", _ocrLocalizer.CurrentCulture.TwoLetterISOLanguageName);
    }

    [Theory]
    [InlineData(TextImageUrl, true)]
    [InlineData(EmptyImageUrl, false)]
    public async Task GoogleAsync_Uses_GoogleLens(string url, bool success)
    {
        var module = _moduleMock.Object;
        const bool isEphemeral = false;

        var result = await module.GoogleAsync(url);
        Assert.Equal(success, result.IsSuccess);

        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => b == isEphemeral), It.IsAny<RequestOptions>()), Times.Once);

        _googleLensMock.Verify(x => x.OcrAsync(It.Is<string>(s => s == url), It.IsAny<CancellationToken>()), Times.Once);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => b == isEphemeral),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(), It.IsAny<PollProperties>(), It.IsAny<MessageFlags>()), success ? Times.Once : Times.Never);
    }

    [Theory]
    [InlineData(TextImageUrl, true)]
    [InlineData(EmptyImageUrl, false)]
    public async Task BingAsync_Uses_BingVisualSearch(string url, bool success)
    {
        var module = _moduleMock.Object;
        const bool isEphemeral = false;

        var result = await module.BingAsync(url);
        Assert.Equal(success, result.IsSuccess);

        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => b == isEphemeral), It.IsAny<RequestOptions>()), Times.Once);

        _bingVisualSearchMock.Verify(x => x.OcrAsync(It.Is<string>(s => s == url), It.IsAny<CancellationToken>()), Times.Once);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => b == isEphemeral),
                It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(), It.IsAny<PollProperties>(), It.IsAny<MessageFlags>()), success ? Times.Once : Times.Never);
    }

    [Theory]
    [InlineData(TextImageUrl, true)]
    [InlineData(EmptyImageUrl, false)]
    public async Task YandexAsync_Uses_YandexImageSearch(string url, bool success)
    {
        var module = _moduleMock.Object;
        const bool isEphemeral = false;

        var result = await module.YandexAsync(url);
        Assert.Equal(success, result.IsSuccess);

        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => b == isEphemeral), It.IsAny<RequestOptions>()), Times.Once);

        _yandexImageSearchMock.Verify(x => x.OcrAsync(It.Is<string>(s => s == url), It.IsAny<CancellationToken>()), Times.Once);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => b == isEphemeral),
                It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(), It.IsAny<PollProperties>(), It.IsAny<MessageFlags>()), success ? Times.Once : Times.Never);
    }

    [Fact]
    public async Task OcrAsync_Returns_Unsuccessful_Result_On_Empty_Image_Url()
    {
        var module = _moduleMock.Object;
        const bool isEphemeral = true;

        var result = await module.OcrAsync(It.IsAny<OcrEngine>(), string.Empty, _interactionMock.Object, null, isEphemeral);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Invalid_OcrEngine_Throws_ArgumentException()
    {
        var module = _moduleMock.Object;
        const bool isEphemeral = true;

        var task = module.OcrAsync((OcrEngine)3, TextImageUrl, _interactionMock.Object, null, isEphemeral);

        await Assert.ThrowsAsync<ArgumentException>("ocrEngine", () => task);
    }

    [Fact]
    public async Task OcrAsync_Returns_Unsuccessful_Result_On_Invalid_Image_Url()
    {
        var module = _moduleMock.Object;
        const bool isEphemeral = true;

        var result = await module.OcrAsync(It.IsAny<OcrEngine>(), InvalidImageUrl, _interactionMock.Object, null, isEphemeral);
        Assert.False(result.IsSuccess);

        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => b == isEphemeral), It.IsAny<RequestOptions>()), Times.Once);
    }
}