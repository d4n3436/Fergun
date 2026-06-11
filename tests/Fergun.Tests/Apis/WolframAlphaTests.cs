using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fergun.Apis.WolframAlpha;
using Xunit;

namespace Fergun.Tests.Apis;

public class WolframAlphaTests
{
    private readonly IWolframAlphaClient _wolframAlphaClient = new WolframAlphaClient(Utils.CreateMockedHttpClient());
    private readonly JsonSerializerOptions _wolframAlphaOptions = new();

    public WolframAlphaTests()
    {
        _wolframAlphaOptions.Converters.Add(new WolframAlphaErrorInfoConverter());
        _wolframAlphaOptions.Converters.Add(new ArrayOrObjectConverter<WolframAlphaWarning>());
        _wolframAlphaOptions.Converters.Add(new ArrayOrObjectConverter<string>());
    }

    [Fact]
    public async Task Operations_Throw_OperationCanceledException_With_Canceled_Token()
    {
        using var cts = new CancellationTokenSource(0);

        await Assert.ThrowsAsync<OperationCanceledException>(() => _wolframAlphaClient.GetAutocompleteResultsAsync("test", "en", cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => _wolframAlphaClient.SendQueryAsync("test", "en", false, cts.Token));
    }

    [Fact]
    public async Task Disposed_WolframAlphaClient_Usage_Throws_ObjectDisposedException()
    {
        ((IDisposable)_wolframAlphaClient).Dispose();
        ((IDisposable)_wolframAlphaClient).Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _wolframAlphaClient.GetAutocompleteResultsAsync("test", "en", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _wolframAlphaClient.SendQueryAsync("test", "en", false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAutocompleteResultsAsync_Parses_Results()
    {
        var client = new WolframAlphaClient(Utils.CreateMockedHttpClient((HttpStatusCode.OK, WolframAlphaTestData.AutocompleteResponse)));

        var results = await client.GetAutocompleteResultsAsync("2 +", "en", TestContext.Current.CancellationToken);

        Assert.Equal(["2 + 2", "2 + 3"], results);
    }

    [Fact]
    public async Task SendQueryAsync_Parses_Success_Result()
    {
        var result = await SendQueryAsync(WolframAlphaTestData.SuccessResponse);

        Assert.Equal(WolframAlphaResultType.Success, result.Type);
        Assert.NotEmpty(result.Warnings);
        var pod = Assert.Single(result.Pods);
        Assert.Equal("Input", pod.Title);
        var subPod = Assert.Single(pod.SubPods);
        Assert.Equal("2 + 2", subPod.PlainText);
        Assert.Equal("https://example.com/i.gif", subPod.Image.SourceUrl);
        Assert.Equal(100, subPod.Image.Width);
        Assert.Equal("image/gif", subPod.Image.ContentType);
    }

    [Fact]
    public async Task SendQueryAsync_Parses_DidYouMean_Result()
    {
        var result = await SendQueryAsync(WolframAlphaTestData.DidYouMeanResponse);

        Assert.Equal(WolframAlphaResultType.DidYouMean, result.Type);
        var suggestion = Assert.Single(result.DidYouMeans);
        Assert.Equal("kitten", suggestion.Value);
        Assert.Equal("medium", suggestion.Level);
        Assert.Equal(0.5f, suggestion.Score);
    }

    [Fact]
    public async Task SendQueryAsync_Parses_FutureTopic_Result()
    {
        var result = await SendQueryAsync(WolframAlphaTestData.FutureTopicResponse);

        Assert.Equal(WolframAlphaResultType.FutureTopic, result.Type);
        Assert.NotNull(result.FutureTopic);
        Assert.Equal("Microsoft Windows", result.FutureTopic.Topic);
        Assert.NotEmpty(result.FutureTopic.Message);
    }

    [Fact]
    public async Task SendQueryAsync_Parses_NoResult()
    {
        var result = await SendQueryAsync(WolframAlphaTestData.NoResultResponse);

        Assert.Equal(WolframAlphaResultType.NoResult, result.Type);
        Assert.Empty(result.Pods);
    }

    [Fact]
    public async Task SendQueryAsync_Parses_Error_Result()
    {
        var result = await SendQueryAsync(WolframAlphaTestData.ErrorResponse);

        Assert.Equal(WolframAlphaResultType.Error, result.Type);
        Assert.NotNull(result.ErrorInfo);
        Assert.Equal(1000, result.ErrorInfo.StatusCode);
        Assert.NotEmpty(result.ErrorInfo.Message);
    }

    private static Task<IWolframAlphaResult> SendQueryAsync(string fixture)
    {
        var client = new WolframAlphaClient(Utils.CreateMockedHttpClient((HttpStatusCode.OK, fixture)));
        return client.SendQueryAsync("test", "en", cancellationToken: TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(GetWolframAlphaErrorInfoConverterData), DisableDiscoveryEnumeration = true)]
    public void WolframAlphaErrorInfoConverter_Returns_Expected_Results(string input, WolframAlphaErrorInfo? expectedResult)
    {
        var result = JsonSerializer.Deserialize<WolframAlphaErrorInfo?>(input, _wolframAlphaOptions);

        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [MemberData(nameof(ArrayOrObjectConverterData), DisableDiscoveryEnumeration = true)]
    public void ArrayOrObjectConverter_Returns_Expected_Results(string input, IReadOnlyList<WolframAlphaWarning> expectedResult)
    {
        var result = JsonSerializer.Deserialize<IReadOnlyList<WolframAlphaWarning>>(input, _wolframAlphaOptions);

        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void WolframAlphaErrorInfoConverter_Throws_NotSupportedException()
        => Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new WolframAlphaErrorInfo(0, "test"), _wolframAlphaOptions));

    [Fact]
    public void ArrayOrObjectConverter_Throws_Exceptions()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IReadOnlyList<string>>("true", _wolframAlphaOptions));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize<IReadOnlyList<string>>(["test"], _wolframAlphaOptions));
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