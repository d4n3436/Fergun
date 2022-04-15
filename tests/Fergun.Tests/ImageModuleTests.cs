using System;
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

namespace Fergun.Tests;

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
            _duckDuckGoScraper, _braveScraper, _bingVisualSearch, _yandexImageSearch))
        {
            CallBase = true
        };

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
    public async Task Google_Sends_Paginator(string query, bool multiImages, bool nsfw)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(nsfw);
        _contextMock.SetupGet(x => x.Channel).Returns(channel.Object);

        await _module.Google(query, multiImages);

        _interactionMock.Verify(x => x.DeferAsync(It.IsAny<bool>(), It.IsAny<RequestOptions>()), Times.Once);
        _contextMock.VerifyGet(x => x.Channel);
        _interactionMock.VerifyGet(x => x.User);
        channel.VerifyGet(x => x.IsNsfw);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
    }

    [Fact]
    public async Task Google_Returns_No_Results()
    {
        await _module.Google(" ");

        Mock.Get(_localizer).VerifyGet(x => x[It.Is<string>(y => y == "No results.")]);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
    }

    [Theory]
    [InlineData("Discord", false, true)]
    [InlineData("DuckDuckGo", true, false)]
    public async Task DuckDuckGo_Sends_Paginator(string query, bool multiImages, bool nsfw)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(nsfw);
        _contextMock.SetupGet(x => x.Channel).Returns(channel.Object);

        await _module.DuckDuckGo(query, multiImages);

        _interactionMock.Verify(x => x.DeferAsync(It.IsAny<bool>(), It.IsAny<RequestOptions>()), Times.Once);
        _contextMock.VerifyGet(x => x.Channel);
        _interactionMock.VerifyGet(x => x.User);
        channel.VerifyGet(x => x.IsNsfw);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
    }

    [Fact]
    public async Task DuckDuckGo_Returns_No_Results()
    {
        await _module.DuckDuckGo("\u200b");

        Mock.Get(_localizer).VerifyGet(x => x[It.Is<string>(y => y == "No results.")]);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
    }

    [Theory]
    [InlineData("Discord", false, true)]
    [InlineData("Brave", true, false)]
    public async Task Brave_Sends_Paginator(string query, bool multiImages, bool nsfw)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(nsfw);
        _contextMock.SetupGet(x => x.Channel).Returns(channel.Object);

        await _module.Brave(query, multiImages);

        _interactionMock.Verify(x => x.DeferAsync(It.IsAny<bool>(), It.IsAny<RequestOptions>()), Times.Once);
        _contextMock.VerifyGet(x => x.Channel);
        _interactionMock.VerifyGet(x => x.User);
        channel.VerifyGet(x => x.IsNsfw);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
    }

    [Fact]
    public async Task Brave_Returns_No_Results()
    {
        await _module.Brave("\u200b");

        Mock.Get(_localizer).VerifyGet(x => x[It.Is<string>(y => y == "No results.")]);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
    }

    [Theory]
    [InlineData("https://example.com/image.png", ImageModule.ReverseImageSearchEngine.Bing, false, false)]
    [InlineData("https://example.com/image.png", ImageModule.ReverseImageSearchEngine.Bing, true, true)]
    [InlineData("", ImageModule.ReverseImageSearchEngine.Bing, true, false)]
    [InlineData("https://example.com/image.png", ImageModule.ReverseImageSearchEngine.Yandex, false, false)]
    [InlineData("https://example.com/image.png", ImageModule.ReverseImageSearchEngine.Yandex, true, true)]
    [InlineData("", ImageModule.ReverseImageSearchEngine.Yandex, true, true)]
    public async Task Reverse_Sends_Paginator(string url, ImageModule.ReverseImageSearchEngine engine, bool multiImages, bool nsfw)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(nsfw);
        _contextMock.SetupGet(x => x.Channel).Returns(channel.Object);

        await _module.Reverse(url, engine, multiImages);

        _contextMock.VerifyGet(x => x.Channel);
        _interactionMock.VerifyGet(x => x.User);
        channel.VerifyGet(x => x.IsNsfw);
        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => !b), It.IsAny<RequestOptions>()), Times.Once);
        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);

        if (engine == ImageModule.ReverseImageSearchEngine.Bing)
        {
            _moduleMock.Verify(x => x.BingAsync(It.Is<string>(s => s == url), It.Is<bool>(b => b == multiImages), It.IsAny<IDiscordInteraction>(), It.Is<bool>(b => !b)), Times.Once);
            Mock.Get(_bingVisualSearch).Verify(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == url), It.Is<BingSafeSearchLevel>(l => l == (nsfw ? BingSafeSearchLevel.Off : BingSafeSearchLevel.Strict))), Times.Once);
        }
        else if (engine == ImageModule.ReverseImageSearchEngine.Yandex)
        {
            _moduleMock.Verify(x => x.YandexAsync(It.Is<string>(s => s == url), It.Is<bool>(b => b == multiImages), It.IsAny<IDiscordInteraction>(), It.Is<bool>(b => !b)), Times.Once);
            Mock.Get(_yandexImageSearch).Verify(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == url), It.Is<YandexSearchFilterMode>(l => l == (nsfw ? YandexSearchFilterMode.None : YandexSearchFilterMode.Family))), Times.Once);
        }
    }

    [Fact]
    public async Task Reverse_Throws_Exception_If_Invalid_Engine_Is_Passed()
    {
        await Assert.ThrowsAsync<ArgumentException>("engine", () => _module.Reverse(It.IsAny<string>(), (ImageModule.ReverseImageSearchEngine)2, It.IsAny<bool>()));
    }

    [Theory]
    [InlineData(ImageModule.ReverseImageSearchEngine.Bing)]
    [InlineData(ImageModule.ReverseImageSearchEngine.Yandex)]
    public async Task Reverse_Throws_Exception_If_Invalid_Parameters_Are_Passed(ImageModule.ReverseImageSearchEngine engine)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(false);
        _contextMock.SetupGet(x => x.Channel).Returns(channel.Object);

        await _module.Reverse(null!, engine, It.IsAny<bool>());

        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => !b), It.IsAny<RequestOptions>()), Times.Once);
        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => !b),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.Is<Embed>(e => e.Description.EndsWith("Error message.")), It.IsAny<RequestOptions>()), Times.Once);
    }
}