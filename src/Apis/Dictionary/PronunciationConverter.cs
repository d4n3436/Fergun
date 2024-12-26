using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Handles cases where "pronunciation" is a string.
/// </summary>
public class PronunciationConverter : JsonConverter<EntryPronunciation>
{
    /// <inheritdoc/>
    public override EntryPronunciation? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => new EntryPronunciation(reader.GetString()!, null),
            JsonTokenType.StartObject => JsonSerializer.Deserialize<EntryPronunciation>(ref reader, options),
            JsonTokenType.Null => null,
            _ => throw new JsonException("Token type must be either string, object or null.")
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, EntryPronunciation value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}