using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents a converter of <see cref="WolframAlphaWarning"/> that handles objects that should be arrays.
/// </summary>
internal class WolframAlphaWarningConverter : JsonConverter<IReadOnlyList<WolframAlphaWarning>>
{
    /// <inheritdoc />
    public override IReadOnlyList<WolframAlphaWarning> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.StartArray => JsonSerializer.Deserialize<IReadOnlyList<WolframAlphaWarning>>(ref reader, options)!,
            JsonTokenType.StartObject => new[] { JsonSerializer.Deserialize<WolframAlphaWarning>(ref reader, options)! },
            _ => throw new InvalidOperationException("Token type must be either array or object.")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<WolframAlphaWarning> value, JsonSerializerOptions options)
        => throw new NotSupportedException("This method is not supported.");
}