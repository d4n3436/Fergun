using Fergun.Apis.WolframAlpha;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Moq;
using Xunit;
using System.Threading;
using System.Text.Json;
using AutoBogus;

namespace Fergun.Tests.Apis;

public class WolframAlphaTests
{
    private readonly IWolframAlphaClient _wolframAlphaClient = new WolframAlphaClient();

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
    public async Task GetAutocompleteResultsAsync_Throws_OperationCanceledException_With_Canceled_CancellationToken()
    {
        var cts = new CancellationTokenSource(0);
        await Assert.ThrowsAsync<OperationCanceledException>(() => _wolframAlphaClient.GetAutocompleteResultsAsync("test", "en", cts.Token));
    }

    [Fact]
    public async Task SendQueryAsync_Returns_Successful_Result()
    {
        var result = await _wolframAlphaClient.SendQueryAsync("Chicag", "en");

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
            Assert.True(pod.Position > 0);

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
        var result = await _wolframAlphaClient.SendQueryAsync("kitten danger", "en", false);

        Assert.Equal(WolframAlphaResultType.DidYouMean, result.Type);
        Assert.NotEmpty(result.DidYouMeans);

        foreach (var suggestion in result.DidYouMeans)
        {
            Assert.InRange(suggestion.Score, 0, 1);
            Assert.Contains(suggestion.Level, new[] { "low", "medium", "high" });
            Assert.NotEmpty(suggestion.Value);
        }
    }

    [Fact]
    public async Task SendQueryAsync_Returns_FutureTopic_Result()
    {
        var result = await _wolframAlphaClient.SendQueryAsync("Microsoft Windows", "es");

        Assert.Equal(WolframAlphaResultType.FutureTopic, result.Type);
        Assert.NotNull(result.FutureTopic);
        Assert.NotEmpty(result.FutureTopic.Topic);
        Assert.NotEmpty(result.FutureTopic.Message);
    }

    [Fact]
    public async Task SendQueryAsync_Returns_No_Result()
    {
        var result = await _wolframAlphaClient.SendQueryAsync("oadf lds", "ja");

        Assert.Equal(WolframAlphaResultType.NoResult, result.Type);
    }

    [Fact]
    public async Task SendQueryAsync_Returns_Error()
    {
        var result = await _wolframAlphaClient.SendQueryAsync(string.Empty, "en");

        Assert.Equal(WolframAlphaResultType.Error, result.Type);
        Assert.NotNull(result.ErrorInfo);
        Assert.Equal(1000, result.ErrorInfo.StatusCode);
        Assert.NotEmpty(result.ErrorInfo.Message);
    }

    [Fact]
    public async Task SendQueryAsync_Throws_OperationCanceledException_With_Canceled_CancellationToken()
    {
        var cts = new CancellationTokenSource(0);
        await Assert.ThrowsAsync<OperationCanceledException>(() => _wolframAlphaClient.SendQueryAsync("test", "en", It.IsAny<bool>(), cts.Token));
    }

    [Fact]
    public async Task Disposed_WolframAlphaClient_Usage_Throws_ObjectDisposedException()
    {
        (_wolframAlphaClient as IDisposable)?.Dispose();
        (_wolframAlphaClient as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _wolframAlphaClient.GetAutocompleteResultsAsync(AutoFaker.Generate<string>(), AutoFaker.Generate<string>(), It.IsAny<CancellationToken>()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _wolframAlphaClient.SendQueryAsync(AutoFaker.Generate<string>(), AutoFaker.Generate<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));
    }

    [Theory]
    [MemberData(nameof(GetWolframAlphaErrorInfoConverterData))]
    public void WolframAlphaErrorInfoConverter_Returns_Expected_Results(string input, WolframAlphaErrorInfo? expectedResult)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new WolframAlphaErrorInfoConverter());

        var result = JsonSerializer.Deserialize<WolframAlphaErrorInfo?>(input, options);

        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [MemberData(nameof(ArrayOrObjectConverterData))]
    public void ArrayOrObjectConverter_Returns_Expected_Results(string input, IReadOnlyList<WolframAlphaWarning> expectedResult)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ArrayOrObjectConverter<WolframAlphaWarning>());

        var result = JsonSerializer.Deserialize<IReadOnlyList<WolframAlphaWarning>>(input, options);

        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void WolframAlphaErrorInfoConverter_Throws_NotSupportedException()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new WolframAlphaErrorInfoConverter());

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new WolframAlphaErrorInfo(0, "test"), options));
    }

    [Fact]
    public void ArrayOrObjectConverter_Throws_Exceptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ArrayOrObjectConverter<string>());

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IReadOnlyList<string>>("true", options));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize<IReadOnlyList<string>>(new[] { "test" }, options));
    }

    public static TheoryData<string, WolframAlphaErrorInfo?> GetWolframAlphaErrorInfoConverterData()
    {
        return new TheoryData<string, WolframAlphaErrorInfo?>
        {
            { "true", null },
            { "false", null },
            { """{"code":"1000","msg":"error"}""", new WolframAlphaErrorInfo(1000, "error") }
        };
    }

    public static TheoryData<string, IReadOnlyList<WolframAlphaWarning>> ArrayOrObjectConverterData()
    {
        const string json = """{"text":"Error message"}""";
        var suggestions = new[] { new WolframAlphaWarning("Error message") };

        return new TheoryData<string, IReadOnlyList<WolframAlphaWarning>>
        {
            { json, suggestions },
            { $"[{json}]", suggestions }
        };
    }
}