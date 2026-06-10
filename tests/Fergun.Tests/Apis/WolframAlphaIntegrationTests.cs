using System;
using System.Threading;
using System.Threading.Tasks;
using Fergun.Apis.WolframAlpha;
using Xunit;

namespace Fergun.Tests.Apis;

[Trait("Category", "Integration")]
public class WolframAlphaIntegrationTests
{
    private readonly IWolframAlphaClient _wolframAlphaClient = new WolframAlphaClient();
    private static readonly string[] SuggestionLevels = ["low", "medium", "high"];

    [Theory]
    [InlineData("2 +", "en")]
    [InlineData("1/6", "es")]
    [InlineData("2^2", "ja")]
    public async Task GetAutocompleteResultsAsync_Returns_Valid_Results(string input, string language)
    {
        var results = await _wolframAlphaClient.GetAutocompleteResultsAsync(input, language, CancellationToken.None);

        Assert.NotEmpty(results);
        Assert.All(results, Assert.NotEmpty);
    }

    [Fact]
    public async Task SendQueryAsync_Returns_Successful_Result()
    {
        var result = await _wolframAlphaClient.SendQueryAsync("Chicag", "en", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(WolframAlphaResultType.Success, result.Type);
        Assert.NotEmpty(result.Warnings);
        Assert.All(result.Warnings, warning => Assert.NotEmpty(warning.Text));

        Assert.NotEmpty(result.Pods);

        foreach (var pod in result.Pods)
        {
            Assert.NotEmpty(pod.SubPods);
            Assert.All(pod.SubPods, Assert.NotNull);
            Assert.NotEmpty(pod.Title);
            Assert.NotEmpty(pod.Id);

            foreach (var subPod in pod.SubPods)
            {
                Assert.NotNull(subPod.PlainText);
                Assert.NotNull(subPod.Title);
                Assert.NotNull(subPod.Image);

                Assert.True(Uri.IsWellFormedUriString(subPod.Image.SourceUrl, UriKind.Absolute));
                Assert.True(subPod.Image.Height > 0);
                Assert.True(subPod.Image.Width > 0);
                Assert.NotEmpty(subPod.Image.ContentType);
            }
        }
    }

    [Fact]
    public async Task SendQueryAsync_Returns_DidYouMean_Result()
    {
        var result = await _wolframAlphaClient.SendQueryAsync("kitten danger", "en", false, TestContext.Current.CancellationToken);

        Assert.Equal(WolframAlphaResultType.DidYouMean, result.Type);
        Assert.NotEmpty(result.DidYouMeans);

        foreach (var suggestion in result.DidYouMeans)
        {
            Assert.InRange(suggestion.Score, 0, 1);
            Assert.Contains(suggestion.Level, SuggestionLevels);
            Assert.NotEmpty(suggestion.Value);
        }
    }

    [Fact]
    public async Task SendQueryAsync_Returns_FutureTopic_Result()
    {
        var result = await _wolframAlphaClient.SendQueryAsync("Microsoft Windows", "es", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(WolframAlphaResultType.FutureTopic, result.Type);
        Assert.NotNull(result.FutureTopic);
        Assert.NotEmpty(result.FutureTopic.Topic);
        Assert.NotEmpty(result.FutureTopic.Message);
    }

    [Fact]
    public async Task SendQueryAsync_Returns_No_Result()
    {
        var result = await _wolframAlphaClient.SendQueryAsync("oadf lds", "ja", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(WolframAlphaResultType.NoResult, result.Type);
    }

    [Fact]
    public async Task SendQueryAsync_Returns_Error()
    {
        var result = await _wolframAlphaClient.SendQueryAsync(string.Empty, "en", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(WolframAlphaResultType.Error, result.Type);
        Assert.NotNull(result.ErrorInfo);
        Assert.Equal(1000, result.ErrorInfo.StatusCode);
        Assert.NotEmpty(result.ErrorInfo.Message);
    }
}