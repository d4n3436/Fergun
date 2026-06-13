using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Fergun.Apis.Bing;
using Fergun.Apis.Google;
using Fergun.Apis.Yandex;
using Fergun.Common;
using Fergun.Interactive;
using Fergun.Modules;
using GScraper.DuckDuckGo;
using GScraper.Google;
using Moq;
using Xunit;

namespace Fergun.Tests.Modules;

public class ImageModuleTests : ModuleTestBase<ImageModule>
{
    private readonly GoogleScraper _googleScraper = new();
    private readonly DuckDuckGoScraper _duckDuckGoScraper = new();
    private readonly IBingVisualSearch _bingVisualSearch = Utils.CreateMockedBingVisualSearchApi();
    private readonly IYandexImageSearch _yandexImageSearch = Utils.CreateMockedYandexImageSearchApi();
    private readonly IGoogleLensClient _googleLens = Utils.CreateMockedGoogleLensClient();

    public ImageModuleTests()
    {
        var options = Utils.CreateMockedFergunOptions();
        var interactive = new InteractiveService(Client, new InteractiveConfig { DeferStopSelectionInteractions = false, ReturnAfterSendingPaginator = true });

        SetupModule(new Mock<ImageModule>(() => new ImageModule(Logger, Localizer, Emotes, interactive, options,
            _googleScraper, _duckDuckGoScraper, _bingVisualSearch, _yandexImageSearch, _googleLens))
        { CallBase = true });

        InteractionMock.SetupGet(x => x.User).Returns(() => Utils.CreateMockedUser());
        ContextMock.SetupGet(x => x.User).Returns(InteractionMock.Object.User);
    }

    [Theory]
    [InlineData("Discord", false, true)]
    [InlineData("Google", true, false)]
    public async Task GoogleAsync_Sends_Paginator(string query, bool multiImages, bool nsfw)
    {
        SetupChannel(nsfw);

        var result = await Module.GoogleAsync(query, multiImages);
        Assert.True(result.IsSuccess, result.ErrorReason);

        InteractionMock.VerifyDeferAsync(false, Times.Once());
        InteractionMock.VerifyFollowupWithFilesAsync(Times.Once());
    }

    [Fact]
    public async Task GoogleAsync_Returns_No_Results()
    {
        var result = await Module.GoogleAsync(" ");
        Assert.False(result.IsSuccess);

        Mock.Get(Localizer).VerifyGet(x => x["NoResults"]);
    }

    [Trait("Category", "Integration")]
    [Theory]
    [InlineData("Discord", false, true)]
    [InlineData("DuckDuckGo", true, false)]
    public async Task DuckDuckGoAsync_Sends_Paginator(string query, bool multiImages, bool nsfw)
    {
        SetupChannel(nsfw);

        var result = await Module.DuckDuckGoAsync(query, multiImages);
        Assert.True(result.IsSuccess, result.ErrorReason);

        InteractionMock.VerifyDeferAsync(false, Times.Once());
        InteractionMock.VerifyFollowupWithFilesAsync(Times.Once());
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task DuckDuckGoAsync_Returns_No_Results()
    {
        var result = await Module.DuckDuckGoAsync("\u200b");
        Assert.False(result.IsSuccess);

        Mock.Get(Localizer).VerifyGet(x => x["NoResults"]);
    }

    [Theory]
    [MemberData(nameof(GetReverseImageSearchData))]
    public async Task ReverseAsync_Sends_Paginator(string? url, string? attachmentUrl, ReverseImageSearchEngine engine, bool multiImages, bool nsfw)
    {
        var fileMock = new Mock<IAttachment>();
        fileMock.SetupGet(x => x.Url).Returns(attachmentUrl!);
        var file = attachmentUrl is null ? null : fileMock.Object;

        SetupChannel(nsfw);
        InteractionMock.SetupGet(x => x.UserLocale).Returns("en");

        var result = await Module.ReverseAsync(url, file, engine, multiImages);
        Assert.True(result.IsSuccess, result.ErrorReason);

        InteractionMock.VerifyGet(x => x.User);
        InteractionMock.VerifyDeferAsync(false, Times.Once());
        InteractionMock.VerifyFollowupWithFilesAsync(Times.Once());

        string expectedUrl = file?.Url ?? url!;
        switch (engine)
        {
            case ReverseImageSearchEngine.Bing:
                ModuleMock.Verify(x => x.ReverseBingAsync(expectedUrl, multiImages, It.IsAny<IDiscordInteraction>(), It.IsAny<IDiscordInteraction?>(), false), Times.Once);
                Mock.Get(_bingVisualSearch).Verify(x => x.ReverseImageSearchAsync(expectedUrl, nsfw ? BingSafeSearchLevel.Off : BingSafeSearchLevel.Strict, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
                break;
            case ReverseImageSearchEngine.Yandex:
                ModuleMock.Verify(x => x.ReverseYandexAsync(expectedUrl, multiImages, It.IsAny<IDiscordInteraction>(), It.IsAny<IDiscordInteraction?>(), false), Times.Once);
                Mock.Get(_yandexImageSearch).Verify(x => x.ReverseImageSearchAsync(expectedUrl, nsfw ? YandexSearchFilterMode.None : YandexSearchFilterMode.Family, It.IsAny<CancellationToken>()), Times.Once);
                break;
            case ReverseImageSearchEngine.Google:
                ModuleMock.Verify(x => x.ReverseGoogleAsync(expectedUrl, multiImages, It.IsAny<IDiscordInteraction>(), It.IsAny<IDiscordInteraction?>(), false), Times.Once);
                Mock.Get(_googleLens).Verify(x => x.ReverseImageSearchAsync(expectedUrl, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(engine));
        }
    }

    [Theory]
    [InlineData("", null, ReverseImageSearchEngine.Bing, true, false, "UrlNotWellFormed")]
    [InlineData("", null, ReverseImageSearchEngine.Yandex, true, true, "UrlNotWellFormed")]
    [InlineData(null, null, ReverseImageSearchEngine.Bing, false, true, "UrlOrAttachmentRequired")]
    [InlineData(null, null, ReverseImageSearchEngine.Yandex, false, true, "UrlOrAttachmentRequired")]
    public async Task ReverseAsync_Returns_No_Results(string? url, IAttachment? file, ReverseImageSearchEngine engine, bool multiImages, bool nsfw, string message)
    {
        SetupChannel(nsfw);
        InteractionMock.SetupGet(x => x.UserLocale).Returns("en");

        var result = await Module.ReverseAsync(url, file, engine, multiImages);
        Assert.False(result.IsSuccess);

        Mock.Get(Localizer).VerifyGet(x => x[message]);
    }

    [Fact]
    public async Task ReverseAsync_Throws_Exception_If_Invalid_Engine_Is_Passed()
    {
        await Assert.ThrowsAsync<ArgumentException>("engine", () => Module.ReverseAsync(TestData.TextImageUrl, null, (ReverseImageSearchEngine)3, multiImages: false));
    }

    [Theory]
    [InlineData(ReverseImageSearchEngine.Bing)]
    [InlineData(ReverseImageSearchEngine.Yandex)]
    public async Task ReverseAsync_Throws_Exception_If_Invalid_Parameters_Are_Passed(ReverseImageSearchEngine engine)
    {
        SetupChannel(nsfw: false);

        var result = await Module.ReverseAsync(TestData.ErrorImageUrl, null, engine, multiImages: false);
        Assert.False(result.IsSuccess);
        Assert.Equal("Error message.", result.ErrorReason);

        InteractionMock.VerifyDeferAsync(false, Times.Once());
    }

    private void SetupChannel(bool nsfw)
    {
        var channel = new Mock<ITextChannel>();
        channel.SetupGet(x => x.IsNsfw).Returns(nsfw);
        ContextMock.SetupGet(x => x.Channel).Returns(channel.Object);
    }

    public static TheoryData<string?, string?, ReverseImageSearchEngine, bool, bool> GetReverseImageSearchData()
    {
        return new TheoryData<string?, string?, ReverseImageSearchEngine, bool, bool>
        {
            { TestData.TextImageUrl, null, ReverseImageSearchEngine.Bing, false, false },
            { null, TestData.TextImageUrl, ReverseImageSearchEngine.Bing, true, true },
            { TestData.TextImageUrl, null, ReverseImageSearchEngine.Yandex, false, false },
            { null, TestData.TextImageUrl, ReverseImageSearchEngine.Yandex, true, true },
            { TestData.TextImageUrl, null, ReverseImageSearchEngine.Google, false, false },
            { null, TestData.TextImageUrl, ReverseImageSearchEngine.Google, true, true }
        };
    }
}