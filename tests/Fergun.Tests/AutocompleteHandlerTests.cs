using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bogus;
using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using Fergun.Modules.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Fergun.Tests;

public class AutocompleteHandlerTests
{
    private readonly Mock<IInteractionContext> _contextMock = new();
    private readonly Mock<ITextChannel> _channelMock = new();
    private readonly IParameterInfo _parameter = Mock.Of<IParameterInfo>();
    private readonly IServiceProvider _services = GetServiceProvider();
    private readonly Mock<IAutocompleteInteraction> _interactionMock = new();
    private readonly Mock<IAutocompleteInteractionData> _dataMock = new();

    [Theory]
    [MemberData(nameof(GetBraveTestData))]
    public async Task BraveAutocomplete_Should_Return_Valid_Suggestions(string text)
    {
        var handler = new BraveAutocompleteHandler();
        var option = Utils.CreateInstance<AutocompleteOption>(ApplicationCommandOptionType.String, text, text, true);

        _interactionMock.SetupGet(x => x.Data).Returns(_dataMock.Object);
        _dataMock.SetupGet(x => x.Current).Returns(option);

        var results = await handler.GenerateSuggestionsAsync(_contextMock.Object, _interactionMock.Object, _parameter, _services);

        Assert.True(results.IsSuccess);

        if (!string.IsNullOrEmpty(text))
        {
            Assert.NotNull(results.Suggestions);
            Assert.NotEmpty(results.Suggestions);
            Assert.All(results.Suggestions, Assert.NotNull);
            Assert.All(results.Suggestions, x => Assert.NotNull(x.Name));
            Assert.All(results.Suggestions, x => Assert.NotNull(x.Value));
        }
    }

    [Theory]
    [MemberData(nameof(GetDuckDuckGoTestData))]
    public async Task DuckDuckGoAutocomplete_Should_Return_Valid_Suggestions(string text, string locale, bool isNsfw)
    {
        var handler = new DuckDuckGoAutocompleteHandler();
        var option = Utils.CreateInstance<AutocompleteOption>(ApplicationCommandOptionType.String, text, text, true);

        _contextMock.SetupGet(x => x.Channel).Returns(_channelMock.Object);
        _channelMock.SetupGet(x => x.IsNsfw).Returns(isNsfw);
        _interactionMock.SetupGet(x => x.Data).Returns(_dataMock.Object);
        _interactionMock.SetupGet(x => x.UserLocale).Returns(locale);
        _dataMock.SetupGet(x => x.Current).Returns(option);

        var results = await handler.GenerateSuggestionsAsync(_contextMock.Object, _interactionMock.Object, _parameter, _services);

        Assert.True(results.IsSuccess);

        if (!string.IsNullOrEmpty(text))
        {
            Assert.NotNull(results.Suggestions);
            Assert.NotEmpty(results.Suggestions);
            Assert.All(results.Suggestions, Assert.NotNull);
            Assert.All(results.Suggestions, x => Assert.NotNull(x.Name));
            Assert.All(results.Suggestions, x => Assert.NotNull(x.Value));
        }
    }

    [Theory]
    [MemberData(nameof(GetGoogleTestData))]
    public async Task GoogleAutocomplete_Should_Return_Valid_Suggestions(string text, string locale)
    {
        var handler = new GoogleAutocompleteHandler();
        var option = Utils.CreateInstance<AutocompleteOption>(ApplicationCommandOptionType.String, text, text, true);

        _interactionMock.SetupGet(x => x.Data).Returns(_dataMock.Object);
        _interactionMock.SetupGet(x => x.UserLocale).Returns(locale);
        _dataMock.SetupGet(x => x.Current).Returns(option);

        var results = await handler.GenerateSuggestionsAsync(_contextMock.Object, _interactionMock.Object, _parameter, _services);

        Assert.True(results.IsSuccess);

        if (!string.IsNullOrEmpty(text))
        {
            Assert.NotNull(results.Suggestions);
            Assert.NotEmpty(results.Suggestions);
            Assert.All(results.Suggestions, Assert.NotNull);
            Assert.All(results.Suggestions, x => Assert.NotNull(x.Name));
            Assert.All(results.Suggestions, x => Assert.NotNull(x.Value));
        }
    }

    [Theory]
    [MemberData(nameof(GetGoogleTestData))]
    public async Task YouTubeAutocomplete_Should_Return_Valid_Suggestions(string text, string locale)
    {
        var handler = new YouTubeAutocompleteHandler();
        var option = Utils.CreateInstance<AutocompleteOption>(ApplicationCommandOptionType.String, text, text, true);

        _interactionMock.SetupGet(x => x.Data).Returns(_dataMock.Object);
        _interactionMock.SetupGet(x => x.UserLocale).Returns(locale);
        _dataMock.SetupGet(x => x.Current).Returns(option);

        var results = await handler.GenerateSuggestionsAsync(_contextMock.Object, _interactionMock.Object, _parameter, _services);

        Assert.True(results.IsSuccess);

        if (!string.IsNullOrEmpty(text))
        {
            Assert.NotNull(results.Suggestions);
            Assert.NotEmpty(results.Suggestions);
            Assert.All(results.Suggestions, Assert.NotNull);
            Assert.All(results.Suggestions, x => Assert.NotNull(x.Name));
            Assert.All(results.Suggestions, x => Assert.NotNull(x.Value));
        }
    }

    private static IServiceProvider GetServiceProvider()
    {
        var services = new ServiceCollection()
            .AddFergunPolicies();

        services.AddHttpClient("autocomplete", client => client.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.ChromeUserAgent))
            .SetHandlerLifetime(TimeSpan.FromMinutes(30));

        return services.BuildServiceProvider();
    }

    private static IEnumerable<object?[]> GetBraveTestData()
    {
        var faker = new Faker();
        return faker.MakeLazy(10, () => faker.Music.Genre())
            .Append(string.Empty).Append(null).Select(x => new object?[] { x });
    }

    private static IEnumerable<object?[]> GetDuckDuckGoTestData()
    {
        var faker = new Faker();
        return faker.MakeLazy(10, () => faker.Music.Genre())
            .Append(string.Empty).Append(null)
            .Zip(faker.MakeLazy(12, () => faker.Random.RandomLocale().Replace('_', '-')), faker.MakeLazy(12, () => faker.Random.Bool()))
            .Select(x => new object?[] { x.First, x.Second, x.Third });
    }

    private static IEnumerable<object?[]> GetGoogleTestData()
    {
        var faker = new Faker();
        return faker.MakeLazy(10, () => faker.Music.Genre())
            .Append(string.Empty).Append(null)
            .Zip(faker.MakeLazy(12, () => faker.Random.RandomLocale().Replace('_', '-')))
            .Select(x => new object?[] { x.First, x.Second });
    }
}