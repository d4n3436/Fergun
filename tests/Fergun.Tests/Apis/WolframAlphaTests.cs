using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoBogus;
using Fergun.Apis.WolframAlpha;
using Moq;
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
    public async Task GetAutocompleteResultsAsync_Throws_OperationCanceledException_With_Canceled_CancellationToken()
    {
        using var cts = new CancellationTokenSource(0);
        await Assert.ThrowsAsync<OperationCanceledException>(() => _wolframAlphaClient.GetAutocompleteResultsAsync("test", "en", cts.Token));
    }

    [Fact]
    public async Task SendQueryAsync_Throws_OperationCanceledException_With_Canceled_CancellationToken()
    {
        using var cts = new CancellationTokenSource(0);
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