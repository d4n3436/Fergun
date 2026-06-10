using System.Threading.Tasks;
using Fergun.Apis.Urban;
using Xunit;

namespace Fergun.Tests.Apis;

[Trait("Category", "Integration")]
public class UrbanDictionaryIntegrationTests
{
    private readonly IUrbanDictionaryClient _urbanDictionary = new UrbanDictionaryClient();

    [InlineData("lol")]
    [InlineData("cringe")]
    [InlineData("yikes")]
    [InlineData("bruh")]
    [Theory]
    public async Task GetDefinitionsAsync_Returns_Definitions(string term)
    {
        var definitions = await _urbanDictionary.GetDefinitionsAsync(term, TestContext.Current.CancellationToken);

        Assert.NotNull(definitions);
        Assert.NotEmpty(definitions);
        Assert.All(definitions, AssertDefinitionProperties);
    }

    [Fact]
    public async Task GetRandomDefinitionsAsync_Returns_Definitions()
    {
        var definitions = await _urbanDictionary.GetRandomDefinitionsAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(definitions);
        Assert.NotEmpty(definitions);
        Assert.All(definitions, AssertDefinitionProperties);
    }

    [Fact]
    public async Task GetWordsOfTheDayAsync_Returns_Definitions()
    {
        var definitions = await _urbanDictionary.GetWordsOfTheDayAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(definitions);
        Assert.NotEmpty(definitions);
        Assert.All(definitions, x => Assert.NotNull(x.Date));
        Assert.All(definitions, x => Assert.NotEmpty(x.Date!));
        Assert.All(definitions, AssertDefinitionProperties);
    }

    [InlineData("lo")]
    [InlineData("g")]
    [InlineData("h")]
    [InlineData("s")]
    [Theory]
    public async Task GetAutocompleteResultsAsync_Returns_Results(string term)
    {
        var results = await _urbanDictionary.GetAutocompleteResultsAsync(term, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.All(results, Assert.NotNull);
        Assert.All(results, Assert.NotEmpty);
    }

    [InlineData("lo")]
    [InlineData("g")]
    [InlineData("s")]
    [Theory]
    public async Task GetAutocompleteResultsExtraAsync_Returns_Results(string term)
    {
        var results = await _urbanDictionary.GetAutocompleteResultsExtraAsync(term, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.All(results, x => Assert.NotNull(x.Term));
        Assert.All(results, x => Assert.NotEmpty(x.Term));
        Assert.All(results, x => Assert.NotNull(x.Preview));
        Assert.All(results, x => Assert.NotEmpty(x.Preview));
        Assert.All(results, x => Assert.NotNull(x.ToString()));
    }

    private static void AssertDefinitionProperties(UrbanDefinition definition)
    {
        Assert.NotNull(definition.Word);
        Assert.NotEmpty(definition.Word);
        Assert.NotNull(definition.Definition);
        Assert.NotEmpty(definition.Definition);
        Assert.NotNull(definition.Permalink);
        Assert.NotEmpty(definition.Permalink);
        Assert.NotNull(definition.Author);
        Assert.NotNull(definition.SoundUrls);
        Assert.NotNull(definition.Example);
        Assert.True(definition.ThumbsDown >= 0);
        Assert.True(definition.ThumbsUp >= 0);
        Assert.NotEqual(default, definition.WrittenOn);
        Assert.NotNull(definition.ToString());
    }
}