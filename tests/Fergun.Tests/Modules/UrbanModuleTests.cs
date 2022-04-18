using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoBogus;
using AutoBogus.Moq;
using Bogus;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Apis.Urban;
using Fergun.Interactive;
using Fergun.Modules;
using Moq;
using Moq.Protected;
using Xunit;

namespace Fergun.Tests.Modules;

public class UrbanModuleTests
{
    private readonly Mock<IInteractionContext> _contextMock = new();
    private readonly Mock<IDiscordInteraction> _interactionMock = new();
    private readonly Mock<IUrbanDictionary> _urbanDictionaryMock = CreateMockedUrbanDictionary();
    private readonly Mock<UrbanModule> _moduleMock;
    private readonly DiscordSocketClient _client = new();
    private readonly InteractiveConfig _interactiveConfig = new() { ReturnAfterSendingPaginator = true };
    private readonly IFergunLocalizer<UrbanModule> _localizer = Utils.CreateMockedLocalizer<UrbanModule>();

    public UrbanModuleTests()
    {
        var interactive = new InteractiveService(_client, _interactiveConfig);

        _moduleMock = new Mock<UrbanModule>(() => new UrbanModule(_localizer, _urbanDictionaryMock.Object, interactive)) { CallBase = true };
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

    [MemberData(nameof(GetRandomWords))]
    [Theory]
    public async Task Search_Calls_GetDefinitionsAsync(string term)
    {
        var module = _moduleMock.Object;

        await module.Search(term);

        _moduleMock.Protected().Verify<Task>("DeferAsync", Times.Once(), ItExpr.IsAny<bool>(), ItExpr.IsAny<RequestOptions>());
        _urbanDictionaryMock.Verify(u => u.GetDefinitionsAsync(It.Is<string>(x => x == term)), Times.Once);
        int count = (await _urbanDictionaryMock.Object.GetDefinitionsAsync(It.IsAny<string>())).Count;

        if (count == 0)
        {
            _interactionMock.Verify(i => i.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<AllowedMentions>(),
                It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
        }
    }

    [Fact]
    public async Task Random_Calls_GetRandomDefinitionsAsync()
    {
        var module = _moduleMock.Object;

        await module.Random();

        _moduleMock.Protected().Verify<Task>("DeferAsync", Times.Once(), ItExpr.IsAny<bool>(), ItExpr.IsAny<RequestOptions>());
        _urbanDictionaryMock.Verify(u => u.GetRandomDefinitionsAsync(), Times.Once);
    }

    [Fact]
    public async Task WordsOfTheDay_Calls_GetWordsOfTheDayAsync()
    {
        var module = _moduleMock.Object;

        await module.WordsOfTheDay();

        _moduleMock.Protected().Verify<Task>("DeferAsync", Times.Once(), ItExpr.IsAny<bool>(), ItExpr.IsAny<RequestOptions>());
        _urbanDictionaryMock.Verify(u => u.GetWordsOfTheDayAsync(), Times.Once);
    }

    [Fact]
    public async Task Invalid_SearchType_Throws_ArgumentException()
    {
        var module = _moduleMock.Object;

        var task = module.SearchAndSendAsync((UrbanModule.UrbanSearchType)3);

        await Assert.ThrowsAsync<ArgumentException>(() => task);
    }

    private static IEnumerable<object?[]> GetRandomWords() => AutoFaker.Generate<string>(20).Select(x => new object[] { x });

    private static Mock<IUrbanDictionary> CreateMockedUrbanDictionary()
    {
        var faker = new Faker();
        var mock = new Mock<IUrbanDictionary>();

        var definitionFaker = new AutoFaker<UrbanDefinition>()
            .RuleFor(x => x.Definition, f => f.Lorem.Sentence())
            .RuleFor(x => x.Date, f => f.Date.Weekday().OrNull(f))
            .RuleFor(x => x.Permalink, f => f.Internet.Url())
            .RuleFor(x => x.ThumbsUp, f => f.Random.Int())
            .RuleFor(x => x.SoundUrls, Array.Empty<string>())
            .RuleFor(x => x.Author, f => f.Internet.UserName())
            .RuleFor(x => x.Word, f => f.Lorem.Word())
            .RuleFor(x => x.Id, f => f.Random.Int())
            .RuleFor(x => x.WrittenOn, f => f.Date.PastOffset())
            .RuleFor(x => x.Example, f => f.Lorem.Sentence());

        mock.Setup(u => u.GetDefinitionsAsync(It.IsAny<string>())).ReturnsAsync(definitionFaker.Generate(10).OrDefault(faker, defaultValue: new()));
        mock.Setup(u => u.GetRandomDefinitionsAsync()).ReturnsAsync(definitionFaker.Generate(10));
        mock.Setup(u => u.GetDefinitionAsync(It.IsAny<int>())).ReturnsAsync(definitionFaker.Generate());
        mock.Setup(u => u.GetWordsOfTheDayAsync()).ReturnsAsync(definitionFaker.Generate(10));
        mock.Setup(u => u.GetAutocompleteResultsAsync(It.IsAny<string>())).ReturnsAsync(AutoFaker.Generate<string>(20));
        mock.Setup(u => u.GetAutocompleteResultsExtraAsync(It.IsAny<string>())).ReturnsAsync(AutoFaker.Generate<UrbanAutocompleteResult>(20));

        return mock;
    }
}