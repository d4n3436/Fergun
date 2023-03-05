using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents a converter that handles strings that should be lists.
/// </summary>
public class ArrayOrStringConverter : JsonConverter<IReadOnlyList<string>>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.StartArray => JsonSerializer.Deserialize<IReadOnlyList<string>>(ref reader)!,
            JsonTokenType.String => reader.ValueTextEquals(ReadOnlySpan<byte>.Empty) ? Array.Empty<string>() : new[] { reader.GetString()! },
            JsonTokenType.Null => Array.Empty<string>(),
            _ => throw new JsonException("Token type must be either array, string or null.")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}