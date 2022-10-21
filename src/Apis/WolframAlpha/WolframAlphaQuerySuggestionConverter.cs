using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents a converter of <see cref="WolframAlphaQuerySuggestion"/> that handles objects that should be arrays.
/// </summary>
internal class WolframAlphaQuerySuggestionConverter : JsonConverter<IReadOnlyList<WolframAlphaQuerySuggestion>>
{
    /// <inheritdoc />
    public override IReadOnlyList<WolframAlphaQuerySuggestion> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.StartArray => JsonSerializer.Deserialize<IReadOnlyList<WolframAlphaQuerySuggestion>>(ref reader, options)!,
            JsonTokenType.StartObject => new[] { JsonSerializer.Deserialize<WolframAlphaQuerySuggestion>(ref reader, options)! },
            _ => throw new InvalidOperationException("Token type must be either array or object.")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<WolframAlphaQuerySuggestion> value, JsonSerializerOptions options)
        => throw new NotSupportedException("This method is not supported.");
}