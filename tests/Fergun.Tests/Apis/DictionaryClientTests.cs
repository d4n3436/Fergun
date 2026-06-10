using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Fergun.Apis.Dictionary;
using Xunit;

namespace Fergun.Tests.Apis;

public class DictionaryClientTests
{
    private readonly IDictionaryClient _dictionary = new DictionaryClient(Utils.CreateMockedHttpClient());

    private sealed record PronunciationWrapper(
        [property: JsonConverter(typeof(PronunciationConverter))]
        [property: JsonPropertyName("pronunciation")] EntryPronunciation Pronunciation);

    [Fact]
    public void Constructor_Throws_ArgumentNullException_IfHttpClientIsNull()
        => Assert.Throws<ArgumentNullException>(() => new DictionaryClient(null!));

    [Fact]
    public async Task Empty_Parameters_Throws_ArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _dictionary.GetDefinitionsAsync(string.Empty, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => _dictionary.GetSearchResultsAsync(string.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Disposed_Dictionary_Usage_Throws_ObjectDisposedException()
    {
        (_dictionary as IDisposable)?.Dispose();
        (_dictionary as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _dictionary.GetDefinitionsAsync("test", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _dictionary.GetSearchResultsAsync("test", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanceledToken_Throws_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource(0);

        await Assert.ThrowsAsync<OperationCanceledException>(() => _dictionary.GetDefinitionsAsync("test", cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => _dictionary.GetSearchResultsAsync("test", cts.Token));
    }

    [Theory]
    [InlineData("\"bee\"", "bee")]
    [InlineData("""{"ipa":"rʌn","spell": "ruhn"}""", "rʌn")]
    public void PronunciationConverter_Returns_ExpectedValues(string json, string expectedIpa)
    {
        var actual = JsonSerializer.Deserialize<PronunciationWrapper>($$"""{"pronunciation":{{json}}}""")!.Pronunciation;

        Assert.Equal(expectedIpa, actual.Ipa);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    public void PronunciationConverter_Throws_JsonException_When_InvalidValueIsPassed(string json)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PronunciationWrapper>($$"""{"pronunciation":{{json}}}"""));
}