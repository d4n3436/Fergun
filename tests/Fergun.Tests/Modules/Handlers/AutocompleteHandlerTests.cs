using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Modules.Handlers;
using Moq;
using Xunit;

namespace Fergun.Tests.Modules.Handlers;

public class AutocompleteHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public Task DuckDuckGoAutocomplete_Returns_No_Suggestions_For_Empty_Input(string? text)
        => AssertEmptyInputShortCircuits(new DuckDuckGoAutocompleteHandler(), text);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public Task GoogleAutocomplete_Returns_No_Suggestions_For_Empty_Input(string? text)
        => AssertEmptyInputShortCircuits(new GoogleAutocompleteHandler(), text);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public Task YouTubeAutocomplete_Returns_No_Suggestions_For_Empty_Input(string? text)
        => AssertEmptyInputShortCircuits(new YouTubeAutocompleteHandler(), text);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public Task UrbanAutocomplete_Returns_No_Suggestions_For_Empty_Input(string? text)
        => AssertEmptyInputShortCircuits(new UrbanAutocompleteHandler(), text);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public Task WikipediaAutocomplete_Returns_No_Suggestions_For_Empty_Input(string? text)
        => AssertEmptyInputShortCircuits(new WikipediaAutocompleteHandler(), text);

    private static async Task AssertEmptyInputShortCircuits(AutocompleteHandler handler, string? text)
    {
        var option = Utils.CreateNonPublicInstance<AutocompleteOption>(ApplicationCommandOptionType.String, text!, text!, true);

        var dataMock = new Mock<IAutocompleteInteractionData>();
        dataMock.SetupGet(x => x.Current).Returns(option);

        var interactionMock = new Mock<IAutocompleteInteraction>();
        interactionMock.SetupGet(x => x.Data).Returns(dataMock.Object);

        // The short-circuit must return before resolving any service
        var services = new Mock<IServiceProvider>(MockBehavior.Strict).Object;

        var result = await handler.GenerateSuggestionsAsync(Mock.Of<IInteractionContext>(), interactionMock.Object, Mock.Of<IParameterInfo>(), services);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Suggestions ?? Enumerable.Empty<AutocompleteResult>());
    }
}