using System;
using System.Net;
using System.Threading.Tasks;
using Fergun.Apis.Urban;
using Xunit;

namespace Fergun.Tests.Apis;

public class UrbanDictionaryTests
{
    [Fact]
    public async Task GetDefinitionsAsync_Parses_Fixture()
    {
        var httpClient = Utils.CreateMockedHttpClient((HttpStatusCode.OK, UrbanTestData.DefinitionsResponse));
        var urbanDictionary = new UrbanDictionaryClient(httpClient);

        var definitions = await urbanDictionary.GetDefinitionsAsync("test", TestContext.Current.CancellationToken);

        Assert.Equal(2, definitions.Count);
        Assert.All(definitions, x => Assert.Equal("test", x.Word));

        var first = definitions[0];
        Assert.Equal(123, first.Id);
        Assert.Equal(42, first.ThumbsUp);
        Assert.Equal(7, first.ThumbsDown);
        Assert.Equal("tester", first.Author);
        Assert.Null(first.Date);
        Assert.Equal("2021-05-06", definitions[1].Date);
        Assert.Equal("Word = test, Definition = A procedure intended to establish the quality, performance, or reliability of something.", first.ToString());
    }

    [Fact]
    public async Task GetDefinitionsAsync_Throws_ArgumentException_On_Empty_Term()
    {
        var urbanDictionary = new UrbanDictionaryClient(Utils.CreateMockedHttpClient());

        await Assert.ThrowsAsync<ArgumentException>(() => urbanDictionary.GetDefinitionsAsync(string.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_Throws_ArgumentNullException_If_HttpClient_Is_Null()
        => Assert.Throws<ArgumentNullException>(() => new UrbanDictionaryClient(null!));

    [Fact]
    public async Task Disposed_UrbanDictionary_Usage_Throws_ObjectDisposedException()
    {
        var urbanDictionary = new UrbanDictionaryClient(Utils.CreateMockedHttpClient());
        urbanDictionary.Dispose();
        urbanDictionary.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => urbanDictionary.GetDefinitionsAsync("test", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => urbanDictionary.GetRandomDefinitionsAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => urbanDictionary.GetWordsOfTheDayAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => urbanDictionary.GetAutocompleteResultsAsync("test", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => urbanDictionary.GetAutocompleteResultsExtraAsync("test", TestContext.Current.CancellationToken));
    }
}