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
    /// <param name="futureTopic">The future topic info.</param>
    /// <param name="errorInfo">The error info.</param>
    public WolframAlphaResult(bool isSuccess, IReadOnlyList<WolframAlphaPod>? pods, IReadOnlyList<WolframAlphaQuerySuggestion>? didYouMeans,
        WolframAlphaFutureTopic? futureTopic, WolframAlphaErrorInfo? errorInfo)
    {
        IsSuccess = isSuccess;
        Pods = pods ?? Array.Empty<WolframAlphaPod>();
        DidYouMeans = didYouMeans ?? Array.Empty<WolframAlphaQuerySuggestion>();
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
    public IReadOnlyList<WolframAlphaQuerySuggestion> DidYouMeans { get; }

    /// <inheritdoc cref="IWolframAlphaResult.FutureTopic"/>
    [JsonPropertyName("futuretopic")]
    public WolframAlphaFutureTopic? FutureTopic { get;  }

    /// <inheritdoc cref="IWolframAlphaResult.ErrorInfo"/>
    [JsonPropertyName("error")]
    [JsonConverter(typeof(ErrorConverter))]
    public WolframAlphaErrorInfo? ErrorInfo { get; }

    /// <inheritdoc/>
    IReadOnlyList<IWolframAlphaPod> IWolframAlphaResult.Pods => Pods;

    /// <inheritdoc/>
    IReadOnlyList<IWolframAlphaQuerySuggestion> IWolframAlphaResult.DidYouMeans => DidYouMeans;

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

        if (!IsSuccess)
        {
            return ErrorInfo is not null ? WolframAlphaResultType.Error : WolframAlphaResultType.Unknown;
        }

        return WolframAlphaResultType.Success;
    }
}