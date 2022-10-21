using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents a converter of <see cref="WolframAlphaErrorInfo"/> that handles cases where the type is a boolean.
/// </summary>
internal class WolframAlphaErrorInfoConverter : JsonConverter<WolframAlphaErrorInfo>
{
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, WolframAlphaErrorInfo value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);

    /// <inheritdoc/>
    public override WolframAlphaErrorInfo? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.True => null,
            JsonTokenType.False => null,
            _ => JsonSerializer.Deserialize<WolframAlphaErrorInfo>(ref reader)
        };
}