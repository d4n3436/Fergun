using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Fergun.Apis.Dictionary;
using JetBrains.Annotations;
using Xunit;

namespace Fergun.Tests.Apis;

[Trait("Category", "Integration")]
public class DictionaryClientIntegrationTests
{
    private readonly IDictionaryClient _dictionary = new DictionaryClient(new HttpClient());

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
        var groups = new[] { result.Data.Content.Luna, result.Data.Content.Collins }
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

                        if (definition.Subdefinitions.Count > 0)
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
}