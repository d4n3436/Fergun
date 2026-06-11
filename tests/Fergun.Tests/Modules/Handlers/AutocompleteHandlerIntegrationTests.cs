using System;
using System.Linq;
using System.Threading.Tasks;
using Bogus;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Urban;
using Fergun.Apis.Wikipedia;
using Fergun.Common;
using Fergun.Extensions;
using Fergun.Modules.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Fergun.Tests.Modules.Handlers;

[Trait("Category", "Integration")]
public class AutocompleteHandlerIntegrationTests
{
    private readonly Mock<IInteractionContext> _contextMock = new();
    private readonly Mock<ITextChannel> _channelMock = new();
    private readonly IParameterInfo _parameter = Mock.Of<IParameterInfo>();
    private readonly IServiceProvider _services = GetServiceProvider();
    private readonly Mock<IAutocompleteInteraction> _interactionMock = new();
    private readonly Mock<IAutocompleteInteractionData> _dataMock = new();

    [Theory]
    [MemberData(nameof(GetQueriesWithLocaleAndNsfw))]
    public async Task DuckDuckGoAutocomplete_Returns_Valid_Suggestions(string query, string locale, bool isNsfw)
    {
        _contextMock.SetupGet(x => x.Channel).Returns(_channelMock.Object);
        _channelMock.SetupGet(x => x.IsNsfw).Returns(isNsfw);
        SetupInteraction(query, locale);

        var results = await new DuckDuckGoAutocompleteHandler().GenerateSuggestionsAsync(_contextMock.Object, _interactionMock.Object, _parameter, _services);

        AssertValidSuggestions(results);
    }

    [Theory]
    [MemberData(nameof(GetQueriesWithLocale))]
    public async Task GoogleAutocomplete_Returns_Valid_Suggestions(string query, string locale)
    {
        SetupInteraction(query, locale);

        var results = await new GoogleAutocompleteHandler().GenerateSuggestionsAsync(_contextMock.Object, _interactionMock.Object, _parameter, _services);

        AssertValidSuggestions(results);
    }

    [Theory]
    [MemberData(nameof(GetQueriesWithLocale))]
    public async Task YouTubeAutocomplete_Returns_Valid_Suggestions(string query, string locale)
    {
        SetupInteraction(query, locale);

        var results = await new YouTubeAutocompleteHandler().GenerateSuggestionsAsync(_contextMock.Object, _interactionMock.Object, _parameter, _services);

        AssertValidSuggestions(results);
    }

    [Theory]
    [MemberData(nameof(GetQueries))]
    public async Task UrbanAutocomplete_Returns_Valid_Suggestions(string query)
    {
        SetupInteraction(query, locale: null);

        var results = await new UrbanAutocompleteHandler().GenerateSuggestionsAsync(_contextMock.Object, _interactionMock.Object, _parameter, _services);

        AssertValidSuggestions(results);
    }

    [Theory]
    [InlineData("a", "en")]
    [InlineData("b", "es")]
    [InlineData("c", "fr")]
    public async Task WikipediaAutocomplete_Returns_Valid_Suggestions(string query, string locale)
    {
        SetupInteraction(query, locale);

        var results = await new WikipediaAutocompleteHandler().GenerateSuggestionsAsync(_contextMock.Object, _interactionMock.Object, _parameter, _services);

        AssertValidSuggestions(results);
    }

    private void SetupInteraction(string query, string? locale)
    {
        var option = Utils.CreateNonPublicInstance<AutocompleteOption>(ApplicationCommandOptionType.String, query, query, true);
        _dataMock.SetupGet(x => x.Current).Returns(option);
        _interactionMock.SetupGet(x => x.Data).Returns(_dataMock.Object);

        if (locale is not null)
            _interactionMock.SetupGet(x => x.UserLocale).Returns(locale);
    }

    private static void AssertValidSuggestions(AutocompletionResult result)
    {
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Suggestions);
        Assert.NotEmpty(result.Suggestions);
        Assert.All(result.Suggestions, x =>
        {
            Assert.NotNull(x);
            Assert.NotNull(x.Name);
            Assert.NotNull(x.Value);
        });
    }

    private static ServiceProvider GetServiceProvider()
    {
        var services = new ServiceCollection()
            .AddFergunPolicies();

        services.AddHttpClient("autocomplete", client => client.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.ChromeUserAgent))
            .SetHandlerLifetime(TimeSpan.FromMinutes(30));

        services.AddHttpClient<IUrbanDictionaryClient, UrbanDictionaryClient>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        services.AddHttpClient<IWikipediaClient, WikipediaClient>(client => client.DefaultRequestHeaders.UserAgent.ParseAdd(DiscordConfig.UserAgent))
            .SetHandlerLifetime(TimeSpan.FromMinutes(30))
            .AddRetryPolicy();

        return services.BuildServiceProvider();
    }

    public static TheoryData<string, string, bool> GetQueriesWithLocaleAndNsfw()
    {
        var faker = new Faker { Random = new Randomizer(42) };
        return faker.MakeLazy(5, () => faker.Music.Genre())
            .Zip(faker.MakeLazy(5, () => faker.Random.RandomLocale().Replace('_', '-')), faker.MakeLazy(5, () => faker.Random.Bool()))
            .ToTheoryData();
    }

    public static TheoryData<string, string> GetQueriesWithLocale()
    {
        var faker = new Faker { Random = new Randomizer(42) };
        return faker.MakeLazy(5, () => faker.Music.Genre())
            .Zip(faker.MakeLazy(5, () => faker.Random.RandomLocale().Replace('_', '-')))
            .ToTheoryData();
    }

    public static TheoryData<string> GetQueries()
    {
        var faker = new Faker { Random = new Randomizer(42) };
        return faker.MakeLazy(5, () => faker.Hacker.Noun()).ToTheoryData();
    }
}