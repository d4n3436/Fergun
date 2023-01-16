using System.Text.Json.Serialization;

namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaResult"/>
public class WolframAlphaResult : IWolframAlphaResult
{
    private WolframAlphaResultType? _type;

    /// <summary>
    /// Initializes a new instance of the <see cref="WolframAlphaResult"/> class with the provided values.
    /// </summary>
    /// <param name="isSuccess">Whether if the result is successful.</param>
    /// <param name="pods">The pods.</param>
    /// <param name="didYouMeans">The alternative queries.</param>
    /// <param name="warnings">The warnings.</param>
    /// <param name="futureTopic">The future topic info.</param>
    /// <param name="errorInfo">The error info.</param>
    public WolframAlphaResult(bool isSuccess, IReadOnlyList<WolframAlphaPod>? pods, IReadOnlyList<WolframAlphaQuerySuggestion>? didYouMeans,
        IReadOnlyList<WolframAlphaWarning>? warnings, WolframAlphaFutureTopic? futureTopic, WolframAlphaErrorInfo? errorInfo)
    {
        IsSuccess = isSuccess;
        Pods = pods ?? Array.Empty<WolframAlphaPod>();
        DidYouMeans = didYouMeans ?? Array.Empty<WolframAlphaQuerySuggestion>();
        Warnings = warnings ?? Array.Empty<WolframAlphaWarning>();
        FutureTopic = futureTopic;
        ErrorInfo = errorInfo;
    }

    /// <inheritdoc/>
    public WolframAlphaResultType Type => _type ??= GetResultType();

    /// <inheritdoc/>
    [JsonPropertyName("success")]
    public bool IsSuccess { get; }

    /// <inheritdoc cref="IWolframAlphaResult.Pods"/>
    [JsonPropertyName("pods")]
    public IReadOnlyList<WolframAlphaPod> Pods { get; }

    /// <inheritdoc cref="IWolframAlphaResult.DidYouMeans"/>
    [JsonPropertyName("didyoumeans")]
    [JsonConverter(typeof(ArrayOrObjectConverter<WolframAlphaQuerySuggestion>))]
    public IReadOnlyList<WolframAlphaQuerySuggestion> DidYouMeans { get; }

    /// <inheritdoc cref="IWolframAlphaResult.Warnings"/>
    [JsonPropertyName("warnings")]
    [JsonConverter(typeof(ArrayOrObjectConverter<WolframAlphaWarning>))]
    public IReadOnlyList<WolframAlphaWarning> Warnings { get; }

    /// <inheritdoc cref="IWolframAlphaResult.FutureTopic"/>
    [JsonPropertyName("futuretopic")]
    public WolframAlphaFutureTopic? FutureTopic { get;  }

    /// <inheritdoc cref="IWolframAlphaResult.ErrorInfo"/>
    [JsonPropertyName("error")]
    [JsonConverter(typeof(WolframAlphaErrorInfoConverter))]
    public WolframAlphaErrorInfo? ErrorInfo { get; }

    /// <inheritdoc/>
    IReadOnlyList<IWolframAlphaPod> IWolframAlphaResult.Pods => Pods;

    /// <inheritdoc/>
    IReadOnlyList<IWolframAlphaQuerySuggestion> IWolframAlphaResult.DidYouMeans => DidYouMeans;

    /// <inheritdoc/>
    IReadOnlyList<IWolframAlphaWarning> IWolframAlphaResult.Warnings => Warnings;

    /// <inheritdoc/>
    IWolframAlphaFutureTopic? IWolframAlphaResult.FutureTopic => FutureTopic;

    /// <inheritdoc/>
    IWolframAlphaErrorInfo? IWolframAlphaResult.ErrorInfo => ErrorInfo;

    private WolframAlphaResultType GetResultType()
    {
        if (FutureTopic is not null)
        {
            return WolframAlphaResultType.FutureTopic;
        }

        if (DidYouMeans.Count > 0)
        {
            return WolframAlphaResultType.DidYouMean;
        }

        if (!IsSuccess && ErrorInfo is not null)
        {
            return WolframAlphaResultType.Error;
        }

        return Pods.Count == 0 ? WolframAlphaResultType.NoResult : WolframAlphaResultType.Success;
    }
}