using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoBogus;
using AutoBogus.Moq;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Apis.Urban;
using Fergun.Interactive;
using Fergun.Modules;
using Moq;
using Xunit;

namespace Fergun.Tests.Modules;

public class UrbanModuleTests
{
    private readonly Mock<IInteractionContext> _contextMock = new();
    private readonly Mock<IDiscordInteraction> _interactionMock = new();
    private readonly IUrbanDictionary _urbanDictionary = Utils.CreateMockedUrbanDictionaryApi();
    private readonly Mock<UrbanModule> _moduleMock;
    private readonly DiscordSocketClient _client = new();
    private readonly InteractiveConfig _interactiveConfig = new() { ReturnAfterSendingPaginator = true };
    private readonly IFergunLocalizer<UrbanModule> _localizer = Utils.CreateMockedLocalizer<UrbanModule>();

    public UrbanModuleTests()
    {
        var options = Utils.CreateMockedFergunOptions();
        var interactive = new InteractiveService(_client, _interactiveConfig);

        _moduleMock = new Mock<UrbanModule>(() => new UrbanModule(_localizer, options, _urbanDictionary, interactive)) { CallBase = true };
        _contextMock.SetupGet(x => x.Interaction).Returns(_interactionMock.Object);
        _contextMock.SetupGet(x => x.User).Returns(() => AutoFaker.Generate<IUser>(b => b.WithBinder(new MoqBinder())));
        ((IInteractionModuleBase)_moduleMock.Object).SetContext(_contextMock.Object);
    }

    [Fact]
    public void BeforeExecute_Sets_Language()
    {
        _interactionMock.SetupGet(x => x.UserLocale).Returns("en");
        _moduleMock.Object.BeforeExecute(It.IsAny<ICommandInfo>());
        Assert.Equal("en", _localizer.CurrentCulture.TwoLetterISOLanguageName);
    }

    [Theory]
    [MemberData(nameof(GetRandomWords))]
    public async Task SearchAsync_Returns_Definitions(string term)
    {
        var result = await _moduleMock.Object.SearchAsync(term);
        Assert.True(result.IsSuccess);

        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => !b), It.IsAny<RequestOptions>()), Times.Once);
        Mock.Get(_urbanDictionary).Verify(u => u.GetDefinitionsAsync(It.Is<string>(x => x == term)), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    public async Task SearchAsync_Returns_No_Definitions(string term)
    {
        var result = await _moduleMock.Object.SearchAsync(term);
        Assert.False(result.IsSuccess);

        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => !b), It.IsAny<RequestOptions>()), Times.Once);
        Mock.Get(_urbanDictionary).Verify(u => u.GetDefinitionsAsync(It.Is<string>(x => x == term)), Times.Once);
    }

    [Fact]
    public async Task RandomAsync_Calls_GetRandomDefinitionsAsync()
    {
        var result = await _moduleMock.Object.RandomAsync();
        Assert.True(result.IsSuccess);

        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => !b), It.IsAny<RequestOptions>()), Times.Once);
        Mock.Get(_urbanDictionary).Verify(u => u.GetRandomDefinitionsAsync(), Times.Once);
    }

    [Fact]
    public async Task WordsOfTheDayAsync_Calls_GetWordsOfTheDayAsync()
    {
        var result = await _moduleMock.Object.WordsOfTheDayAsync();
        Assert.True(result.IsSuccess);

        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => !b), It.IsAny<RequestOptions>()), Times.Once);
        Mock.Get(_urbanDictionary).Verify(u => u.GetWordsOfTheDayAsync(), Times.Once);
    }

    [Fact]
    public async Task Invalid_SearchType_Throws_ArgumentException()
    {
        var task = _moduleMock.Object.SearchAndSendAsync((UrbanModule.UrbanSearchType)3);

        await Assert.ThrowsAsync<ArgumentException>(() => task);
    }

    private static IEnumerable<object?[]> GetRandomWords() => AutoFaker.Generate<string>(10).Select(x => new object[] { x });
}