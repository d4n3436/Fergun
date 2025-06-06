using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Handles cases where "pronunciation" is a string.
/// </summary>
public class PronunciationConverter : JsonConverter<EntryPronunciation>
{
    /// <inheritdoc/>
    public override EntryPronunciation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String => new EntryPronunciation(reader.GetString()!),
            JsonTokenType.StartObject => JsonSerializer.Deserialize<EntryPronunciation>(ref reader)!, // HACK: options is not passed to avoid a stack overflow
            _ => throw new JsonException("Token type must be either string or object.")
        };

    /// <inheritdoc/>
    [ExcludeFromCodeCoverage(Justification = "Converter is only used for deserialization.")]
    public override void Write(Utf8JsonWriter writer, EntryPronunciation value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}