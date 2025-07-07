using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Fergun.Apis.Urban;
using Moq;
using Xunit;

namespace Fergun.Tests.Apis;

public class UrbanDictionaryTests
{
    private readonly IUrbanDictionaryClient _urbanDictionary = new UrbanDictionaryClient();

    [InlineData("lol")]
    [InlineData("cringe")]
    [InlineData("yikes")]
    [InlineData("bruh")]
    [Theory]
    public async Task GetDefinitionsAsync_Returns_Definitions(string term)
    {
        var definitions = await _urbanDictionary.GetDefinitionsAsync(term);

        Assert.NotNull(definitions);
        Assert.NotEmpty(definitions);
        Assert.All(definitions, AssertDefinitionProperties);
    }

    [Fact]
    public async Task GetRandomDefinitionsAsync_Returns_Definitions()
    {
        var definitions = await _urbanDictionary.GetRandomDefinitionsAsync();

        Assert.NotNull(definitions);
        Assert.NotEmpty(definitions);
        Assert.All(definitions, AssertDefinitionProperties);
    }

    [InlineData(871139)]
    [InlineData(15369452)]
    [Theory]
    public async Task GetDefinitionAsync_Returns_Definition(int id)
    {
        var definition = await _urbanDictionary.GetDefinitionAsync(id);

        Assert.NotNull(definition);
        Assert.Equal(id, definition.Id);
    }

    [InlineData(int.MaxValue)]
    [InlineData(0)]
    [Theory]
    public async Task GetDefinitionAsync_Returns_InternalServerError_If_Id_Is_Invalid(int id)
    {
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => _urbanDictionary.GetDefinitionAsync(id));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [Fact]
    public async Task GetWordsOfTheDayAsync_Returns_Definitions()
    {
        var definitions = await _urbanDictionary.GetWordsOfTheDayAsync();

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
        var results = await _urbanDictionary.GetAutocompleteResultsAsync(term);

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
        var results = await _urbanDictionary.GetAutocompleteResultsExtraAsync(term);

        Assert.NotNull(results);
        Assert.All(results, x => Assert.NotNull(x.Term));
        Assert.All(results, x => Assert.NotEmpty(x.Term));
        Assert.All(results, x => Assert.NotNull(x.Preview));
        Assert.All(results, x => Assert.NotEmpty(x.Preview));
        Assert.All(results, x => Assert.NotNull(x.ToString()));
    }

    [Fact]
    public async Task Disposed_UrbanDictionary_Usage_Throws_ObjectDisposedException()
    {
        (_urbanDictionary as IDisposable)?.Dispose();
        (_urbanDictionary as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _urbanDictionary.GetDefinitionsAsync("test"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _urbanDictionary.GetRandomDefinitionsAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _urbanDictionary.GetDefinitionAsync(It.IsAny<int>()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _urbanDictionary.GetWordsOfTheDayAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _urbanDictionary.GetAutocompleteResultsAsync("test"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _urbanDictionary.GetAutocompleteResultsExtraAsync("test"));
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