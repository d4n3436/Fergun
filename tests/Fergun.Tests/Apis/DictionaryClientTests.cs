using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fergun.Apis.Dictionary;
using JetBrains.Annotations;
using Xunit;

namespace Fergun.Tests.Apis;

public class DictionaryClientTests
{
    private static readonly JsonSerializerOptions _arrayOrStringSerializerOptions = new()
    {
        Converters = { new ArrayOrStringConverter() }
    };

    private static readonly JsonSerializerOptions _pronunciationSerializerOptions = new()
    {
        Converters = { new PronunciationConverter() }
    };

    private readonly IDictionaryClient _dictionary = new DictionaryClient(new HttpClient());

    [Theory]
    [InlineData("run")]
    [InlineData("a")]
    [InlineData("satire")]
    public async Task GetDefinitionsAsync_ReturnsValidResults(string word)
    {
        var result = await _dictionary.GetDefinitionsAsync(word);

        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.Content);
        Assert.NotEmpty(result.Data.Content);

        // Only groups from "luna" or "collins" are supported
        var groups = result.Data.Content
            .Where(content => content.Source is "luna" or "collins")
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
                    Assert.Matches("^[0-9]+$", entry.Homograph);
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

                    Assert.All(block.Definitions,[AssertionMethod] (definition) =>
                    {
                        Assert.NotNull(definition);
                        Assert.False(definition.Ordinal is null && definition.Order is null);

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
        var results = await _dictionary.GetSearchResultsAsync(word);

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
        await Assert.ThrowsAsync<ArgumentException>(() => _dictionary.GetDefinitionsAsync(string.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => _dictionary.GetSearchResultsAsync(string.Empty));
    }

    [Fact]
    public async Task Disposed_BingVisualSearch_Usage_Throws_ObjectDisposedException()
    {
        (_dictionary as IDisposable)?.Dispose();
        (_dictionary as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _dictionary.GetDefinitionsAsync("test"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _dictionary.GetSearchResultsAsync("test"));
    }

    [Fact]
    public async Task CanceledToken_Throws_InvalidOperationException()
    {
        var cts = new CancellationTokenSource(0);

        await Assert.ThrowsAsync<OperationCanceledException>(() => _dictionary.GetDefinitionsAsync("test", cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => _dictionary.GetSearchResultsAsync("test", cts.Token));
    }

    [Theory]
    [InlineData("\"bas\"", new[] { "bas" })]
    [InlineData("[\"bas\"]", new[] { "bas" })]
    [InlineData("\"\"", new string[] { })]
    public void ArrayOrStringConverter_Returns_ExpectedValues(string json, string[] expected)
    {
        var actual = JsonSerializer.Deserialize<IReadOnlyList<string>>(json, _arrayOrStringSerializerOptions);

        Assert.NotNull(actual);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("{}")]
    public void ArrayOrStringConverter_Throws_JsonException_When_InvalidValueIsPassed(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IReadOnlyList<string>>(json, _arrayOrStringSerializerOptions));
    }

    [Theory]
    [InlineData("\"bee\"", "bee")]
    [InlineData("""{"ipa":"rʌn","spell": "ruhn"}""", "rʌn")]
    public void PronunciationConverter_Returns_ExpectedValues(string json, string expectedIpa)
    {
        var actual = JsonSerializer.Deserialize<EntryPronunciation>(json, _pronunciationSerializerOptions)!;

        Assert.Equal(expectedIpa, actual.Ipa);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    public void PronunciationConverter_Throws_JsonException_When_InvalidValueIsPassed(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EntryPronunciation>(json, _pronunciationSerializerOptions));
    }
}