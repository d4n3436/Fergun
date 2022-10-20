using System.Text.Json.Serialization;

namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaErrorInfo"/>
public record WolframAlphaErrorInfo(
    [property: JsonPropertyName("code")] [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] int StatusCode,
    [property: JsonPropertyName("msg")] string Message) : IWolframAlphaErrorInfo;