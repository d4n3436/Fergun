using System;
using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Musixmatch;

/// <summary>
/// Represents a converter of <see cref="bool"/> that converts 1/0 to true/false.
/// </summary>
public class BoolConverter : JsonConverter<bool>
{
    /// <inheritdoc/>
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => Utf8Parser.TryParse(reader.ValueSpan, out bool b, out _, 'l') ? b : throw new InvalidOperationException($"Cannot get the value of a token type '{reader.TokenType}' as a boolean."),
            JsonTokenType.Number => reader.TryGetInt32(out int val) && val != 0,
            _ => throw new InvalidOperationException($"Cannot get the value of a token type '{reader.TokenType}' as a boolean.")
        };

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteBooleanValue(value);
}