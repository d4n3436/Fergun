using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bogus;
using Fergun.Extensions;
using Fergun.Modules.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fergun.Tests;

public class AutocompleteHandlerTests
{
    [Theory]
    [MemberData(nameof(GetTestData))]
    public async Task BraveAutocomplete_Should_Return_Valid_Suggestions(string text)
    {
        var services = GetServiceProvider();

        var results = await BraveAutocompleteHandler.GetBraveSuggestionsAsync(text, services);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.All(results, Assert.NotNull);
        Assert.All(results, Assert.NotEmpty);
    }

    [Theory]
    [MemberData(nameof(GetTestData))]
    public async Task DuckDuckGoAutocomplete_Should_Return_Valid_Suggestions(string text)
    {
        var services = GetServiceProvider();

        var results = await DuckDuckGoAutocompleteHandler.GetDuckDuckGoSuggestionsAsync(text, services);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.All(results, Assert.NotNull);
        Assert.All(results, Assert.NotEmpty);
    }

    [Theory]
    [MemberData(nameof(GetTestData))]
    public async Task GoogleAutocomplete_Should_Return_Valid_Suggestions(string text)
    {
        var services = GetServiceProvider();

        var results = await GoogleAutocompleteHandler.GetGoogleSuggestionsAsync(text, services);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.All(results, Assert.NotNull);
        Assert.All(results, Assert.NotEmpty);
    }

    [Theory]
    [MemberData(nameof(GetTestData))]
    public async Task YouTubeAutocomplete_Should_Return_Valid_Suggestions(string text)
    {
        var services = GetServiceProvider();

        var results = await YouTubeAutocompleteHandler.GetYouTubeSuggestionsAsync(text, services);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.All(results, Assert.NotNull);
        Assert.All(results, Assert.NotEmpty);
    }

    private static IServiceProvider GetServiceProvider()
    {
        var services = new ServiceCollection()
            .AddFergunPolicies();

        services.AddHttpClient("autocomplete", client => client.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.ChromeUserAgent))
            .SetHandlerLifetime(TimeSpan.FromMinutes(30));

        return services.BuildServiceProvider();
    }

    private static IEnumerable<object[]> GetTestData()
    {
        var faker = new Faker();
        return faker.MakeLazy(10, () => faker.Music.Genre()).Select(x => new object[] { x });
    }
}