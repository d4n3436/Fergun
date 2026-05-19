using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Fergun.Apis.Dictionary;
using JetBrains.Annotations;
using Xunit;

namespace Fergun.Tests.Apis;

public class DictionaryClientTests
{
    private readonly IDictionaryClient _dictionary = new DictionaryClient(new HttpClient());

    private sealed record PronunciationWrapper(
        [property: JsonConverter(typeof(PronunciationConverter))]
        [property: JsonPropertyName("pronunciation")] EntryPronunciation Pronunciation);

    [Theory]
    [InlineData("run")]
    [InlineData("a")]
    [InlineData("satire")]
    public async Task GetDefinitionsAsync_ReturnsValidResults(string word)
    {
        var result = await _dictionary.GetDefinitionsAsync(word, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.Content);

        // Only groups from "luna" or "collins" are supported
        var groups = new[] { result.Data.Content?.Luna, result.Data.Content?.Collins }
        .Where(x => x is not null)
        .ToArray();

        Assert.NotEmpty(groups);

        Assert.All(groups, group =>
        {
            Assert.NotNull(group);
            Assert.NotNull(group.Entries);
            Assert.NotEmpty(group.Entries);

            Assert.All(group.Entries, entry =>
            {
                Assert.NotNull(entry);
                Assert.NotEmpty(entry.Entry);
                Assert.NotNull(entry.PartOfSpeechBlocks);
                Assert.NotEmpty(entry.PartOfSpeechBlocks);

                if (entry.Homograph is not null)
                {
                    Assert.Matches("^[0-9]+$", entry.Homograph.Value.ToString(CultureInfo.InvariantCulture));
                }

                Assert.NotEmpty(DictionaryFormatter.FormatEntry(entry));
                Assert.NotNull(DictionaryFormatter.FormatExtraInformation(entry));

                Assert.All(entry.PartOfSpeechBlocks, block =>
                {
                    Assert.NotNull(block);
                    Assert.NotNull(block.Definitions);
                    Assert.NotEmpty(block.Definitions);

                    string formattedBlock = DictionaryFormatter.FormatPartOfSpeechBlock(block, entry, 1000);
                    Assert.NotEmpty(formattedBlock);
                    Assert.InRange(formattedBlock.Length, 1, 1000);

                    Assert.All(block.Definitions, [AssertionMethod] (definition) =>
                    {
                        Assert.NotNull(definition);

                        if (definition.Subdefinitions?.Count > 0)
                        {
                            Assert.All(definition.Subdefinitions, [AssertionMethod] (subDefinition) =>
                            {
                                Assert.NotNull(subDefinition);
                                Assert.NotNull(subDefinition.Definition);
                                Assert.NotEmpty(subDefinition.Definition);
                            });
                        }
                        else
                        {
                            Assert.NotNull(definition.Definition);
                            Assert.NotEmpty(definition.Definition);
                        }
                    });
                });
            });
        });
    }

    [Theory]
    [InlineData("run")]
    public async Task GetSearchResultsAsync_ReturnsValidResults(string word)
    {
        var results = await _dictionary.GetSearchResultsAsync(word, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.NotEmpty(results);

        Assert.All(results, [AssertionMethod] (result) =>
        {
            Assert.NotNull(result);
            Assert.NotNull(result.DisplayText);
            Assert.NotEmpty(result.DisplayText);
            Assert.NotNull(result.Reference);
            Assert.NotNull(result.Reference.Type);
            Assert.NotEmpty(result.Reference.Type);
            Assert.NotNull(result.Reference.Identifier);
            Assert.NotEmpty(result.Reference.Identifier);
        });
    }

    [Fact]
    public void Constructor_Throws_ArgumentNullException_IfHttpClientIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DictionaryClient(null!));
    }

    [Fact]
    public async Task Empty_Parameters_Throws_ArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _dictionary.GetDefinitionsAsync(string.Empty, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => _dictionary.GetSearchResultsAsync(string.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Disposed_BingVisualSearch_Usage_Throws_ObjectDisposedException()
    {
        (_dictionary as IDisposable)?.Dispose();
        (_dictionary as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _dictionary.GetDefinitionsAsync("test", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _dictionary.GetSearchResultsAsync("test", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanceledToken_Throws_InvalidOperationException()
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
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PronunciationWrapper>($$"""{"pronunciation":{{json}}}"""));
    }
}