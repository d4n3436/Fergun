﻿using System;
using System.Collections.Generic;
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
    private readonly IBingVisualSearch _bingVisualSearch = Utils.CreateMockedBingVisualSearchApi();
    private readonly IYandexImageSearch _yandexImageSearch = Utils.CreateMockedYandexImageSearchApi();
    private readonly IGoogleLensClient _googleLens = Utils.CreateMockedGoogleLensClient();
    private readonly DiscordSocketClient _client = new();
    private readonly IFergunLocalizer<ImageModule> _localizer = Utils.CreateMockedLocalizer<ImageModule>();
    private readonly Mock<ImageModule> _moduleMock;
    private readonly ImageModule _module;

    public ImageModuleTests()
    {
        var emoteProvider = Mock.Of<FergunEmoteProvider>();
        var logger = Mock.Of<ILogger<ImageModule>>();
        var options = Utils.CreateMockedFergunOptions();
        var interactive = new InteractiveService(_client, new InteractiveConfig { DeferStopSelectionInteractions = false, ReturnAfterSendingPaginator = true });
        _moduleMock = new Mock<ImageModule>(() => new ImageModule(logger, _localizer, options, emoteProvider, interactive,
            _googleScraper, _duckDuckGoScraper, _bingVisualSearch, _yandexImageSearch, _googleLens))
        { CallBase = true };

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
        Assert.True(result.IsSuccess, result.ErrorReason);

        _interactionMock.Verify(x => x.DeferAsync(It.IsAny<bool>(), It.IsAny<RequestOptions>()), Times.Once);
        _contextMock.VerifyGet(x => x.Channel);
        _interactionMock.VerifyGet(x => x.User);
        channel.VerifyGet(x => x.IsNsfw);

        _interactionMock.Verify(x => x.FollowupWithFilesAsync(It.IsAny<IEnumerable<FileAttachment>>(), It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(), It.IsAny<PollProperties>(), It.IsAny<MessageFlags>()), Times.Once);
    }

    [Fact]
    public async Task GoogleAsync_Returns_No_Results()
    {
        var result = await _module.GoogleAsync(" ");
        Assert.False(result.IsSuccess);

        Mock.Get(_localizer).VerifyGet(x => x[It.Is<string>(y => y == "NoResults")]);
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
        Assert.True(result.IsSuccess, result.ErrorReason);

        _interactionMock.Verify(x => x.DeferAsync(It.IsAny<bool>(), It.IsAny<RequestOptions>()), Times.Once);
        _contextMock.VerifyGet(x => x.Channel);
        _interactionMock.VerifyGet(x => x.User);
        channel.VerifyGet(x => x.IsNsfw);

        _interactionMock.Verify(x => x.FollowupWithFilesAsync(It.IsAny<IEnumerable<FileAttachment>>(), It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(), It.IsAny<PollProperties>(), It.IsAny<MessageFlags>()), Times.Once);
    }

    [Fact]
    public async Task DuckDuckGoAsync_Returns_No_Results()
    {
        var result = await _module.DuckDuckGoAsync("\u200b");
        Assert.False(result.IsSuccess);

        Mock.Get(_localizer).VerifyGet(x => x[It.Is<string>(y => y == "NoResults")]);
    }

    [Theory]
    [MemberData(nameof(GetReverseImageSearchData))]
    public async Task ReverseAsync_Sends_Paginator(string? url, string? attachmentUrl, ReverseImageSearchEngine engine, bool multiImages, bool nsfw)
    {
        var fileMock = new Mock<IAttachment>();
        fileMock.SetupGet(x => x.Url).Returns(attachmentUrl!);
        var file = attachmentUrl is null ? null : fileMock.Object;

        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(nsfw);
        _contextMock.SetupGet(x => x.Channel).Returns(channel.Object);
        _interactionMock.SetupGet(x => x.UserLocale).Returns("en");

        var result = await _module.ReverseAsync(url, file, engine, multiImages);
        Assert.True(result.IsSuccess, result.ErrorReason);

        _interactionMock.VerifyGet(x => x.User);
        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => !b), It.IsAny<RequestOptions>()), Times.Once);
        _interactionMock.Verify(x => x.FollowupWithFilesAsync(It.IsAny<IEnumerable<FileAttachment>>(), It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(), It.IsAny<PollProperties>(), It.IsAny<MessageFlags>()), Times.Once);

        if (engine == ReverseImageSearchEngine.Bing)
        {
            _moduleMock.Verify(x => x.ReverseBingAsync(It.Is<string>(s => s == (file == null ? url : file.Url)), It.Is<bool>(b => b == multiImages), It.IsAny<IDiscordInteraction>(), It.IsAny<IDiscordInteraction?>(), It.Is<bool>(b => !b)), Times.Once);
            Mock.Get(_bingVisualSearch).Verify(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == (file == null ? url : file.Url)), It.Is<BingSafeSearchLevel>(l => l == (nsfw ? BingSafeSearchLevel.Off : BingSafeSearchLevel.Strict)), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        else if (engine == ReverseImageSearchEngine.Yandex)
        {
            _moduleMock.Verify(x => x.ReverseYandexAsync(It.Is<string>(s => s == (file == null ? url : file.Url)), It.Is<bool>(b => b == multiImages), It.IsAny<IDiscordInteraction>(), It.IsAny<IDiscordInteraction?>(), It.Is<bool>(b => !b)), Times.Once);
            Mock.Get(_yandexImageSearch).Verify(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == (file == null ? url : file.Url)), It.Is<YandexSearchFilterMode>(l => l == (nsfw ? YandexSearchFilterMode.None : YandexSearchFilterMode.Family)), It.IsAny<CancellationToken>()), Times.Once);
        }
        else if (engine == ReverseImageSearchEngine.Google)
        {
            _moduleMock.Verify(x => x.ReverseGoogleAsync(It.Is<string>(s => s == (file == null ? url : file.Url)), It.Is<bool>(b => b == multiImages), It.IsAny<IDiscordInteraction>(), It.IsAny<IDiscordInteraction?>(), It.Is<bool>(b => !b)), Times.Once);
            Mock.Get(_googleLens).Verify(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == (file == null ? url : file.Url)), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Theory]
    [InlineData("", null, ReverseImageSearchEngine.Bing, true, false, "UrlNotWellFormed")]
    [InlineData("", null, ReverseImageSearchEngine.Yandex, true, true, "UrlNotWellFormed")]
    [InlineData(null, null, ReverseImageSearchEngine.Bing, false, true, "UrlOrAttachmentRequired")]
    [InlineData(null, null, ReverseImageSearchEngine.Yandex, false, true, "UrlOrAttachmentRequired")]
    public async Task ReverseAsync_Returns_No_Results(string? url, IAttachment? file, ReverseImageSearchEngine engine, bool multiImages, bool nsfw, string message)
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
        await Assert.ThrowsAsync<ArgumentException>("engine", () => _module.ReverseAsync("https://example.com/image.png", It.IsAny<IAttachment>(), (ReverseImageSearchEngine)3, It.IsAny<bool>()));
    }

    [Theory]
    [InlineData(ReverseImageSearchEngine.Bing)]
    [InlineData(ReverseImageSearchEngine.Yandex)]
    public async Task ReverseAsync_Throws_Exception_If_Invalid_Parameters_Are_Passed(ReverseImageSearchEngine engine)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(false);
        _contextMock.SetupGet(x => x.Channel).Returns(channel.Object);

        var result = await _module.ReverseAsync("https://example.com/error", null, engine, It.IsAny<bool>());
        Assert.False(result.IsSuccess);
        Assert.Equal("Error message.", result.ErrorReason);

        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => !b), It.IsAny<RequestOptions>()), Times.Once);
    }

    public static TheoryData<string?, string?, ReverseImageSearchEngine, bool, bool> GetReverseImageSearchData()
    {
        return new TheoryData<string?, string?, ReverseImageSearchEngine, bool, bool>
        {
            { "https://example.com/image.png", null, ReverseImageSearchEngine.Bing, false, false },
            { null, "https://example.com/image.png", ReverseImageSearchEngine.Bing, true, true },
            { "https://example.com/image.png", null, ReverseImageSearchEngine.Yandex, false, false },
            { null, "https://example.com/image.png", ReverseImageSearchEngine.Yandex, true, true },
            { "https://example.com/image.png", null, ReverseImageSearchEngine.Google, false, false },
            { null, "https://example.com/image.png", ReverseImageSearchEngine.Google, true, true }
        };
    }
}