using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Bing;
using Fergun.Apis.Google;
using Fergun.Apis.Yandex;
using Fergun.Common;
using Fergun.Interactive;
using Fergun.Localization;
using Fergun.Modules;
using GTranslate.Translators;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Fergun.Tests.Modules;

public class OcrModuleTests : ModuleTestBase<OcrModule>
{
    private readonly Mock<IGoogleLensClient> _googleLensMock = new();
    private readonly Mock<IBingVisualSearch> _bingVisualSearchMock = new();
    private readonly Mock<IYandexImageSearch> _yandexImageSearchMock = new();

    public OcrModuleTests()
    {
        // Each engine's OcrAsync returns text for TextImageUrl, empty for EmptyImageUrl, and throws for InvalidImageUrl
        _googleLensMock.Setup(x => x.OcrAsync(TestData.TextImageUrl, It.IsAny<CancellationToken>())).ReturnsAsync("test");
        _googleLensMock.Setup(x => x.OcrAsync(TestData.EmptyImageUrl, It.IsAny<CancellationToken>())).ReturnsAsync(string.Empty);
        _googleLensMock.Setup(x => x.OcrAsync(TestData.InvalidImageUrl, It.IsAny<CancellationToken>())).ThrowsAsync(new GoogleLensException("Invalid image."));
        _bingVisualSearchMock.Setup(x => x.OcrAsync(TestData.TextImageUrl, It.IsAny<CancellationToken>())).ReturnsAsync("test");
        _bingVisualSearchMock.Setup(x => x.OcrAsync(TestData.EmptyImageUrl, It.IsAny<CancellationToken>())).ReturnsAsync(string.Empty);
        _bingVisualSearchMock.Setup(x => x.OcrAsync(TestData.InvalidImageUrl, It.IsAny<CancellationToken>())).ThrowsAsync(new BingException("Invalid image."));
        _yandexImageSearchMock.Setup(x => x.OcrAsync(TestData.TextImageUrl, It.IsAny<CancellationToken>())).ReturnsAsync("test");
        _yandexImageSearchMock.Setup(x => x.OcrAsync(TestData.EmptyImageUrl, It.IsAny<CancellationToken>())).ReturnsAsync(string.Empty);
        _yandexImageSearchMock.Setup(x => x.OcrAsync(TestData.InvalidImageUrl, It.IsAny<CancellationToken>())).ThrowsAsync(new YandexException("Invalid image."));

        var sharedLocalizer = Utils.CreateMockedLocalizer<SharedResource>();
        var shared = new SharedModule(Mock.Of<ILogger<SharedModule>>(), sharedLocalizer, Mock.Of<IFergunTranslator>(), new GoogleTranslator2());

        var interactive = new InteractiveService(Client, new InteractiveConfig { DeferStopSelectionInteractions = false });

        SetupModule(new Mock<OcrModule>(() => new OcrModule(Logger, Localizer, Emotes, interactive, shared,
            _googleLensMock.Object, _bingVisualSearchMock.Object, _yandexImageSearchMock.Object))
        { CallBase = true });
    }

    [Theory]
    [InlineData(OcrEngine.Google, TestData.TextImageUrl, true)]
    [InlineData(OcrEngine.Google, TestData.EmptyImageUrl, false)]
    [InlineData(OcrEngine.Bing, TestData.TextImageUrl, true)]
    [InlineData(OcrEngine.Bing, TestData.EmptyImageUrl, false)]
    [InlineData(OcrEngine.Yandex, TestData.TextImageUrl, true)]
    [InlineData(OcrEngine.Yandex, TestData.EmptyImageUrl, false)]
    public async Task SlashCommand_Uses_Matching_Engine(OcrEngine engine, string url, bool success)
    {
        const bool ephemeral = false; // The slash commands always defer non-ephemerally

        var result = await InvokeSlashCommandAsync(engine, url);
        Assert.Equal(success, result.IsSuccess);

        InteractionMock.VerifyDeferAsync(ephemeral, Times.Once());
        VerifyEngineCalled(engine, url, Times.Once());
        InteractionMock.VerifyFollowupAsync(ephemeral, success ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task SlashCommand_Renders_Recognized_Text_In_Response()
    {
        MessageComponent? captured = null;
        InteractionMock
            .Setup(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<AllowedMentions>(),
                It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(), It.IsAny<PollProperties>(), It.IsAny<MessageFlags>()))
            .Callback<string, Embed[], bool, bool, AllowedMentions, MessageComponent, Embed, RequestOptions, PollProperties, MessageFlags>(
                (_, _, _, _, _, components, _, _, _, _) => captured = components)
            .ReturnsAsync(Mock.Of<IUserMessage>());

        var result = await Module.GoogleAsync(TestData.TextImageUrl);
        Assert.True(result.IsSuccess);

        Assert.NotNull(captured);
        Assert.Contains(captured.Components.SelectMany(GetTextContent), content => content.Contains("test"));
    }

    [Fact]
    public async Task OcrAsync_Returns_Unsuccessful_Result_On_Empty_Image_Url()
    {
        var result = await Module.OcrAsync(OcrEngine.Google, string.Empty, InteractionMock.Object, ephemeral: true);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Invalid_OcrEngine_Throws_ArgumentException()
    {
        var task = Module.OcrAsync((OcrEngine)3, TestData.TextImageUrl, InteractionMock.Object, ephemeral: true);

        await Assert.ThrowsAsync<ArgumentException>("ocrEngine", () => task);
    }

    [Fact]
    public async Task OcrAsync_Returns_Unsuccessful_Result_On_Invalid_Image_Url()
    {
        const bool ephemeral = true;

        var result = await Module.OcrAsync(OcrEngine.Google, TestData.InvalidImageUrl, InteractionMock.Object, null, ephemeral);
        Assert.False(result.IsSuccess);

        InteractionMock.VerifyDeferAsync(ephemeral, Times.Once());
    }
    
    private static IEnumerable<string> GetTextContent(IMessageComponent component) => component switch
    {
        TextDisplayComponent text => [text.Content],
        ContainerComponent container => container.Components.SelectMany(GetTextContent),
        ActionRowComponent row => row.Components.SelectMany(GetTextContent),
        _ => []
    };

    private Task<RuntimeResult> InvokeSlashCommandAsync(OcrEngine engine, string url) => engine switch
    {
        OcrEngine.Google => Module.GoogleAsync(url),
        OcrEngine.Bing => Module.BingAsync(url),
        OcrEngine.Yandex => Module.YandexAsync(url),
        _ => throw new ArgumentOutOfRangeException(nameof(engine))
    };

    private void VerifyEngineCalled(OcrEngine engine, string url, Times times)
    {
        switch (engine)
        {
            case OcrEngine.Google:
                _googleLensMock.Verify(x => x.OcrAsync(It.Is<string>(s => s == url), It.IsAny<CancellationToken>()), times);
                break;
            case OcrEngine.Bing:
                _bingVisualSearchMock.Verify(x => x.OcrAsync(It.Is<string>(s => s == url), It.IsAny<CancellationToken>()), times);
                break;
            case OcrEngine.Yandex:
                _yandexImageSearchMock.Verify(x => x.OcrAsync(It.Is<string>(s => s == url), It.IsAny<CancellationToken>()), times);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(engine));
        }
    }
}