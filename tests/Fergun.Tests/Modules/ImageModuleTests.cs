using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Apis.Bing;
using Fergun.Apis.Yandex;
using Fergun.Interactive;
using Fergun.Modules;
using GScraper.Brave;
using GScraper.DuckDuckGo;
using GScraper.Google;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Fergun.Tests.Modules;

public class ImageModuleTests
{
    private readonly Mock<IInteractionContext> _contextMock = new();
    private readonly Mock<IDiscordInteraction> _interactionMock = new();
    private readonly GoogleScraper _googleScraper = new();
    private readonly DuckDuckGoScraper _duckDuckGoScraper = new();
    private readonly BraveScraper _braveScraper = new();
    private readonly IBingVisualSearch _bingVisualSearch = Utils.CreateMockedBingVisualSearchApi();
    private readonly IYandexImageSearch _yandexImageSearch = Utils.CreateMockedYandexImageSearchApi();
    private readonly DiscordSocketClient _client = new();
    private readonly IFergunLocalizer<ImageModule> _localizer = Utils.CreateMockedLocalizer<ImageModule>();
    private readonly Mock<ImageModule> _moduleMock;
    private readonly ImageModule _module;

    public ImageModuleTests()
    {
        var logger = Mock.Of<ILogger<ImageModule>>();
        var interactive = new InteractiveService(_client, new InteractiveConfig { DeferStopSelectionInteractions = false, ReturnAfterSendingPaginator = true });
        _moduleMock = new Mock<ImageModule>(() => new ImageModule(logger, _localizer, interactive, _googleScraper,
            _duckDuckGoScraper, _braveScraper, _bingVisualSearch, _yandexImageSearch)) { CallBase = true };

        _module = _moduleMock.Object;
        _interactionMock.SetupGet(x => x.User).Returns(() => Utils.CreateMockedUser());
        _contextMock.SetupGet(x => x.Interaction).Returns(_interactionMock.Object);
        ((IInteractionModuleBase)_moduleMock.Object).SetContext(_contextMock.Object);
        _contextMock.SetupGet(x => x.User).Returns(_interactionMock.Object.User);
    }

    [Fact]
    public void BeforeExecute_Sets_Language()
    {
        _interactionMock.SetupGet(x => x.UserLocale).Returns("en");
        _module.BeforeExecute(It.IsAny<ICommandInfo>());
        Assert.Equal("en", _localizer.CurrentCulture.TwoLetterISOLanguageName);
    }

    [Theory]
    [InlineData("Discord", false, true)]
    [InlineData("Google", true, false)]
    public async Task GoogleAsync_Sends_Paginator(string query, bool multiImages, bool nsfw)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(nsfw);
        _contextMock.SetupGet(x => x.Channel).Returns(channel.Object);

        var result = await _module.GoogleAsync(query, multiImages);
        Assert.True(result.IsSuccess);

        _interactionMock.Verify(x => x.DeferAsync(It.IsAny<bool>(), It.IsAny<RequestOptions>()), Times.Once);
        _contextMock.VerifyGet(x => x.Channel);
        _interactionMock.VerifyGet(x => x.User);
        channel.VerifyGet(x => x.IsNsfw);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
    }

    [Fact]
    public async Task GoogleAsync_Returns_No_Results()
    {
        var result = await _module.GoogleAsync(" ");
        Assert.False(result.IsSuccess);

        Mock.Get(_localizer).VerifyGet(x => x[It.Is<string>(y => y == "No results.")]);
    }

    [Theory]
    [InlineData("Discord", false, true)]
    [InlineData("DuckDuckGo", true, false)]
    public async Task DuckDuckGoAsync_Sends_Paginator(string query, bool multiImages, bool nsfw)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(nsfw);
        _contextMock.SetupGet(x => x.Channel).Returns(channel.Object);

        var result = await _module.DuckDuckGoAsync(query, multiImages);
        Assert.True(result.IsSuccess);

        _interactionMock.Verify(x => x.DeferAsync(It.IsAny<bool>(), It.IsAny<RequestOptions>()), Times.Once);
        _contextMock.VerifyGet(x => x.Channel);
        _interactionMock.VerifyGet(x => x.User);
        channel.VerifyGet(x => x.IsNsfw);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
    }

    [Fact]
    public async Task DuckDuckGoAsync_Returns_No_Results()
    {
        var result = await _module.DuckDuckGoAsync("\u200b");
        Assert.False(result.IsSuccess);

        Mock.Get(_localizer).VerifyGet(x => x[It.Is<string>(y => y == "No results.")]);
    }

    [Theory]
    [InlineData("Discord", false, true)]
    [InlineData("Brave", true, false)]
    public async Task BraveAsync_Sends_Paginator(string query, bool multiImages, bool nsfw)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(nsfw);
        _contextMock.SetupGet(x => x.Channel).Returns(channel.Object);

        var result = await _module.BraveAsync(query, multiImages);
        Assert.True(result.IsSuccess);

        _interactionMock.Verify(x => x.DeferAsync(It.IsAny<bool>(), It.IsAny<RequestOptions>()), Times.Once);
        _contextMock.VerifyGet(x => x.Channel);
        _interactionMock.VerifyGet(x => x.User);
        channel.VerifyGet(x => x.IsNsfw);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
    }

    [Fact]
    public async Task BraveAsync_Returns_No_Results()
    {
        var result = await _module.BraveAsync("\u200b");
        Assert.False(result.IsSuccess);

        Mock.Get(_localizer).VerifyGet(x => x[It.Is<string>(y => y == "No results.")]);
    }

    [Theory]
    [MemberData(nameof(GetReverseImageSearchData))]
    public async Task ReverseAsync_Sends_Paginator(string? url, IAttachment? file, ImageModule.ReverseImageSearchEngine engine, bool multiImages, bool nsfw)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(nsfw);
        _contextMock.SetupGet(x => x.Channel).Returns(channel.Object);
        _interactionMock.SetupGet(x => x.UserLocale).Returns("en");

        var result = await _module.ReverseAsync(url, file, engine, multiImages);
        Assert.True(result.IsSuccess);

        _contextMock.VerifyGet(x => x.Channel);
        _interactionMock.VerifyGet(x => x.User);
        channel.VerifyGet(x => x.IsNsfw);
        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => !b), It.IsAny<RequestOptions>()), Times.Once);
        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);

        if (engine == ImageModule.ReverseImageSearchEngine.Bing)
        {
            _moduleMock.Verify(x => x.BingAsync(It.Is<string>(s => s == (file == null ? url : file.Url)), It.Is<bool>(b => b == multiImages), It.IsAny<IDiscordInteraction>(), It.Is<bool>(b => !b)), Times.Once);
            Mock.Get(_bingVisualSearch).Verify(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == (file == null ? url : file.Url)), It.Is<BingSafeSearchLevel>(l => l == (nsfw ? BingSafeSearchLevel.Off : BingSafeSearchLevel.Strict)), It.IsAny<string>()), Times.Once);
        }
        else if (engine == ImageModule.ReverseImageSearchEngine.Yandex)
        {
            _moduleMock.Verify(x => x.YandexAsync(It.Is<string>(s => s == (file == null ? url : file.Url)), It.Is<bool>(b => b == multiImages), It.IsAny<IDiscordInteraction>(), It.Is<bool>(b => !b)), Times.Once);
            Mock.Get(_yandexImageSearch).Verify(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == (file == null ? url : file.Url)), It.Is<YandexSearchFilterMode>(l => l == (nsfw ? YandexSearchFilterMode.None : YandexSearchFilterMode.Family))), Times.Once);
        }
    }

    [Theory]
    [InlineData("", null, ImageModule.ReverseImageSearchEngine.Bing, true, false, "No results.")]
    [InlineData("", null, ImageModule.ReverseImageSearchEngine.Yandex, true, true, "No results.")]
    [InlineData(null, null, ImageModule.ReverseImageSearchEngine.Bing, false, true, "A URL or attachment is required.")]
    [InlineData(null, null, ImageModule.ReverseImageSearchEngine.Yandex, false, true, "A URL or attachment is required.")]
    public async Task ReverseAsync_Returns_No_Results(string? url, IAttachment? file, ImageModule.ReverseImageSearchEngine engine, bool multiImages, bool nsfw, string message)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(nsfw);
        _contextMock.SetupGet(x => x.Channel).Returns(channel.Object);
        _interactionMock.SetupGet(x => x.UserLocale).Returns("en");

        var result = await _module.ReverseAsync(url, file, engine, multiImages);
        Assert.False(result.IsSuccess);

        Mock.Get(_localizer).VerifyGet(x => x[It.Is<string>(y => y == message)]);
    }

    [Fact]
    public async Task ReverseAsync_Throws_Exception_If_Invalid_Engine_Is_Passed()
    {
        await Assert.ThrowsAsync<ArgumentException>("engine", () => _module.ReverseAsync("", It.IsAny<IAttachment>(), (ImageModule.ReverseImageSearchEngine)2, It.IsAny<bool>()));
    }

    [Theory]
    [InlineData(ImageModule.ReverseImageSearchEngine.Bing)]
    [InlineData(ImageModule.ReverseImageSearchEngine.Yandex)]
    public async Task ReverseAsync_Throws_Exception_If_Invalid_Parameters_Are_Passed(ImageModule.ReverseImageSearchEngine engine)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(false);
        _contextMock.SetupGet(x => x.Channel).Returns(channel.Object);

        var result = await _module.ReverseAsync("https://example.com/error", null, engine, It.IsAny<bool>());
        Assert.False(result.IsSuccess);
        Assert.Equal("Error message.", result.ErrorReason);

        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => !b), It.IsAny<RequestOptions>()), Times.Once);
    }

    private static IEnumerable<object?[]> GetReverseImageSearchData()
    {
        var attachmentMock = new Mock<IAttachment>();
        attachmentMock.SetupGet(x => x.Url).Returns("https://example.com/image.png");

        return new[]
        {
            new object?[] { "https://example.com/image.png", null, ImageModule.ReverseImageSearchEngine.Bing, false, false },
            new object?[] { null, attachmentMock.Object, ImageModule.ReverseImageSearchEngine.Bing, true, true },
            new object?[] { "https://example.com/image.png", null, ImageModule.ReverseImageSearchEngine.Yandex, false, false },
            new object?[] { null, attachmentMock.Object, ImageModule.ReverseImageSearchEngine.Yandex, true, true }
        };
    }
}